using System;
using System.Linq;
using UnityEngine;

namespace Horizon.Game
{
    public sealed partial class RareMoment
    {
        public int futureDay;
        public string futureName, futureCardId, futureNodeId;
        public bool futureFromWorld, futureVerified;
        public string parallelCardId;
        public ResourceDelta parallelState;
    }

    public static partial class ExperienceContent
    {
        public static void GroundRareMoment(RareMoment moment, GameSession life)
        {
            if (moment == null || life == null || moment.type > 1) return;
            if (moment.type == 0)
            {
                WorldEventSpec spec = life.CatalogVersion >= 3 && (life.RunNumber >= 3 || life.Deadline >= 30) ?
                    WorldEvents.ForCatalog(life.CatalogVersion).FirstOrDefault(e => e.Day > life.Day && e.Day <= life.Deadline &&
                        WorldEvents.Occurs(life.WorldSeed, e.Day, e.Chance)) : null;
                PendingEcho echo = life.Pending.Where(e => e.dueDay > life.Day).OrderBy(e => e.dueDay).FirstOrDefault();
                if (spec != null && (echo == null || spec.Day < echo.dueDay))
                { moment.futureDay = spec.Day; moment.futureName = spec.Name; moment.futureCardId = spec.ReplacementId;
                    moment.futureFromWorld = true; }
                else if (echo != null)
                { moment.futureDay = echo.dueDay; moment.futureName = echo.echoName; moment.futureNodeId = echo.nodeId;
                    moment.futureCardId = echo.cardId; }
                if (moment.futureDay > 0)
                {
                    moment.description = "裂缝里，短暂看见了 D" + moment.futureDay + "。\n「" + moment.futureName +
                        "」\n\n" + (moment.futureFromWorld ? "这个世界短暂泄漏了一个未来事件。" : "这道信号来自你已经种下的回声。") +
                        "\n它没有替你选择；到了那天，再看看它。";
                    return;
                }
                // A life with no planted or occurring future signal receives a
                // real conditional parallel, rather than a fabricated leak.
                moment.type = 1; moment.title = "另一个我";
            }
            RunSnapshot saved = JsonUtility.FromJson<RunSnapshot>(JsonUtility.ToJson(life.Snapshot()));
            GameSession branch = GameSession.ForkForSimulation(saved, life.Deadline);
            while (branch.HasPredictionReview) branch.MarkPredictionReviewed();
            if (branch.CanPredict) branch.SkipPrediction();
            CardSpec choice = branch.Hand.Where(branch.CanPlay).OrderBy(c =>
                life.Actions.Count(a => a.cardId == c.Id)).ThenBy(c => (int)c.Kind).FirstOrDefault();
            if (choice == null) return;
            int target = Math.Min(life.Deadline, life.Day + 3);
            branch.Choose(choice.Id);
            while (branch.Day < target)
            {
                if (branch.NeedsStation) branch.VisitStation(); branch.Advance();
                while (branch.HasPredictionReview) branch.MarkPredictionReviewed();
                if (branch.CanPredict) branch.SkipPrediction(); branch.Choose(branch.Hand[2].Id);
            }
            moment.futureDay = target; moment.parallelCardId = choice.Id;
            moment.parallelState = new ResourceDelta(branch.Energy, branch.Mood, branch.Insight,
                branch.Relation, branch.Money, branch.Ability);
            moment.description = "另一个你，在 D" + life.Day + " 选了「" + choice.Name + "」。\n随后每天先照顾自己，走到了 D" + target +
                " 夜。\n精力 " + branch.Energy + " · 心情 " + branch.Mood + " · 能力 " + branch.Ability +
                "\n\n这是从今天出发的一条可能分支。\n你自己的下一张牌，仍然在手里。";
        }

        public static bool VerifyRareFuture(RareMoment moment, GameSession life)
        {
            if (moment == null || moment.type != 0 || moment.futureVerified || moment.runNumber != life.RunNumber ||
                moment.futureDay < 1 || life.Day < moment.futureDay) return false;
            CausalNode arrived = moment.futureFromWorld ? life.CausalNodes.Find(n => n.type == CausalNodeKind.World &&
                n.day == moment.futureDay && n.label == moment.futureName && n.resolved) :
                life.CausalNodes.Find(n => n.id == moment.futureNodeId && n.resolved);
            if (arrived == null) return false;
            moment.futureVerified = true; moment.futureNodeId = arrived.id;
            return true;
        }
    }
}
