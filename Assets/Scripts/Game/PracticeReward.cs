using System;
using System.Collections.Generic;
using System.Globalization;

namespace Horizon.Game
{
    [Serializable]
    public sealed class PracticeTake
    {
        public string cardId;
        public int day, difficulty;
        // Absent in older saved performances, which retain their original allocation on replay.
        public bool settleOnDueDay;
        public string routes = "";

        public bool Complete => difficulty >= 1 && difficulty <= 3 && routes != null && routes.Length == difficulty;
        public bool Valid => difficulty >= 1 && difficulty <= 3 && routes != null && routes.Length <= difficulty &&
            Array.TrueForAll(routes.ToCharArray(), c => c == 'A' || c == 'B');
        public PracticeTake Copy() => (PracticeTake)MemberwiseClone();
    }

    public static class PracticeReward
    {
        public const int Unit = 10000;
        public const int MaxBudget = 8000;
        public static readonly string[] Names = { "精力","心情","专注","金钱","关系","能力" };

        public static string Format(int ticks) => (ticks / (decimal)Unit).ToString("0.0000", CultureInfo.InvariantCulture);
        public static int[] Ticks(ResourceDelta d)
        {
            if (d == null) return new int[6];
            if (d.ticks != null && d.ticks.Length == 6) return (int[])d.ticks.Clone();
            int[] whole = ResourceMath.Axes(d);
            for (int i = 0; i < 6; i++) whole[i] *= Unit;
            return whole;
        }
        public static ResourceDelta FromTicks(int[] t)
        {
            return new ResourceDelta(t[0]/Unit, t[1]/Unit, t[2]/Unit, t[4]/Unit, t[3]/Unit, t[5]/Unit) { ticks = t };
        }
        public static string Label(ResourceDelta d, bool directions = false)
        {
            var parts = new List<string>();
            int[] values = Ticks(d);
            for (int i = 0; i < values.Length; i++)
                if (values[i] != 0) parts.Add(Names[i] + (directions ? (values[i] > 0 ? "↑" : "↓") :
                    (values[i] > 0 ? " +" : " ") + Format(values[i])));
            return parts.Count == 0 ? "状态保持不变" : string.Join("　", parts.ToArray());
        }
        public static ResourceDelta DeferredWeights(PracticeTake take, CardSpec card, int deadline)
        {
            if (card.Delay == 0) return null;
            return take.settleOnDueDay || take.day + card.Delay <= deadline ? card.Later : null;
        }

        public static string Timing(CardSpec card, ResourceDelta immediate)
        {
            if (card.Delay == 0) return "当日结清";
            if (Array.Exists(Ticks(card.Later), v => v < 0)) return "后续有代价";
            return Array.Exists(Ticks(immediate), v => v > 0) ? "分两次生效" : "成果在以后";
        }

        public static string EchoPreview(CardSpec card, int day, int deadline)
        {
            if (card.Delay == 0) return "本次当日结清";
            int due = day + card.Delay;
            if (due > deadline) return "第 " + due + " 天超出本局，后续不计入本局余额";
            return "第 " + due + " 天 · " + card.EchoName + "\n" + Label(card.Later, true);
        }

        public static string EchoReceipt(ActionRecord action, int deadline)
        {
            if (action.echoDay <= 0) return "当日结清";
            if (action.echoDay > deadline) return "第 " + action.echoDay + " 天超出本局，后续不计入本局余额";
            return "待结算 · 第 " + action.echoDay + " 天\n" + Label(action.later);
        }

        public static int Ceiling(int difficulty) => difficulty == 1 ? 2200 : difficulty == 2 ? 4800 : MaxBudget;

        // Roll once per run, day and card. Changing or replaying a route cannot reroll the percentile.
        public static int Draw(int seed, int run, PracticeTake take)
        {
            if (!take.Valid || !take.Complete) throw new InvalidOperationException("Unfinished practice.");
            uint hash = 2166136261u;
            unchecked
            {
                foreach (char c in seed + "/" + run + "/" + take.day + "/" + take.cardId) hash = (hash ^ c) * 16777619u;
                hash ^= hash >> 16; hash *= 0x7feb352du; hash ^= hash >> 15;
            }
            int ceiling = Ceiling(take.difficulty);
            int roll = (int)(hash % 10000);
            // Common 70%, strong 25%, exceptional 5%. Ranges meet at 55% and 85% of the band.
            int percentile = roll < 7000 ? 2000 + roll / 2 : roll < 9500 ?
                5500 + (roll - 7000) * 3000 / 2500 : 8500 + (roll - 9500) * 1500 / 499;
            return Math.Max(1, ceiling * percentile / Unit);
        }
        public static void Allocate(PracticeTake take, int budget, ResourceDelta immediate, ResourceDelta delayed, out ResourceDelta now, out ResourceDelta later)
        {
            int[] a = ResourceMath.Axes(immediate), b = ResourceMath.Axes(delayed ?? new ResourceDelta());
            int total = 0;
            for (int i=0;i<6;i++) total += Math.Abs(a[i]) + Math.Abs(b[i]);
            int[] x = new int[6], y = new int[6];
            if (total > 0)
            {
                // Distribute exact integer ticks including rounding residuals, conserving the whole budget.
                int carried = 0;
                for (int i=0;i<12;i++)
                {
                    int weight = i<6 ? a[i] : b[i-6];
                    carried += Math.Abs(weight) * budget;
                    (i<6 ? x : y)[i%6] = Math.Sign(weight) * (carried / total);
                    carried %= total;
                }
                // Legacy performances borrowed a quarter of a future gain for the reveal.
                if (!take.settleOnDueDay && !Array.Exists(x, value => value > 0))
                {
                    int axis = Array.FindIndex(y, value => value > 1);
                    if (axis >= 0) { x[axis] += y[axis] / 4; y[axis] -= y[axis] / 4; }
                }
            }
            // A small part of the immediate gain follows the skill actually used.
            // Reading a map trains focus, while walking the detour keeps that effort in energy.
            int donor=0;
            for(int i=1;i<6;i++) if(x[i]>x[donor]) donor=i;
            int share=x[donor]>0 ? x[donor]/(5*take.difficulty) : 0;
            var film=PracticeFilms.For(take.cardId);
            for(int i=0;i<take.routes.Length && share>0;i++)
            {
                var move=film.Move(i,take.routes[i]=='B');
                int axis=move==PracticeMove.Talk || move==PracticeMove.Listen ? 4 :
                    move==PracticeMove.Walk || move==PracticeMove.Carry ? 0 :
                    move==PracticeMove.Rest ? 1 : move==PracticeMove.Assemble ? 5 : 2;
                if(axis==donor || x[axis]<0) continue;
                x[donor]-=share; x[axis]+=share;
            }
            now = FromTicks(x); later = FromTicks(y);
        }
    }

    public sealed partial class GameSession
    {
        private int[] fractions;
        public PracticeTake Practice { get; private set; }
        public void BeginPractice(CardSpec card, int difficulty)
        {
            if (!CanPlay(card) || difficulty < 1 || difficulty > 3) throw new InvalidOperationException("Practice unavailable.");
            Practice = new PracticeTake { day=Day, cardId=card.Id, difficulty=difficulty, settleOnDueDay=true };
        }
        public void PickPracticeRoute(bool second)
        {
            if (Practice == null || !Practice.Valid || Practice.Complete) throw new InvalidOperationException("No open obstacle.");
            Practice.routes += second ? "B" : "A";
        }
        public void AbandonPractice() { Practice = null; }
        public int PracticeBudget => PracticeReward.Draw(WorldSeed, RunNumber, Practice);
        public bool HasPracticeBalances => fractions != null;
        public int[] CopyFractions() => (int[])fractions?.Clone();
        public int AxisTicks(int axis) => PracticeReward.Ticks(Values())[axis];
        public string Balance(int axis) => PracticeReward.Format(AxisTicks(axis));

        public ResourceDelta PreviewPractice(CardSpec card)
        {
            if (Practice == null || Practice.cardId != card.Id || Practice.day != Day) throw new InvalidOperationException("Wrong practice.");
            PracticeReward.Allocate(Practice, PracticeBudget, ImmediateEffect(card), PracticeReward.DeferredWeights(Practice,card,Deadline), out var now, out _);
            int[] current = PracticeReward.Ticks(Values()), delta = PracticeReward.Ticks(now);
            for (int i=0;i<6;i++) delta[i] = Math.Min(ResourceCap*PracticeReward.Unit,Math.Max(0,current[i]+delta[i])) - current[i];
            return PracticeReward.FromTicks(delta);
        }
        public ActionRecord FinishPractice()
        {
            if (Practice == null || !Practice.Valid || !Practice.Complete || Practice.day != Day) throw new InvalidOperationException("Practice unfinished.");
            return ChooseCore(Practice.cardId, Practice);
        }
    }
}
