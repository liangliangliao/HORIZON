using System;
using System.Collections.Generic;
using System.Linq;
using Horizon.Game;

namespace Horizon.Game
{
    [Serializable]
    public sealed class ImaginationCalibration
    {
        public string imaginationId, goal, expectedCardId, actualCardId, actualNodeId, failureNodeId, recoveryNodeId, expectedRecovery, actualRecovery, difference;
        public int run, plannedDay, actualDay, failureDay, recoveryDay;
        public bool actionObserved, actionMatched, recoveryObserved, recoveryMatched;
        public ImaginationCalibration Copy() { return (ImaginationCalibration)MemberwiseClone(); }

        // Compare only evidence that actually happened. An imagined failure is never
        // imposed on the simulation, and a simulated action is never a reality node.
        public void Observe(GameSession life)
        {
            if (life == null || life.RunNumber != run) return;
            ActionRecord first = life.Actions.FirstOrDefault(a => a.day == plannedDay);
            if (first != null)
            {
                actionObserved = true; actualCardId = first.cardId; actualNodeId = first.nodeId; actualDay = first.day;
                actionMatched = expectedCardId == first.cardId;
                difference = actionMatched ? "原定行动已经发生；继续观察困难后的选择。" :
                    "原本预演「" + CardCatalog.FindById(expectedCardId)?.Name + "」，实际选择了「" + first.cardName + "」。先检查状态、成本或目标是否改变。";
            }
            DomainEvent failure = life.Master?.events.FirstOrDefault(e => e.kind == DomainEventKind.FailAndAgain && e.day >= plannedDay);
            if (failure == null) return;
            failureNodeId = failure.nodeId; failureDay = failure.day;
            ActionRecord after = life.Actions.FirstOrDefault(a => a.day >= failure.day && a.day >= plannedDay &&
                ComesAfter(life, a.nodeId, failure.nodeId));
            if (after == null) return;
            recoveryObserved = true; recoveryDay = after.day; recoveryNodeId = after.nodeId; actualRecovery = after.cardName;
            recoveryMatched = ImaginationEngine.Matches(new FutureMemory { actionKey = expectedRecovery }, CardCatalog.FindById(after.cardId));
            string recoveryDifference = recoveryMatched ? "受挫后的行动与预演一致。你练过的恢复路径，在游戏里发生了一次。" :
                "遇到困难后，预演选择「" + RecoveryName(expectedRecovery) + "」，实际选择「" + after.cardName + "」。下次预演可以把这次变化加入路径。";
            difference = actionMatched ? recoveryDifference : difference + "\n" + recoveryDifference;
        }
        private static bool ComesAfter(GameSession life, string action, string failure)
        { return life.CausalNodes.FindIndex(n => n.id == action) > life.CausalNodes.FindIndex(n => n.id == failure); }
        public static string RecoveryName(string key)
        { return key == "Rest" ? "先恢复精力" : key == "AskHelp" ? "请求帮助" : key == "LowerTarget" ? "缩小一步" : "改变方法"; }
    }
}

namespace Horizon
{
    public sealed partial class ArchiveData
    {
        public List<ImaginationCalibration> imaginationComparisons = new List<ImaginationCalibration>();
        public void TrackImagination(ImagineRun imagined, GameSession life, string cardId)
        {
            if (life == null || imagined == null || imagined.phase != ImaginePhase.Complete || CardCatalog.FindById(cardId) == null ||
                imaginationComparisons.Any(c => c.imaginationId == imagined.id)) return;
            imaginationComparisons.Add(new ImaginationCalibration { imaginationId = imagined.id, goal = imagined.goal,
                run = life.RunNumber, plannedDay = life.Day, expectedCardId = cardId, expectedRecovery = imagined.memories.Last().actionKey,
                difference = "预演已经完成。实际行动还没有发生。" });
        }
        public void ObserveImagination(GameSession life)
        {
            foreach (ImaginationCalibration comparison in imaginationComparisons.Where(c => c.run == life.RunNumber))
            {
                comparison.Observe(life);
                if (comparison.actionObserved) me.ObserveCalibration(comparison.imaginationId + ":action", comparison.actionMatched);
                if (comparison.recoveryObserved) me.ObserveCalibration(comparison.imaginationId + ":recovery", comparison.recoveryMatched);
            }
        }
    }
}
