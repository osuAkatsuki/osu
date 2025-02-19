// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using osu.Framework.Bindables;

namespace osu.Game.Screens.Play.HUD
{
    public interface ILeaderboardScore
    {
        BindableDouble TotalScore { get; }
        BindableDouble Accuracy { get; }
        BindableInt Combo { get; }

        BindableBool HasQuit { get; }

        /// <summary>
        /// An optional value to guarantee stable ordering.
        /// Lower numbers will appear higher in cases of <see cref="TotalScore"/> ties.
        /// </summary>
        Bindable<long> DisplayOrder { get; }
    }
}
