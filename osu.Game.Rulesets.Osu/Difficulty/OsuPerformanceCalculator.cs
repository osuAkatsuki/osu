// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Osu.Difficulty
{
    public class OsuPerformanceCalculator : PerformanceCalculator
    {
        public const double PERFORMANCE_BASE_MULTIPLIER = 1.14; // This is being adjusted to keep the final pp value scaled around what it used to be when changing things.

        private double accuracy;
        private int scoreMaxCombo;
        private int countGreat;
        private int countOk;
        private int countMeh;
        private int countMiss;

        private double effectiveMissCount;

        public OsuPerformanceCalculator()
            : base(new OsuRuleset())
        {
        }

        protected override PerformanceAttributes CreatePerformanceAttributes(ScoreInfo score, DifficultyAttributes attributes)
        {
            var osuAttributes = (OsuDifficultyAttributes)attributes;

            accuracy = score.Accuracy;
            scoreMaxCombo = score.MaxCombo;
            countGreat = score.Statistics.GetValueOrDefault(HitResult.Great);
            countOk = score.Statistics.GetValueOrDefault(HitResult.Ok);
            countMeh = score.Statistics.GetValueOrDefault(HitResult.Meh);
            countMiss = score.Statistics.GetValueOrDefault(HitResult.Miss);
            effectiveMissCount = calculateEffectiveMissCount(osuAttributes);

            double multiplier = PERFORMANCE_BASE_MULTIPLIER;

            if (score.Mods.Any(m => m is OsuModNoFail))
                multiplier *= Math.Max(0.90, 1.0 - 0.02 * effectiveMissCount);

            if (score.Mods.Any(m => m is OsuModSpunOut) && totalHits > 0)
                multiplier *= 1.0 - Math.Pow((double)osuAttributes.SpinnerCount / totalHits, 0.85);

            double aimValue = computeAimValue(score, osuAttributes);
            double speedValue = computeSpeedValue(score, osuAttributes);
            double accuracyValue = computeAccuracyValue(score, osuAttributes);
            double flashlightValue = computeFlashlightValue(score, osuAttributes);

            (double finalAimValue, double finalSpeedValue, double finalAccuracyValue, double finalFlashlightValue) =
                nerfRelaxFinalValues(aimValue, speedValue, accuracyValue, flashlightValue, score);

            double totalValue =
                Math.Pow(
                    finalAimValue +
                    finalSpeedValue +
                    finalAccuracyValue +
                    finalFlashlightValue,
                    1.0 / 1.1
                ) * multiplier;

            double finalTotalValue = nerfRelaxTotalValue(totalValue, score);

            return new OsuPerformanceAttributes
            {
                Aim = aimValue,
                Speed = speedValue,
                Accuracy = accuracyValue,
                Flashlight = flashlightValue,
                EffectiveMissCount = effectiveMissCount,
                Total = finalTotalValue
            };
        }

        private (double, double, double, double) nerfRelaxFinalValues(double aimValue, double speedValue, double accuracyValue, double flashlightValue, ScoreInfo score)
        {
            double accuracyFactor = 1.0;
            if (score.Mods.Any(m => m is OsuModRelax))
            {
                double flowFactor = aimValue / speedValue;
                if (flowFactor < 1.0)
                {
                    accuracyFactor = Math.Max(0.85, accuracy < 1.0 ? 0.95 - (1.0 - accuracy) : 0.95);
                    aimValue *= accuracyFactor;
                }
            }

            double nonDtBonus = 1.0;
            if (!score.Mods.Any(m => m is OsuModDoubleTime) && !score.Mods.Any(m => m is OsuModNightcore) &&
                !score.Mods.Any(m => m is OsuModHalfTime) && score.Mods.Any(m => m is OsuModRelax) &&
                Math.Abs(accuracyFactor - 1.0) < 0.01)
            {
                nonDtBonus += 0.01;
            }

            double finalAimValue = score.Mods.Any(m => m is OsuModRelax)
                ? Math.Pow(aimValue, 1.18 * nonDtBonus)
                : Math.Pow(aimValue, 1.1);

            double finalSpeedValue = score.Mods.Any(m => m is OsuModRelax)
                ? Math.Pow(speedValue, 0.83 * accuracyFactor)
                : score.Mods.Any(m => m is OsuModAutopilot) ? Math.Pow(speedValue, 1.12) : Math.Pow(speedValue, 1.1);

            double finalAccuracyValue = score.Mods.Any(m => m is OsuModRelax)
                ? Math.Pow(accuracyValue, 1.15 * accuracyFactor)
                : Math.Pow(accuracyValue, 1.1);

            double finalFlashlightValue = score.Mods.Any(m => m is OsuModRelax)
                ? Math.Pow(flashlightValue, 0.98)
                : score.Mods.Any(m => m is OsuModAutopilot)
                    ? Math.Pow(flashlightValue, 0.9)
                    : Math.Pow(flashlightValue, 1.1);

            return (finalAimValue, finalSpeedValue, finalAccuracyValue, finalFlashlightValue);
        }

        private double nerfRelaxTotalValue(double totalValue, ScoreInfo score)
        {
            if ((score.Mods.Any(m => m is OsuModDoubleTime) | score.Mods.Any(m => m is OsuModNightcore)) &&
                score.Mods.Any(m => m is OsuModHardRock))
                totalValue *= 1.025;

            // ParkourWizard user ID
            if (score.BeatmapInfo.Metadata.Author.OnlineID == 6938249)
                totalValue *= 0.9;

            switch (score.BeatmapInfo.OnlineID)
            {
                // Louder than steel [ok this is epic]
                case 1808605:
                    totalValue *= 0.85;
                    break;

                // over the top [Above the stars]
                case 1822147:
                    totalValue *= 0.70;
                    break;

                // Just press F [Parkour's ok this is epic]
                case 1844776:
                    totalValue *= 0.64;
                    break;

                // Hardware Store [skyapple mode]
                // HONESTY [RIGHTEOUSNESS OF MORALITY]
                case 1777768:
                    totalValue *= 0.90;
                    break;

                // Akatsuki compilation [ok this is akatsuki]
                case 1962833:
                    totalValue *= 0.885;
                    if (score.Mods.Any(m => m is OsuModDoubleTime) | score.Mods.Any(m => m is OsuModNightcore))
                        totalValue *= 0.83;

                    break;
            }

            return totalValue;
        }

        private double computeAimValue(ScoreInfo score, OsuDifficultyAttributes attributes)
        {
            if (score.Mods.Any(m => m is OsuModAutopilot))
                return 0.0;

            double aimValue = Math.Pow(5.0 * Math.Max(1.0, attributes.AimDifficulty / 0.0675) - 4.0, 3.0) / 100000.0;

            double lengthBonus = 0.95 + 0.4 * Math.Min(1.0, totalHits / 2000.0) +
                                 (totalHits > 2000 ? Math.Log10(totalHits / 2000.0) * 0.5 : 0.0);
            aimValue *= lengthBonus;

            // Penalize misses by assessing # of misses relative to the total # of objects. Default a 3% reduction for any # of misses.
            if (effectiveMissCount > 0)
                aimValue *= calculateMissPenalty(effectiveMissCount, attributes.AimDifficultStrainCount);

            double approachRateFactor = 0.0;
            if (attributes.ApproachRate > 10.33)
                approachRateFactor = 0.3 * (attributes.ApproachRate - 10.33);
            else if (attributes.ApproachRate < 8.0)
                approachRateFactor = 0.05 * (8.0 - attributes.ApproachRate);

            aimValue *= 1.0 + approachRateFactor * lengthBonus; // Buff for longer maps with high AR.

            if (score.Mods.Any(m => m is OsuModBlinds))
                aimValue *= 1.3 + (totalHits * (0.0016 / (1 + 2 * effectiveMissCount)) * Math.Pow(accuracy, 16)) * (1 - 0.003 * attributes.DrainRate * attributes.DrainRate);
            else if (score.Mods.Any(h => h is OsuModHidden))
            {
                // We want to give more reward for lower AR when it comes to aim and HD. This nerfs high AR and buffs lower AR.
                double hiddenFactor = score.Mods.Any(m => m is OsuModRelax) ? 0.05 : 0.04;
                double hiddenApproachRateFactor = score.Mods.Any(m => m is OsuModRelax) ? 11.0 : 12.0;
                aimValue *= 1.0 + hiddenFactor * (hiddenApproachRateFactor - attributes.ApproachRate);
            }

            // NOTE: this should be removed in the future, praying we get good ideas for this one day as this is super flawed
            // Buff aim pp in case of low EZ mod
            if (score.Mods.Any(m => m is OsuModEasy))
            {
                double buffFactor = 1.08;

                if (attributes.ApproachRate <= 8.0)
                    buffFactor += (7.0 - attributes.ApproachRate) / 100;

                aimValue *= buffFactor;
            }

            // We assume 15% of sliders in a map are difficult since there's no way to tell from the performance calculator.
            double estimateDifficultSliders = attributes.SliderCount * 0.15;

            if (attributes.SliderCount > 0)
            {
                double estimateSliderEndsDropped = Math.Clamp(Math.Min(countOk + countMeh + countMiss, attributes.MaxCombo - scoreMaxCombo), 0, estimateDifficultSliders);
                double sliderNerfFactor = (1 - attributes.SliderFactor) * Math.Pow(1 - estimateSliderEndsDropped / estimateDifficultSliders, 3) + attributes.SliderFactor;
                aimValue *= sliderNerfFactor;
            }

            aimValue *= accuracy;
            // It is important to consider accuracy difficulty when scaling with accuracy.
            double accuracyFactor = score.Mods.Any(m => m is OsuModRelax) ? 0.95 : 0.98;
            aimValue *= accuracyFactor + Math.Pow(attributes.OverallDifficulty, 2) / 2500;

            return aimValue;
        }

        private double computeSpeedValue(ScoreInfo score, OsuDifficultyAttributes attributes)
        {
            double speedValue = Math.Pow(5.0 * Math.Max(1.0, attributes.SpeedDifficulty / 0.0675) - 4.0, 3.0) / 100000.0;

            double lengthBonus = 0.95 + 0.4 * Math.Min(1.0, totalHits / 2000.0) +
                                 (totalHits > 2000 ? Math.Log10(totalHits / 2000.0) * 0.5 : 0.0);
            speedValue *= lengthBonus;

            // Penalize misses by assessing # of misses relative to the total # of objects. Default a 3% reduction for any # of misses.
            if (effectiveMissCount > 0)
                speedValue *= calculateMissPenalty(effectiveMissCount, attributes.SpeedDifficultStrainCount);

            double approachRateFactor = 0.0;
            if (attributes.ApproachRate > 10.33)
                approachRateFactor = 0.3 * (attributes.ApproachRate - 10.33);

            speedValue *= 1.0 + approachRateFactor * lengthBonus; // Buff for longer maps with high AR.

            if (score.Mods.Any(m => m is OsuModBlinds))
            {
                // Increasing the speed value by object count for Blinds isn't ideal, so the minimum buff is given.
                speedValue *= 1.12;
            }
            else if (score.Mods.Any(m => m is OsuModHidden))
            {
                // We want to give more reward for lower AR when it comes to aim and HD. This nerfs high AR and buffs lower AR.
                speedValue *= 1.0 + 0.04 * (12.0 - attributes.ApproachRate);
            }

            // Calculate accuracy assuming the worst case scenario
            double relevantTotalDiff = totalHits - attributes.SpeedNoteCount;
            double relevantCountGreat = Math.Max(0, countGreat - relevantTotalDiff);
            double relevantCountOk = Math.Max(0, countOk - Math.Max(0, relevantTotalDiff - countGreat));
            double relevantCountMeh = Math.Max(0, countMeh - Math.Max(0, relevantTotalDiff - countGreat - countOk));
            double relevantAccuracy = attributes.SpeedNoteCount == 0 ? 0 : (relevantCountGreat * 6.0 + relevantCountOk * 2.0 + relevantCountMeh) / (attributes.SpeedNoteCount * 6.0);

            // Scale the speed value with accuracy and OD.
            speedValue *= (0.95 + Math.Pow(attributes.OverallDifficulty, 2) / 750) * Math.Pow((accuracy + relevantAccuracy) / 2.0, (14.5 - Math.Max(attributes.OverallDifficulty, 8)) / 2);

            // Scale the speed value with # of 50s to punish doubletapping.
            speedValue *= Math.Pow(0.99, countMeh < totalHits / 500.0 ? 0 : countMeh - totalHits / 500.0);

            return speedValue;
        }

        private double computeAccuracyValue(ScoreInfo score, OsuDifficultyAttributes attributes)
        {
            // This percentage only considers HitCircles of any value - in this part of the calculation we focus on hitting the timing hit window.
            double betterAccuracyPercentage;
            int amountHitObjectsWithAccuracy = attributes.HitCircleCount;

            if (amountHitObjectsWithAccuracy > 0)
                betterAccuracyPercentage = ((countGreat - (totalHits - amountHitObjectsWithAccuracy)) * 6 + countOk * 2 + countMeh) / (double)(amountHitObjectsWithAccuracy * 6);
            else
                betterAccuracyPercentage = 0;

            // It is possible to reach a negative accuracy with this formula. Cap it at zero - zero points.
            if (betterAccuracyPercentage < 0)
                betterAccuracyPercentage = 0;

            // Lots of arbitrary values from testing.
            // Considering to use derivation from perfect accuracy in a probabilistic manner - assume normal distribution.
            double accuracyValue = Math.Pow(1.52163, attributes.OverallDifficulty) * Math.Pow(betterAccuracyPercentage, 24) * 2.83;

            // Bonus for many hitcircles - it's harder to keep good accuracy up for longer.
            accuracyValue *= Math.Min(1.15, Math.Pow(amountHitObjectsWithAccuracy / 1000.0, 0.3));

            // Increasing the accuracy value by object count for Blinds isn't ideal, so the minimum buff is given.
            if (score.Mods.Any(m => m is OsuModBlinds))
                accuracyValue *= 1.14;
            else if (score.Mods.Any(m => m is OsuModHidden))
                accuracyValue *= 1.08;

            if (score.Mods.Any(m => m is OsuModFlashlight))
                accuracyValue *= 1.02;

            return accuracyValue;
        }

        private double computeFlashlightValue(ScoreInfo score, OsuDifficultyAttributes attributes)
        {
            if (!score.Mods.Any(h => h is OsuModFlashlight))
                return 0.0;

            double flashlightValue = Math.Pow(attributes.FlashlightDifficulty, 2.0) * 25.0;

            // Penalize misses by assessing # of misses relative to the total # of objects. Default a 3% reduction for any # of misses.
            if (effectiveMissCount > 0)
                flashlightValue *= 0.97 * Math.Pow(1 - Math.Pow(effectiveMissCount / totalHits, 0.775), Math.Pow(effectiveMissCount, .875));

            flashlightValue *= getComboScalingFactor(attributes);

            // Account for shorter maps having a higher ratio of 0 combo/100 combo flashlight radius.
            flashlightValue *= 0.7 + 0.1 * Math.Min(1.0, totalHits / 200.0) +
                               (totalHits > 200 ? 0.2 * Math.Min(1.0, (totalHits - 200) / 200.0) : 0.0);

            // Scale the flashlight value with accuracy _slightly_.
            flashlightValue *= 0.5 + accuracy / 2.0;
            // It is important to also consider accuracy difficulty when doing that.
            flashlightValue *= 0.98 + Math.Pow(attributes.OverallDifficulty, 2) / 2500;

            return flashlightValue;
        }

        private double calculateEffectiveMissCount(OsuDifficultyAttributes attributes)
        {
            // Guess the number of misses + slider breaks from combo
            double comboBasedMissCount = 0.0;

            if (attributes.SliderCount > 0)
            {
                double fullComboThreshold = attributes.MaxCombo - 0.1 * attributes.SliderCount;
                if (scoreMaxCombo < fullComboThreshold)
                    comboBasedMissCount = fullComboThreshold / Math.Max(1.0, scoreMaxCombo);
            }

            // Clamp miss count to maximum amount of possible breaks
            comboBasedMissCount = Math.Min(comboBasedMissCount, countOk + countMeh + countMiss);

            return Math.Max(countMiss, comboBasedMissCount);
        }

        // Miss penalty assumes that a player will miss on the hardest parts of a map,
        // so we use the amount of relatively difficult sections to adjust miss penalty
        // to make it more punishing on maps with lower amount of hard sections.
        private double calculateMissPenalty(double missCount, double difficultStrainCount) => 0.94 / ((missCount / (2 * Math.Sqrt(difficultStrainCount))) + 1);
        private double getComboScalingFactor(OsuDifficultyAttributes attributes) => attributes.MaxCombo <= 0 ? 1.0 : Math.Min(Math.Pow(scoreMaxCombo, 0.8) / Math.Pow(attributes.MaxCombo, 0.8), 1.0);
        private int totalHits => countGreat + countOk + countMeh + countMiss;
    }
}
