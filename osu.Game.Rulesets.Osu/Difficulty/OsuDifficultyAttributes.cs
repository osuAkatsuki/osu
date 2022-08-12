// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Newtonsoft.Json;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Osu.Difficulty
{
    public class OsuDifficultyAttributes : DifficultyAttributes
    {
        /// <summary>
        /// The difficulty corresponding to the aim skill.
        /// </summary>
        [JsonProperty("aim_difficulty")]
        public double AimDifficulty { get; set; }

        /// <summary>
        /// The difficulty corresponding to the speed skill.
        /// </summary>
        [JsonProperty("speed_difficulty")]
        public double SpeedDifficulty { get; set; }

        /// <summary>
        /// The number of clickable objects weighted by difficulty.
        /// Related to <see cref="SpeedDifficulty"/>
        /// </summary>
        [JsonProperty("speed_note_count")]
        public double SpeedNoteCount { get; set; }

        /// <summary>
        /// The difficulty corresponding to the flashlight skill.
        /// </summary>
        [JsonProperty("flashlight_difficulty")]
        public double FlashlightDifficulty { get; set; }

        /// <summary>
        /// Describes how much of <see cref="AimDifficulty"/> is contributed to by hitcircles or sliders.
        /// A value closer to 1.0 indicates most of <see cref="AimDifficulty"/> is contributed by hitcircles.
        /// A value closer to 0.0 indicates most of <see cref="AimDifficulty"/> is contributed by sliders.
        /// </summary>
        [JsonProperty("slider_factor")]
        public double SliderFactor { get; set; }

        [JsonProperty("aim_difficult_strain_count")]
        public double AimDifficultStrainCount { get; set; }

        [JsonProperty("speed_difficult_strain_count")]
        public double SpeedDifficultStrainCount { get; set; }

        /// <summary>
        /// The perceived approach rate inclusive of rate-adjusting mods (DT/HT/etc).
        /// </summary>
        /// <remarks>
        /// Rate-adjusting mods don't directly affect the approach rate difficulty value, but have a perceived effect as a result of adjusting audio timing.
        /// </remarks>
        [JsonProperty("approach_rate")]
        public double ApproachRate { get; set; }

        /// <summary>
        /// The perceived overall difficulty inclusive of rate-adjusting mods (DT/HT/etc).
        /// </summary>
        /// <remarks>
        /// Rate-adjusting mods don't directly affect the overall difficulty value, but have a perceived effect as a result of adjusting audio timing.
        /// </remarks>
        [JsonProperty("overall_difficulty")]
        public double OverallDifficulty { get; set; }

        /// <summary>
        /// The beatmap's drain rate. This doesn't scale with rate-adjusting mods.
        /// </summary>
        [JsonProperty("drain_rate")]
        public double DrainRate { get; set; }

        /// <summary>
        /// The number of hitcircles in the beatmap.
        /// </summary>
        [JsonProperty("hit_circle_count")]
        public int HitCircleCount { get; set; }

        /// <summary>
        /// The number of sliders in the beatmap.
        /// </summary>
        [JsonProperty("slider_count")]
        public int SliderCount { get; set; }

        /// <summary>
        /// The number of spinners in the beatmap.
        /// </summary>
        [JsonProperty("spinner_count")]
        public int SpinnerCount { get; set; }

        public override IEnumerable<(int attributeId, object value)> ToDatabaseAttributes()
        {
            foreach (var v in base.ToDatabaseAttributes())
                yield return v;

            yield return (ATTRIB_ID_AIM, AimDifficulty);
            yield return (ATTRIB_ID_SPEED, SpeedDifficulty);
            yield return (ATTRIB_ID_OVERALL_DIFFICULTY, OverallDifficulty);
            yield return (ATTRIB_ID_APPROACH_RATE, ApproachRate);
            yield return (ATTRIB_ID_MAX_COMBO, MaxCombo);
            yield return (ATTRIB_ID_DIFFICULTY, StarRating);

            if (ShouldSerializeFlashlightRating())
                yield return (ATTRIB_ID_FLASHLIGHT, FlashlightDifficulty);

            yield return (ATTRIB_ID_SLIDER_FACTOR, SliderFactor);
            yield return (ATTRIB_ID_AIM_DIFFICULT_STRAIN_COUNT, AimDifficultStrainCount);
            yield return (ATTRIB_ID_SPEED_DIFFICULT_STRAIN_COUNT, SpeedDifficultStrainCount);
            yield return (ATTRIB_ID_SPEED_NOTE_COUNT, SpeedNoteCount);

            yield return (ATTRIB_ID_DRAIN_RATE, DrainRate);
            yield return (ATTRIB_ID_HIT_CIRCLE_COUNT, HitCircleCount);
            yield return (ATTRIB_ID_SLIDER_COUNT, SliderCount);
            yield return (ATTRIB_ID_SPINNER_COUNT, SpinnerCount);
        }

        public override void FromDatabaseAttributes(IReadOnlyDictionary<int, double> values, IBeatmapOnlineInfo onlineInfo)
        {
            base.FromDatabaseAttributes(values, onlineInfo);

            AimDifficulty = values[ATTRIB_ID_AIM];
            SpeedDifficulty = values[ATTRIB_ID_SPEED];
            OverallDifficulty = values[ATTRIB_ID_OVERALL_DIFFICULTY];
            ApproachRate = values[ATTRIB_ID_APPROACH_RATE];
            MaxCombo = (int)values[ATTRIB_ID_MAX_COMBO];
            StarRating = values[ATTRIB_ID_DIFFICULTY];
            FlashlightDifficulty = values.GetValueOrDefault(ATTRIB_ID_FLASHLIGHT);
            SliderFactor = values[ATTRIB_ID_SLIDER_FACTOR];
            AimDifficultStrainCount = values[ATTRIB_ID_AIM_DIFFICULT_STRAIN_COUNT];
            SpeedDifficultStrainCount = values[ATTRIB_ID_SPEED_DIFFICULT_STRAIN_COUNT];
            SpeedNoteCount = values[ATTRIB_ID_SPEED_NOTE_COUNT];

            DrainRate = values[ATTRIB_ID_DRAIN_RATE];
            HitCircleCount = (int)values[ATTRIB_ID_HIT_CIRCLE_COUNT];
            SliderCount = (int)values[ATTRIB_ID_SLIDER_COUNT];
            SpinnerCount = (int)values[ATTRIB_ID_SPINNER_COUNT];
        }

        #region Newtonsoft.Json implicit ShouldSerialize() methods

        // The properties in this region are used implicitly by Newtonsoft.Json to not serialise certain fields in some cases.
        // They rely on being named exactly the same as the corresponding fields (casing included) and as such should NOT be renamed
        // unless the fields are also renamed.

        [UsedImplicitly]
        public bool ShouldSerializeFlashlightRating() => Mods.Any(m => m is ModFlashlight);

        #endregion
    }
}
