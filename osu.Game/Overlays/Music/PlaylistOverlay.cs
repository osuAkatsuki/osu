﻿// Copyright (c) 2007-2018 ppy Pty Ltd <contact@ppy.sh>.
// Licensed under the MIT Licence - https://raw.githubusercontent.com/ppy/osu/master/LICENCE

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using OpenTK;
using OpenTK.Graphics;

namespace osu.Game.Overlays.Music
{
    public class PlaylistOverlay : OverlayContainer
    {
        private const float transition_duration = 600;
        private const float playlist_height = 510;

        /// <summary>
        /// Invoked when the order of an item in the list has changed.
        /// The second parameter indicates the new index of the item.
        /// </summary>
        public Action<BeatmapSetInfo, int> OrderChanged;

        private GameBeatmap beatmap;
        private BeatmapManager beatmaps;

        private FilterControl filter;
        private PlaylistList list;

        [BackgroundDependencyLoader]
        private void load(OsuColour colours, GameBeatmap beatmap, BeatmapManager beatmaps)
        {
            this.beatmap = beatmap.GetBoundCopy();
            this.beatmaps = beatmaps;

            Children = new Drawable[]
            {
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    CornerRadius = 5,
                    Masking = true,
                    EdgeEffect = new EdgeEffectParameters
                    {
                        Type = EdgeEffectType.Shadow,
                        Colour = Color4.Black.Opacity(40),
                        Radius = 5,
                    },
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            Colour = colours.Gray3,
                            RelativeSizeAxes = Axes.Both,
                        },
                        list = new PlaylistList
                        {
                            RelativeSizeAxes = Axes.Both,
                            Padding = new MarginPadding { Top = 95, Bottom = 10, Right = 10 },
                            Selected = itemSelected,
                            OrderChanged = (s, i) => OrderChanged?.Invoke(s, i)
                        },
                        filter = new FilterControl
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            ExitRequested = () => State = Visibility.Hidden,
                            FilterChanged = search => list.Filter(search),
                            Padding = new MarginPadding(10),
                        },
                    },
                },
            };

            filter.Search.OnCommit = (sender, newText) =>
            {
                BeatmapInfo toSelect = list.FirstVisibleSet?.Beatmaps?.FirstOrDefault();
                if (toSelect != null)
                {
                    beatmap.Value = beatmaps.GetWorkingBeatmap(toSelect);
                    beatmap.Value.Track.Restart();
                }
            };
        }

        protected override void PopIn()
        {
            filter.Search.HoldFocus = true;
            Schedule(() => GetContainingInputManager().ChangeFocus(filter.Search));

            this.ResizeTo(new Vector2(1, playlist_height), transition_duration, Easing.OutQuint);
            this.FadeIn(transition_duration, Easing.OutQuint);
        }

        protected override void PopOut()
        {
            filter.Search.HoldFocus = false;

            this.ResizeTo(new Vector2(1, 0), transition_duration, Easing.OutQuint);
            this.FadeOut(transition_duration);
        }

        private void itemSelected(BeatmapSetInfo set)
        {
            if (set.ID == (beatmap.Value?.BeatmapSetInfo?.ID ?? -1))
            {
                beatmap.Value?.Track?.Seek(0);
                return;
            }

            beatmap.Value = beatmaps.GetWorkingBeatmap(set.Beatmaps.First());
            beatmap.Value.Track.Restart();
        }
    }

    //todo: placeholder
    public enum PlaylistCollection
    {
        All
    }
}
