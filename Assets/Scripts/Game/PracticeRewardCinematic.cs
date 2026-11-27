using System;
using System.Collections.Generic;

namespace Horizon.Game
{
    public sealed class PracticeRewardCinematic
    {
        public const float ReleaseTime = 2.9f;
        public const float GainTime = ReleaseTime + .23f;
        public const float HoldTime = ReleaseTime - .19f;
        public const float LandingTime = ReleaseTime + 1.16f;
        public const float CostTime = ReleaseTime + 1.78f;
        public const float Duration = 5.65f;
        public const float ObjectSpacing = .094f;
        private static readonly float[] LatchBeats = { 1.24f, 1.88f, 2.23f };
        private readonly int[] ticks;
        private readonly int[] slots = new int[6];
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
            strength = Difficulty == 1 ? .65f : Difficulty == 2 ? .88f : 1;
            ticks = PracticeReward.Ticks(action.actualNow);
            HasGain = Array.Exists(ticks, value => value > 0);
            int slot = 0;
            for (int axis = 0; axis < ticks.Length; axis++)
                if (ticks[axis] > 0) slots[axis] = slot++;
            finalGains = FormatChanges(Duration, true, true);
            finalCosts = FormatChanges(Duration, false, true);
        }

        public static bool Supports(PracticeSet set) => set != PracticeSet.Street && set != PracticeSet.Track;

        public int TicksAt(int axis) => ticks[axis];
        public int GainSlot(int axis) => slots[axis];
        public static float LatchTime(int index) => LatchBeats[index];
        public float ContactTime(int axis) => LandingTime + GainSlot(axis) * ObjectSpacing;

        public int DisplayTicks(int axis, float time, bool reducedMotion)
        {
            int value = ticks[axis];
            if (value <= 0) return time >= CostTime ? value : 0;
            if (time < GainTime) return 0;
            if (reducedMotion) return value;
            float contact = ContactTime(axis);
            if (time + .00002f >= contact) return value;
            float age = time - GainTime;
            float progress = .74f * Window(age, .045f, .32f) + .18f * Window(time, GainTime + .31f, contact);
            return (int)Math.Min(value, 1 + Math.Floor((value - 1d) * progress));
        }

        public string Changes(float time, bool gains, bool reducedMotion)
        {
            if (!gains) return time >= CostTime ? finalCosts : "";
            if (time < GainTime) return "";
            return reducedMotion || time >= CostTime ? finalGains : FormatChanges(time, true, false);
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

            float pose = age < 0 ? Math.Min(time, HoldTime) : time;
            float first = Crest(pose, 1.02f, LatchBeats[0], 1.55f);
            float second = Crest(pose, 1.68f, LatchBeats[1], 2.08f);
            float third = Crest(pose, 2.07f, LatchBeats[2], 2.48f);
            float brace = Window(pose, 2.30f, HoldTime);
            float charge = age < 0 ? Math.Min(1, .08f + .69f * Window(pose, 1.10f, 2.55f) +
                .22f * first + .26f * second + .29f * third) : 0;
            float compression = age < 0 ? .045f * first + .072f * second + .10f * third + .13f * brace :
                .13f * (1 - Window(age, 0, .060f)) - .048f * Crest(age, .035f, .104f, .33f);
            float tilt = age < 0 ? -3.5f * first + 6 * second - 8.5f * third - 3 * brace :
                -3 * (1 - Window(age, 0, .061f)) + 11 * Crest(age, .025f, .11f, .31f) - 4 * Crest(age, .23f, .33f, .56f);
            float lid = .94f * Window(age, 0, .13f) + .06f * Window(age, .13f, .55f)
                + .18f * Crest(age, .065f, .18f, .44f) - .06f * Crest(age, .33f, .45f, .71f);
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
