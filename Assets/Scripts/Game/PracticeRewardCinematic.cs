using System;
using System.Collections.Generic;

namespace Horizon.Game
{
    public sealed class PracticeRewardCinematic
    {
        public const float ReleaseTime = 2.9f;
        public const float GainTime = ReleaseTime + .23f;
        public const float LandingTime = ReleaseTime + 1.32f;
        public const float CostTime = ReleaseTime + 1.78f;
        public const float Duration = 6.1f;
        private const float CountSeconds = 1.22f;
        private readonly int[] ticks;
        private readonly string finalGains, finalCosts;
        private readonly float strength;

        public readonly string EventId;
        public readonly float Seed;
        public readonly int Difficulty;
        public bool HasGain { get; }

        public PracticeRewardCinematic(ActionRecord action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            EventId = action.nodeId;
            Seed = CinematicEnergy.VisualSeed(EventId);
            Difficulty = action.practice?.difficulty ?? 2;
            strength = Difficulty == 1 ? .78f : Difficulty == 2 ? .90f : .98f;
            ticks = PracticeReward.Ticks(action.actualNow);
            HasGain = Array.Exists(ticks, value => value > 0);
            finalGains = FormatChanges(Duration, true, true);
            finalCosts = FormatChanges(Duration, false, true);
        }

        public static bool Supports(PracticeSet set) => set != PracticeSet.Street && set != PracticeSet.Track;

        public int TicksAt(int axis) => ticks[axis];

        public int DisplayTicks(int axis, float time, bool reducedMotion)
        {
            int value = ticks[axis];
            if (value <= 0) return time >= CostTime ? value : 0;
            if (time < GainTime) return 0;
            if (reducedMotion) return value;
            float progress = ImmersiveMotion.Smooth((time - GainTime) / CountSeconds);
            return (int)Math.Min(value, 1 + Math.Floor((value - 1d) * progress));
        }

        public string Changes(float time, bool gains, bool reducedMotion)
        {
            if (!gains) return time >= CostTime ? finalCosts : "";
            if (time < GainTime) return "";
            return reducedMotion || time >= GainTime + CountSeconds ? finalGains : FormatChanges(time, true, false);
        }

        private string FormatChanges(float time, bool gains, bool reducedMotion)
        {
            var visible = new List<string>(ticks.Length);
            for (int axis = 0; axis < ticks.Length; axis++)
            {
                int value = DisplayTicks(axis, time, reducedMotion);
                if (value == 0 || (value > 0) != gains) continue;
                visible.Add(PracticeReward.Names[axis] + (gains ? " +" : " −") +
                    PracticeReward.Format(value > 0 ? value : -value));
            }
            // Two axes per line keep receipts with four or more gains readable.
            if (gains && visible.Count > 3)
                for (int i = visible.Count - 2; i >= 0; i -= 2)
                {
                    visible[i] += "　" + visible[i + 1];
                    visible.RemoveAt(i + 1);
                }
            return string.Join(gains ? "\n" : "　", visible);
        }

        public PracticeRewardCinematicFrame Sample(float seconds, bool reducedMotion)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
            float time = Math.Max(0, Math.Min(Duration, seconds));
            float age = time - ReleaseTime;
            if (!HasGain || reducedMotion)
                return new PracticeRewardCinematicFrame(time, age, 0, 0, 0, HasGain && age >= 0 ? 1 : 0, default);

            // The case leans into three waves, braces, then recoils from its own hinge.
            float first = Crest(time, .35f, .79f, 1.19f);
            float second = Crest(time, 1.12f, 1.58f, 1.96f);
            float third = Crest(time, 1.85f, 2.40f, 2.65f);
            float brace = Window(time, 2.48f, 2.67f);
            float charge = age < 0 ? Math.Min(1, .04f + .57f * Window(time, .25f, 2.64f) +
                .32f * first + .36f * second + .42f * third) : .20f * (1 - Window(age, .11f, 1.42f));
            float compression = age < 0 ? .07f * first + .095f * second + .12f * third + .13f * brace :
                .13f * (1 - Window(age, 0, .11f)) - .055f * Crest(age, .04f, .15f, .39f);
            float tilt = age < 0 ? -5 * first + 7 * second - 9 * third - 3 * brace :
                -3 * (1 - Window(age, 0, .08f)) + 9 * Crest(age, .02f, .15f, .49f) - 3 * Crest(age, .38f, .52f, .71f);
            float lid = Window(age, 0, .35f) + .15f * Crest(age, .15f, .39f, .70f);
            return new PracticeRewardCinematicFrame(time, age, charge, compression, tilt, lid, EnergyRelease.SampleScenic(age, strength));
        }

        private static float Crest(float time, float from, float peak, float end) =>
            Window(time, from, peak) * (1 - Window(time, peak, end));

        private static float Window(float time, float from, float to) => ImmersiveMotion.Smooth((time - from) / (to - from));
    }

    public readonly struct PracticeRewardCinematicFrame
    {
        public readonly float Time, Age, Charge, Compression, Tilt, Lid;
        public readonly EnergyRelease Release;

        public PracticeRewardCinematicFrame(float time, float age, float charge, float compression, float tilt,
            float lid, EnergyRelease release)
        {
            Time = time;
            Age = age;
            Charge = charge;
            Compression = compression;
            Tilt = tilt;
            Lid = lid;
            Release = release;
        }
    }
}
