// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Numerics;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators
{
    public static class ReadingEvaluator
    {
        // AR Constants
        private const double max_approach_rate_bonus = 8.67;
        private const double min_approach_rate_bonus = 10.33;
        private const double reading_hidden_bonus = 1.75;

        public static double ReadingDifficultyOf(DifficultyHitObject current, bool hidden, double approachRate)
        {
            if (current == null)
                return 0;

            var osuCurrent = (OsuDifficultyHitObject)current;
            var osuFirstFormerObj = (OsuDifficultyHitObject)current.Previous(0);
            var osuSecondFormerObj = (OsuDifficultyHitObject)current.Previous(1);

            double readingStrain = 0.0;

            // Bonus for low-AR
            if (approachRate < max_approach_rate_bonus)
            {
                readingStrain = 1.0 - Math.Pow(approachRate / max_approach_rate_bonus, 0.6);

                if (hidden)
                    readingStrain *= reading_hidden_bonus;
            }

            // Bonus for high-AR
            if (approachRate > min_approach_rate_bonus)
            {
                readingStrain = Math.Pow(approachRate / min_approach_rate_bonus, 4.0) - 1.0;
            }

            // TODO:

            // Low-AR:
            // Bonus for stacked return patterns
            // Bonus for Note Density (greater difficulty for aim > speed)
            // Bonus for rhythmically complex patterns

            // High-AR:
            // Bonus for cut patterns (Cut Streams, Stacked Triples, etc.)

            return readingStrain;
        }



        // Hidden Constants
        private const double max_stack_distance = 15;

        public static double HiddenDifficultyOf(DifficultyHitObject current)
        {
            if (current.Previous(2) == null)
                return 0;

            var osuCurrent = (OsuDifficultyHitObject)current;
            var osuFirstFormerObj = (OsuDifficultyHitObject)current.Previous(0);
            var osuSecondFormerObj = (OsuDifficultyHitObject)current.Previous(1);

            var currentBaseObject = (OsuHitObject)osuCurrent.BaseObject;

            double scalingFactor = 52.0 / currentBaseObject.Radius;
            double hiddenStrain = 0.0;

            // Bonus for stacked doubles and triples
            if (osuSecondFormerObj.LazyJumpDistance > max_stack_distance)
            {
                if (osuCurrent.MinimumJumpDistance < max_stack_distance)
                    hiddenStrain += osuCurrent.MinimumJumpDistance * scalingFactor / 2.0;
                if (osuFirstFormerObj.MinimumJumpDistance < max_stack_distance)
                    hiddenStrain += osuFirstFormerObj.MinimumJumpDistance * scalingFactor / 2.0;
            }



            // TODO:
            // Bonus for Flow
            // Bonus for Aim Consistency (Long Jump Patterns)

            return hiddenStrain;
        }



        // Flashlight Constants
        private const double max_opacity_bonus = 0.4;
        private const double flashlight_hidden_bonus = 1.2;

        private const double min_velocity = 0.5;
        private const double slider_multiplier = 1.3;

        private const double min_angle_multiplier = 0.2;

        /// <summary>
        /// Evaluates the difficulty of memorising and hitting an object, based on:
        /// <list type="bullet">
        /// <item><description>distance between a number of previous objects and the current object,</description></item>
        /// <item><description>the visual opacity of the current object,</description></item>
        /// <item><description>the angle made by the current object,</description></item>
        /// <item><description>length and speed of the current object (for sliders),</description></item>
        /// <item><description>and whether the hidden mod is enabled.</description></item>
        /// </list>
        /// </summary>
        public static double FlashlightDifficultyOf(DifficultyHitObject current, bool hidden)
        {
            if (current.BaseObject is Spinner)
                return 0;

            var osuCurrent = (OsuDifficultyHitObject)current;
            var osuHitObject = (OsuHitObject)(osuCurrent.BaseObject);

            double scalingFactor = 52.0 / osuHitObject.Radius;
            double smallDistNerf = 1.0;
            double cumulativeStrainTime = 0.0;

            double flashlightStrain = 0.0;

            OsuDifficultyHitObject lastObj = osuCurrent;

            double angleRepeatCount = 0.0;

            // This is iterating backwards in time from the current object.
            for (int i = 0; i < Math.Min(current.Index, 10); i++)
            {
                var currentObj = (OsuDifficultyHitObject)current.Previous(i);
                var currentHitObject = (OsuHitObject)(currentObj.BaseObject);

                cumulativeStrainTime += lastObj.StrainTime;

                if (!(currentObj.BaseObject is Spinner))
                {
                    double jumpDistance = (osuHitObject.StackedPosition - currentHitObject.StackedEndPosition).Length;

                    // We want to nerf objects that can be easily seen within the Flashlight circle radius.
                    if (i == 0)
                        smallDistNerf = Math.Min(1.0, jumpDistance / 75.0);

                    // We also want to nerf stacks so that only the first object of the stack is accounted for.
                    double stackNerf = Math.Min(1.0, (currentObj.LazyJumpDistance / scalingFactor) / 25.0);

                    // Bonus based on how visible the object is.
                    double opacityBonus = 1.0 + max_opacity_bonus * (1.0 - osuCurrent.OpacityAt(currentHitObject.StartTime, hidden));

                    flashlightStrain += stackNerf * opacityBonus * scalingFactor * jumpDistance / cumulativeStrainTime;

                    if (currentObj.Angle != null && osuCurrent.Angle != null)
                    {
                        // Objects further back in time should count less for the nerf.
                        if (Math.Abs(currentObj.Angle.Value - osuCurrent.Angle.Value) < 0.02)
                            angleRepeatCount += Math.Max(1.0 - 0.1 * i, 0.0);
                    }
                }

                lastObj = currentObj;
            }

            flashlightStrain = Math.Pow(smallDistNerf * flashlightStrain, 2.0);

            // Additional bonus for Hidden due to there being no approach circles.
            if (hidden)
                flashlightStrain *= flashlight_hidden_bonus;

            // Nerf patterns with repeated angles.
            flashlightStrain *= min_angle_multiplier + (1.0 - min_angle_multiplier) / (angleRepeatCount + 1.0);

            double sliderBonus = 0.0;

            if (osuCurrent.BaseObject is Slider osuSlider)
            {
                // Invert the scaling factor to determine the true travel distance independent of circle size.
                double pixelTravelDistance = osuSlider.LazyTravelDistance / scalingFactor;

                // Reward sliders based on velocity.
                sliderBonus = Math.Pow(Math.Max(0.0, pixelTravelDistance / osuCurrent.TravelTime - min_velocity), 0.5);

                // Longer sliders require more memorisation.
                sliderBonus *= pixelTravelDistance;

                // Nerf sliders with repeats, as less memorisation is required.
                if (osuSlider.RepeatCount > 0)
                    sliderBonus /= (osuSlider.RepeatCount + 1);
            }

            flashlightStrain += sliderBonus * slider_multiplier;

            return flashlightStrain;
        }
    }
}
