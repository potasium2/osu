// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators;
using osu.Game.Rulesets.Osu.Mods;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    /// Represents the skill required to read a given beatmap dependent on modifiers used as well as the Approach Rate of Objects.
    /// </summary>
    internal class Reading : OsuStrainSkill
    {
        private readonly bool hasHiddenMod;
        private readonly bool hasFlashlightMod;
        private readonly double ApproachRate;
        public Reading(Mod[] mods, double approachRate)
            : base(mods)
        {
            hasHiddenMod = mods.Any(m => m is OsuModHidden);
            hasFlashlightMod = mods.Any(m => m is OsuModFlashlight);
            ApproachRate = approachRate;
        }

        private double skillMultiplier => 0.05512;
        private double strainDecayBase => 0.15;

        private double currentStrain;

        private double strainDecay(double ms) => Math.Pow(strainDecayBase, ms / 1000);

        protected override double CalculateInitialStrain(double time, DifficultyHitObject current) => currentStrain * strainDecay(time - current.Previous(0).StartTime);

        protected override double StrainValueAt(DifficultyHitObject current)
        {
            currentStrain *= strainDecay(current.DeltaTime);
            currentStrain += ReadingEvaluator.ReadingDifficultyOf(current, hasHiddenMod, ApproachRate) * skillMultiplier;

            if (hasHiddenMod)
                currentStrain += ReadingEvaluator.HiddenDifficultyOf(current) * skillMultiplier;
            if (hasFlashlightMod)
                currentStrain += ReadingEvaluator.FlashlightDifficultyOf(current, hasHiddenMod) * skillMultiplier;

            return currentStrain;
        }

        public override double DifficultyValue() => GetCurrentStrainPeaks().Sum();
    }
}
