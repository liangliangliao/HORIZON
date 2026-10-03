using System;
using System.Collections.Generic;
using System.Linq;

namespace Horizon.Game
{
    public enum ChapterOutcome { InProgress, Arrived, WindowClosed }

    [Serializable]
    public sealed class StoryChapter
    {
        public string id, originNode, preparationNode, failureNode, recoveryNode, returnNode, outcomeNode, response, route;
        public int startDay, encounterDay, failureDay, windowDay, targetAbility = 6;
        public bool setbackOccurred;
        public ChapterOutcome outcome;
        public string Title { get { return id == "tomorrow" ? "在机会关闭之前" : id == "perfection" ? "让不完美的作品出发" : "穿过不确定"; } }
        public string BossName { get { return id == "tomorrow" ? "明天再说" : id == "perfection" ? "永远准备" : "不确定"; } }
        public string Goal { get { return id == "tomorrow" ? "D" + windowDay + " 前接住一次邀约" : id == "perfection" ? "D" + windowDay + " 交出一份作品" : "D" + windowDay + " 带着作品参加面试"; } }
        public StoryChapter Copy() { return (StoryChapter)MemberwiseClone(); }
        public void Validate(int deadline)
        {
            if (!new[] { "uncertainty", "tomorrow", "perfection" }.Contains(id) || startDay < 1 ||
                encounterDay != startDay + 2 || failureDay < encounterDay + 1 || windowDay > deadline || windowDay < failureDay + 2 ||
                targetAbility < 4 || targetAbility > 8 || !Enum.IsDefined(typeof(ChapterOutcome), outcome) || string.IsNullOrEmpty(originNode) ||
                !string.IsNullOrEmpty(response) && !new[] { "step", "help", "delay" }.Contains(response))
                throw new ArgumentException("Invalid story chapter.");
        }
    }

    public sealed partial class GameSession
    {
        public bool ChapterNeedsPreparation { get { return Master?.chapter != null && Master.chapter.outcome == ChapterOutcome.InProgress &&
            Day >= Master.chapter.encounterDay && string.IsNullOrEmpty(Master.chapter.response); } }
        public bool ChapterNeedsBoss { get { return Master?.chapter != null && Master.chapter.outcome == ChapterOutcome.InProgress &&
            Day >= Master.chapter.windowDay && !ChapterNeedsPreparation; } }
        public bool ChapterNeedsChoice { get { return ChapterNeedsPreparation || ChapterNeedsBoss; } }

        public void BeginChapter(string id)
        {
            RequireMasterChoice();
            if (Master.chapter != null || CatalogVersion < 9 || InExecutionMode || Deadline - Day < 7 ||
                !new[] { "uncertainty", "tomorrow", "perfection" }.Contains(id)) throw new InvalidOperationException("Start a chapter before its opportunity window.");
            CausalNode origin = MasterNode(CausalNodeKind.Opportunity, "未来目标 · " + id);
            Master.chapter = new StoryChapter { id = id, originNode = origin.id, startDay = Day, encounterDay = Day + 2,
                failureDay = Math.Min(Day + 4, (id == "tomorrow" ? Deadline - 2 : Deadline) - 2), windowDay = id == "tomorrow" ? Deadline - 2 : Deadline, targetAbility = Math.Min(8, Math.Max(6, Ability + 2)) };
            origin.label = Master.chapter.Goal; Command("chapter", id);
        }

        public ResourceDelta ChapterPreparationCost(string response)
        {
            return response == "step" ? new ResourceDelta(insight: -1) : response == "help" ? new ResourceDelta(money: -1, relation: 1) : new ResourceDelta();
        }
        public bool CanPrepareChapter(string response)
        {
            return ChapterNeedsPreparation && (response == "step" && Insight >= 1 || response == "help" && Money >= 1 || response == "delay");
        }
        public bool PrepareChapter(string response)
        {
            RequireMasterChoice(); if (!CanPrepareChapter(response)) return false;
            StoryChapter chapter = Master.chapter; ResourceDelta before = Values(); Apply(ChapterPreparationCost(response));
            string text = response == "step" ? "只准备一个能开始的小步骤" : response == "help" ? "预约朋友，准备反馈与提醒" : "再次比较，准备留到明天";
            CausalNode node = MasterNode(CausalNodeKind.Decision, text, chapter.originNode, Difference(before));
            chapter.response = response; chapter.preparationNode = node.id; Command("chapter-prepare", response); RefreshEngine(); RecordResourceSample();
            return true;
        }

        private void ChapterAfterAdvance()
        {
            StoryChapter chapter = Master?.chapter;
            if (chapter == null || chapter.outcome != ChapterOutcome.InProgress || chapter.setbackOccurred || Day < chapter.failureDay) return;
            ResourceDelta before = Values();
            Apply(chapter.response == "help" ? new ResourceDelta(-1) : chapter.response == "step" ? new ResourceDelta(-1, -1) : new ResourceDelta(-2, -2));
            CausalNode node = MasterNode(CausalNodeKind.Thought, "第一次反馈：作品还需要修改", string.IsNullOrEmpty(chapter.preparationNode) ? chapter.originNode : chapter.preparationNode, Difference(before));
            ActionRecord attempt = Actions.LastOrDefault(a => a.kind == CardKind.Growth);
            if (attempt != null) CausalGraph.Link(node, attempt.nodeId);
            chapter.setbackOccurred = true; chapter.failureNode = node.id;
            Master.resilienceChain = Math.Min(10, Master.resilienceChain + 1); Master.awaitingComeback = true; Master.lastFailureNode = node.id;
            ObservePattern("setback", false, node);
            Emit(DomainEventKind.FailAndAgain, node, "第一次没有做好", "准备减轻了代价。下一步可以恢复、求助，然后再带着修改后的作品出发。");
            RefreshEngine(); RecordResourceSample();
        }

        private void ChapterAfterChoice(ActionRecord action, CardSpec card)
        {
            StoryChapter chapter = Master?.chapter; if (chapter == null || chapter.outcome != ChapterOutcome.InProgress) return;
            CausalNode node = CausalNodes.Find(n => n.id == action.nodeId);
            if (!string.IsNullOrEmpty(chapter.preparationNode)) CausalGraph.Link(node, chapter.preparationNode);
            if (!chapter.setbackOccurred) return;
            if (string.IsNullOrEmpty(chapter.recoveryNode) && (card.Kind == CardKind.Recovery || card.GivesSupport))
            { chapter.recoveryNode = node.id; CausalGraph.Link(node, chapter.failureNode); }
            else if (!string.IsNullOrEmpty(chapter.recoveryNode) && string.IsNullOrEmpty(chapter.returnNode) && card.Kind == CardKind.Growth && node.id != chapter.recoveryNode)
            {
                chapter.returnNode = node.id; CausalGraph.Link(node, chapter.recoveryNode);
                Master.awaitingComeback = false; ObservePattern("setback", true, node);
                Master.insightPoints += 2; Emit(DomainEventKind.Comeback, node, "FAIL & AGAIN ×" + Master.resilienceChain,
                    "D" + chapter.failureDay + " 的挫折没有成为终点。你实际恢复过，又重新开始了。");
            }
        }

        public string ChapterRouteCondition(string route)
        {
            StoryChapter c = Master?.chapter; if (c == null) return "先选择一个目标";
            if (route == "leave") return "允许放下这次机会，时间线会保留真实结果";
            string condition = route == "help" ? "能力≥4 · 关系≥5 · 金钱≥1 · 精力≥2" :
                route == "draft" ? "能力≥4 · 精力≥2" : route == "polish" ? "能力≥8 · 精力≥3" : "能力≥" + c.targetAbility + " · 精力≥3";
            return condition + " · 受挫后恢复并重新行动";
        }
        public bool CanResolveChapter(string route)
        {
            StoryChapter c = Master?.chapter; if (!ChapterNeedsBoss || c == null) return false;
            if (route == "leave") return true;
            if (string.IsNullOrEmpty(c.returnNode)) return false;
            if (route == "help") return c.id != "perfection" && Ability >= 4 && Relation >= 5 && Money >= 1 && Energy >= 2;
            if (route == "draft") return c.id == "perfection" && Ability >= 4 && Energy >= 2;
            if (route == "polish") return c.id == "perfection" && Ability >= 8 && Energy >= 3;
            return route == "act" && c.id != "perfection" && Ability >= c.targetAbility && Energy >= 3;
        }
        public bool ResolveChapter(string route)
        {
            RequireMasterChoice(); if (!CanResolveChapter(route)) return false;
            StoryChapter c = Master.chapter; ResourceDelta before = Values();
            if (route != "leave") Apply(route == "help" ? new ResourceDelta(-1, money: -1) : new ResourceDelta(route == "polish" ? -2 : -1));
            CausalNode node = MasterNode(CausalNodeKind.Gate, route == "leave" ? "这次窗口关闭，保留来路" : "带着准备与恢复，真正抵达", c.originNode, Difference(before));
            if (!string.IsNullOrEmpty(c.returnNode)) CausalGraph.Link(node, c.returnNode);
            foreach (ActionRecord action in Actions.Where(a => a.kind == CardKind.Growth || a.givesSupport))
            {
                CausalNode echo = CausalNodes.Find(n => n.type == CausalNodeKind.Echo && n.parentId == action.nodeId && n.resolved);
                if (echo != null) CausalGraph.Link(node, echo.id);
                else if (action.givesSupport && route == "help") CausalGraph.Link(node, action.nodeId);
            }
            node.gatePassed = route != "leave"; c.outcome = route == "leave" ? ChapterOutcome.WindowClosed : ChapterOutcome.Arrived;
            c.route = route; c.outcomeNode = node.id; Command("chapter-boss", route); RefreshEngine(); RecordResourceSample();
            if (route == "leave") { ObservePattern("setback", false, node); Emit(DomainEventKind.OpportunityExpired, node, "窗口关闭", "这次没有抵达。回看具体原因，下一次仍可换一种准备与恢复方法。"); }
            else { Master.insightPoints += 3; ChargeOverdrive(12, node); Emit(DomainEventKind.Victory, node, "LIVE THE FUTURE", c.Goal + "。你没有消除困难，而是穿过了它。", CausalGraph.Ancestors(CausalNodes, node.id).Count); }
            return true;
        }

        public string ChapterNextStep
        {
            get {
                StoryChapter c = Master?.chapter; if (c == null) return null;
                if (c.outcome != ChapterOutcome.InProgress) return c.outcome == ChapterOutcome.Arrived ? "你已抵达目标。看看这一路由哪些选择连接起来。" : "窗口已经关闭；这条人生仍可以继续。";
                if (ChapterNeedsPreparation) return "不确定出现了：选一种准备方法，让下一次挫折的代价改变。";
                if (!c.setbackOccurred) return "D" + c.failureDay + " 会收到作品反馈。成长、恢复与支持都能成为准备。";
                if (string.IsNullOrEmpty(c.recoveryNode)) return "第一次没有做好。先恢复或求助，给下一次行动留下空间。";
                if (string.IsNullOrEmpty(c.returnNode)) return "你已恢复。现在选一次成长行动，真正重新开始。";
                return "你已经再次行动。D" + c.windowDay + " 前带着能力、状态或支持抵达。";
            }
        }
    }
}
