﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Taiko.Judgements;

namespace osu.Game.Rulesets.Taiko.Objects
{
    public class Swell : TaikoHitObject, IHasEndTime
    {
        public double EndTime
        {
            get => StartTime + Duration;
            set => Duration = value - StartTime;
        }

        public double Duration { get; set; }

        /// <summary>
        /// The number of hits required to complete the swell successfully.
        /// </summary>
        public int RequiredHits = 10;

        public override bool IsStrong
        {
            set => throw new NotSupportedException($"{nameof(Swell)} cannot be a strong hitobject.");
        }

        protected override void CreateNestedHitObjects(CancellationToken cancellationToken)
        {
            base.CreateNestedHitObjects(cancellationToken);

            for (int i = 0; i < RequiredHits; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                AddNested(new SwellTick());
            }
        }

        public override Judgement CreateJudgement() => new TaikoSwellJudgement();

        protected override HitWindows CreateHitWindows() => HitWindows.Empty;
    }
}
