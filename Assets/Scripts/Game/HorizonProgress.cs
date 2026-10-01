using System;
using System.Linq;

namespace Horizon.Game
{
    [Flags]
    public enum LifeLesson { None = 0, Returns = 1, Uncertainty = 2, Prediction = 4, Preparation = 8, Identity = 16 }

    // One observation contract for the board, focus, chapters and teaching.
    // Time scale and clarity describe information, never change a life's rules.
    public sealed class HorizonProgress
    {
        public readonly int Stage, Calibrations;
        public int Level { get { return Stage >= 6 ? 3 : Stage >= 2 ? 2 : 1; } }
        public int Days { get { return Stage >= 7 ? 30 : Stage >= 5 ? 7 : Stage >= 3 ? 3 : Stage >= 2 ? 1 : 0; } }
        public int VisibleTypes { get { return Stage >= 6 ? int.MaxValue : Stage >= 4 ? 2 : Stage >= 2 ? 1 : 0; } }
        public bool SecondOrder { get { return Stage >= 4 || Calibrations >= 10; } }
        public bool Probability { get { return Stage >= 5; } }
        public bool Compare { get { return Stage >= 6; } }
        public bool ThirtyDays { get { return Stage >= 7; } }
        public bool Direction { get { return Calibrations >= 1; } }
        public bool Strength { get { return Calibrations >= 3; } }
        public bool Origin { get { return Calibrations >= 10; } }

        public HorizonProgress(int stage, int calibrations)
        { Stage = Math.Max(1, Math.Min(7, stage)); Calibrations = Math.Max(0, calibrations); }

        public static int LifeStage(GameSession life)
        {
            if (life == null) return 1;
            if (life.Deadline == 30) return 7;
            if (life.HorizonLevel >= 3) return 6;
            int stage = life.RunNumber >= 3 ? 4 : life.RunNumber >= 2 ? 3 : 1;
            if (life.CausalNodes.Any(n => n.type == CausalNodeKind.World && n.resolved)) stage = Math.Max(stage, 5);
            return stage;
        }

        public static HorizonProgress Resolve(GameSession life, JourneyProgress journey, int calibrations = 0)
        { return new HorizonProgress(Math.Max(LifeStage(life), Math.Max(journey?.Chapter ?? 1,
            journey?.learnedStage ?? 1)), calibrations); }

        public bool Sees(PendingEcho echo, int index, int today)
        { return echo != null && index < VisibleTypes && echo.dueDay <= today + Days; }

        public string Clue(PendingEcho echo, int index, int today)
        {
            if (echo == null) return "尚未种下的未来";
            if (!Sees(echo, index, today)) return echo.kind == CardKind.Temptation && echo.depth == 1 ?
                "一处微弱的" + ObservationDesign.EchoType(echo) : "尚未看清的回声";
            if (echo.depth >= 2 && !SecondOrder) return "一次尚未看清的变化";
            string clue = echo.depth >= 2 ? Origin ? echo.echoName + " · 源自 D" + echo.sourceDay :
                "它会改变后来的选择" : Level >= 3 ? echo.echoName : ObservationDesign.EchoType(echo);
            if (!Direction || echo.delta == null) return clue;
            int[] effects = { echo.delta.energy, echo.delta.mood, echo.delta.insight,
                echo.delta.relation, echo.delta.money, echo.delta.ability };
            string[] names = { "精力", "心情", "洞察", "关系", "金钱", "能力" };
            int axis = -1;
            for (int i = 0; i < effects.Length; i++)
                if (effects[i] != 0 && (axis < 0 || Math.Abs(effects[i]) > Math.Abs(effects[axis]))) axis = i;
            return axis < 0 ? clue : clue + " · " + names[axis] +
                (Strength && Math.Abs(effects[axis]) >= 2 ? "明显" : "") + (effects[axis] > 0 ? "↑" : "↓");
        }

        public string Caption { get { return Days == 0 ? "未来 · 先看今天留下了什么" :
            "视野 " + Days + " 天 · " + Clarity; } }
        public string Clarity { get { return Origin ? "二阶来路" : Strength ? "影响强度" : Direction ? "变化方向" : "节点类型"; } }
        public string Next { get { return Stage >= 7 ? "三十天的路，仍由每一天的选择写成。" :
            "下一层：" + JourneyProgress.Name(Stage + 1) + "。继续经历，或在另一天回来；已有理解会保留。"; } }

        public static string LessonText(LifeLesson lesson)
        {
            switch (lesson) {
                case LifeLesson.Returns: return "行为会回来：这份变化来自你之前亲手选的牌。";
                case LifeLesson.Uncertainty: return "未来有不确定：种下的回声会按时回来，环境和后续选择仍会变化。";
                case LifeLesson.Prediction: return "你可以预测：把自己的判断与实际发生的事放在一起，再看看原因。";
                case LifeLesson.Preparation: return "你可以提前准备：让成长回来，也给状态和支援留位置。";
                case LifeLesson.Identity: return "不同选择会制造不同的自己：他记得的，是你亲手走过的路。";
                default: return "今天先选一张，看看它会去哪一天。";
            }
        }
    }
}
