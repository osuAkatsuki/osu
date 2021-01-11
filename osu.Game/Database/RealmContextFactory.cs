// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;
using AutoMapper;
using osu.Framework.Platform;
using osu.Framework.Statistics;
using osu.Framework.Threading;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Input.Bindings;
using osu.Game.IO;
using osu.Game.Rulesets;
using osu.Game.Scoring;
using osu.Game.Skinning;
using Realms;

namespace osu.Game.Database
{
    public class RealmContextFactory : IRealmFactory
    {
        private readonly Storage storage;
        private readonly Scheduler scheduler;

        private const string database_name = @"client";

        private ThreadLocal<Realm> threadContexts;

        private readonly object writeLock = new object();

        private ThreadLocal<bool> refreshCompleted = new ThreadLocal<bool>();

        private bool rollbackRequired;

        private int currentWriteUsages;

        private Transaction currentWriteTransaction;

        public RealmContextFactory(Storage storage, Scheduler scheduler)
        {
            this.storage = storage;
            this.scheduler = scheduler;
            recreateThreadContexts();

            using (CreateContext())
            {
                // ensure our schema is up-to-date and migrated.
            }
        }

        private void onMigration(Migration migration, ulong oldschemaversion)
        {
        }

        private static readonly GlobalStatistic<int> reads = GlobalStatistics.Get<int>("Realm", "Get (Read)");
        private static readonly GlobalStatistic<int> writes = GlobalStatistics.Get<int>("Realm", "Get (Write)");
        private static readonly GlobalStatistic<int> commits = GlobalStatistics.Get<int>("Realm", "Commits");
        private static readonly GlobalStatistic<int> rollbacks = GlobalStatistics.Get<int>("Realm", "Rollbacks");
        private static readonly GlobalStatistic<int> contexts = GlobalStatistics.Get<int>("Realm", "Contexts");

        /// <summary>
        /// Get a context for the current thread for read-only usage.
        /// If a <see cref="RealmWriteUsage"/> is in progress, the existing write-safe context will be returned.
        /// </summary>
        public Realm Get()
        {
            reads.Value++;
            return getContextForCurrentThread();
        }

        /// <summary>
        /// Request a context for write usage. Can be consumed in a nested fashion (and will return the same underlying context).
        /// This method may block if a write is already active on a different thread.
        /// </summary>
        /// <returns>A usage containing a usable context.</returns>
        public RealmWriteUsage GetForWrite()
        {
            writes.Value++;
            Monitor.Enter(writeLock);
            Realm context;

            try
            {
                context = getContextForCurrentThread();

                currentWriteTransaction ??= context.BeginWrite();
            }
            catch
            {
                // retrieval of a context could trigger a fatal error.
                Monitor.Exit(writeLock);
                throw;
            }

            Interlocked.Increment(ref currentWriteUsages);

            return new RealmWriteUsage(context, usageCompleted) { IsTransactionLeader = currentWriteTransaction != null && currentWriteUsages == 1 };
        }

        // TODO: remove if not necessary.
        public void Schedule(Action action) => scheduler.Add(action);

        private Realm getContextForCurrentThread()
        {
            var context = threadContexts.Value;
            if (context?.IsClosed != false)
                threadContexts.Value = context = CreateContext();

            if (!refreshCompleted.Value)
            {
                context.Refresh();
                refreshCompleted.Value = true;
            }

            return context;
        }

        private void usageCompleted(RealmWriteUsage usage)
        {
            int usages = Interlocked.Decrement(ref currentWriteUsages);

            try
            {
                rollbackRequired |= usage.RollbackRequired;

                if (usages == 0)
                {
                    if (rollbackRequired)
                    {
                        rollbacks.Value++;
                        currentWriteTransaction?.Rollback();
                    }
                    else
                    {
                        commits.Value++;
                        currentWriteTransaction?.Commit();
                    }

                    currentWriteTransaction = null;
                    rollbackRequired = false;

                    refreshCompleted = new ThreadLocal<bool>();
                }
            }
            finally
            {
                Monitor.Exit(writeLock);
            }
        }

        private void recreateThreadContexts()
        {
            // Contexts for other threads are not disposed as they may be in use elsewhere. Instead, fresh contexts are exposed
            // for other threads to use, and we rely on the finalizer inside OsuDbContext to handle their previous contexts
            threadContexts?.Value.Dispose();
            threadContexts = new ThreadLocal<Realm>(CreateContext, true);
        }

        protected virtual Realm CreateContext()
        {
            contexts.Value++;
            return Realm.GetInstance(new RealmConfiguration(storage.GetFullPath($"{database_name}.realm", true))
            {
                SchemaVersion = 5,
                MigrationCallback = onMigration
            });
        }

        public void ResetDatabase()
        {
            lock (writeLock)
            {
                recreateThreadContexts();
                storage.DeleteDatabase(database_name);
            }
        }
    }

    public static class RealmExtensions
    {
        private static readonly IMapper mapper = new MapperConfiguration(c =>
        {
            c.ShouldMapField = fi => false;
            c.ShouldMapProperty = pi => pi.SetMethod != null && pi.SetMethod.IsPublic;

            c.CreateMap<BeatmapDifficulty, BeatmapDifficulty>();
            c.CreateMap<BeatmapInfo, BeatmapInfo>();
            c.CreateMap<BeatmapMetadata, BeatmapMetadata>();
            c.CreateMap<BeatmapSetFileInfo, BeatmapSetFileInfo>();

            c.CreateMap<BeatmapSetInfo, BeatmapSetInfo>()
             .ForMember(s => s.Beatmaps, d => d.MapFrom(s => s.Beatmaps))
             .ForMember(s => s.Files, d => d.MapFrom(s => s.Files))
             .MaxDepth(2);

            c.CreateMap<DatabasedKeyBinding, DatabasedKeyBinding>();
            c.CreateMap<RealmKeyBinding, RealmKeyBinding>();
            c.CreateMap<DatabasedSetting, DatabasedSetting>();
            c.CreateMap<FileInfo, FileInfo>();
            c.CreateMap<ScoreFileInfo, ScoreFileInfo>();
            c.CreateMap<SkinInfo, SkinInfo>();
            c.CreateMap<RulesetInfo, RulesetInfo>();
        }).CreateMapper();

        public static T Detach<T>(this T obj) where T : RealmObject
        {
            if (!obj.IsManaged)
                return obj;

            var detached = mapper.Map<T>(obj);

            //typeof(RealmObject).GetField("_realm", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(detached, null);

            return detached;
        }

        public static Live<T> Wrap<T>(this T obj, IRealmFactory contextFactory)
            where T : RealmObject, IHasGuidPrimaryKey => new Live<T>(obj, contextFactory);

        public static Live<T> WrapAsUnmanaged<T>(this T obj)
            where T : RealmObject, IHasGuidPrimaryKey => new Live<T>(obj, null);
    }
}
