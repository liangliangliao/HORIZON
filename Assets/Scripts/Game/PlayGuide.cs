using System;
using System.Collections.Generic;
using System.Linq;

namespace Horizon.Game
{
    [Serializable]
    public sealed class PlayGuideProgress
    {
        public bool completed;
        public int run, page;
        public string preparedCardId;
        public int preparedRun, preparedDay;
        public bool imagineFromBoard;

        public void Repair()
        {
            page = Math.Max(0, Math.Min(1, page));
            if (run < 0) run = 0;
            if (preparedRun < 1 || preparedDay < 1 || preparedDay > 30)
            { preparedCardId = null; preparedRun = preparedDay = 0; }
        }
    }

    // Guidance reads the simulation. Opening help never advances time, reveals
    // hidden causes, awards resources, or makes a decision for the player.
    public static class PlayGuide
    {
        public static string Today(GameSession life)
        {
            if (life.InExecutionMode) return "决定已经完成 · 现在处理下一步";
            if (life.UsesMasterRules && life.Master.awaitingComeback) return "受挫以后 · 今天仍能重新开始";
            if (life.Energy <= 3 || life.Mood <= 3) return "先照顾状态 · 给下一次行动留出余力";
            if (life.Day >= life.Deadline - 2) return "临近抵达 · 留意回声能否赶上截止日";
            if (life.Day == 1) return "今天选一张 · 看看现在与未来的交换";
            return "今天的选择 · 会继续影响后面的日子";
        }

        public static string Next(GameSession life)
        {
            if (life.InExecutionMode) return "打开「继续执行」，完成步骤；你仍可以主动解锁。";
            if (life.UsesMasterRules && life.Master.awaitingComeback)
                return "选择恢复或成长来接续因果链。也可以先预演失败后的下一步。";
            List<PendingEcho> planned = Scheduled(life);
            string next = planned.Count == 0 ? "点开一张牌，对照今天付出与未来抵达日。" :
                "D" + planned[0].dueDay + " · 「" + planned[0].cardName + "」的回声将回来。";
            return next + "\n" + ProductExperience.NextMove(life);
        }

        public static List<PendingEcho> Scheduled(GameSession life)
        {
            // Only dates planted by the player's own action are public here.
            // A second-order echo can contain a still-hidden variable.
            return life.Pending.Where(e => e.depth == 1 && life.Actions.Any(a =>
                a.nodeId == e.parentNodeId && a.echoDay == e.dueDay && a.cardId == e.cardId))
                .OrderBy(e => e.dueDay).ThenBy(e => e.sourceDay).ToList();
        }

        public static string Family(CardSpec card)
        {
            CardFamily f = card.Traits.families;
            if ((f & CardFamily.Trigger) != 0) return "行动提示";
            if ((f & CardFamily.Imagination) != 0) return "想象演练";
            if ((f & CardFamily.Commitment) != 0) return "承诺与执行";
            if ((f & CardFamily.Recovery) != 0) return "恢复与再战";
            if ((f & CardFamily.Worldview) != 0) return "思考视角";
            if ((f & CardFamily.Exploration) != 0) return "探索路线";
            if ((f & CardFamily.Risk) != 0) return "承担风险";
            if (card.GivesSupport) return "关系与支援";
            if ((f & CardFamily.Knowledge) != 0) return "知识与成长";
            return card.Kind == CardKind.Growth ? "积累未来" : card.Kind == CardKind.Recovery ? "照顾自己" : "即时快乐";
        }
    }
}
