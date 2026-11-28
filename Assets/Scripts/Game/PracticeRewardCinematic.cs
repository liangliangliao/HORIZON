using System;
using System.Collections.Generic;

namespace Horizon.Game
{
    public sealed class PracticeRewardCinematic
    {
        public const float ReleaseTime = 4.80f;
        public const float PrizeTime = ReleaseTime + EnergyRelease.PrizeDelay;
        public const float GainTime = PrizeTime + .40f;
        public const float HoldTime = ReleaseTime - .19f;
        public const float LandingTime = PrizeTime + 1.48f;
        public const float CostTime = PrizeTime + 2.18f;
        public const float Duration = PrizeTime + 3.55f;
        public const float ObjectSpacing = .118f;
        private static readonly float[] LatchBeats = { 1.25f, 2.51f, 3.67f };
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
            float readAt=GainTime+GainSlot(axis)*ObjectSpacing;
            if(time<readAt) return 0;
            float contact = ContactTime(axis);
            if (time + .00002f >= contact) return value;
            float age = time - readAt;
            float progress = .74f * Window(age, .045f, .32f) + .18f * Window(time, readAt + .31f, contact);
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
            float first = ImmersiveMotion.Crest(pose, .77f, LatchBeats[0], 1.93f);
            float second = ImmersiveMotion.Crest(pose, 2.02f, LatchBeats[1], 3.11f);
            float third = ImmersiveMotion.Crest(pose, 3.16f, LatchBeats[2], 4.19f);
            float brace = Window(pose, 4.00f, HoldTime);
            float charge = age < 0 ? Math.Min(1, .016f + .63f * Window(pose, .97f, HoldTime) +
                .13f * first + .28f * second + .50f * third) : 0;
            float lidStrain = age < 0 ? .065f * first + .13f * second + .20f * third + .10f * brace :
                .10f * (1 - Window(age, 0, .095f));
            float compression = age < 0 ? .045f * first + .072f * second + .10f * third + .13f * brace :
                .13f * (1 - Window(age, 0, .060f)) - .048f * ImmersiveMotion.Crest(age, .035f, .104f, .33f);
            float tilt = age < 0 ? -3.5f * first + 6 * second - 8.5f * third - 3 * brace :
                -3 * (1 - Window(age, 0, .061f)) + 11 * ImmersiveMotion.Crest(age, .025f, .11f, .31f) -
                4 * ImmersiveMotion.Crest(age, .23f, .33f, .56f);
            float lid = .94f * Window(age, 0, .13f) + .06f * Window(age, .13f, .55f)
                + .18f * ImmersiveMotion.Crest(age, .065f, .18f, .44f) - .06f * ImmersiveMotion.Crest(age, .33f, .45f, .71f);
            return new PracticeRewardCinematicFrame(time, age, charge, compression, tilt, lid,
                EnergyRelease.SampleScenic(age, strength), lidStrain);
        }

        private static float Window(float time, float from, float to) => ImmersiveMotion.Smooth((time - from) / (to - from));
    }

    public readonly struct PracticeRewardCinematicFrame
    {
        public readonly float Time, Age, Charge, Compression, Tilt, Lid, LidStrain;
        public readonly EnergyRelease Release;

        public PracticeRewardCinematicFrame(float time, float age, float charge, float compression, float tilt,
            float lid, EnergyRelease release, float lidStrain = 0)
        {
            Time = time;
            Age = age;
            Charge = charge;
            Compression = compression;
            Tilt = tilt;
            Lid = lid;
            LidStrain = lidStrain;
            Release = release;
        }
    }
}
