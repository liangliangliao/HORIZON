using System;
using System.Collections.Generic;
using System.Linq;

namespace Horizon.Game
{
    public sealed partial class GameSession
    {
        public bool UsesExpedition { get { return CatalogVersion >= 10 && UsesMasterRules; } }
        public bool ReceiveFutureMessage(string id, string text, int sourceDay)
        {
            if (!UsesExpedition || CompletedRun != null || Day >= Deadline || string.IsNullOrWhiteSpace(id) || id.Length > 100 || id.Contains("|") ||
                string.IsNullOrWhiteSpace(text) || text.Length > 200 || sourceDay < 1 || sourceDay > 60 || Master.expedition.receivedMessages.Contains(id)) return false;
            int due = Math.Min(Deadline, Math.Max(Day + 1, sourceDay + 1));
            CausalNode source = MasterNode(CausalNodeKind.Memory, "另一条人生留下的经验");
            CausalNode echo = AddNode(CausalNodes, source.id, CausalNodeKind.Echo, due, text, "future-message", false);
            Pending.Add(new PendingEcho { sourceDay = Day, dueDay = due, cardId = "future-message", cardName = "另一条人生的经验",
                echoName = text, delta = new ResourceDelta(), nodeId = echo.id, depth = 1, kind = CardKind.Growth });
            Master.expedition.receivedMessages.Add(id); Command("future-message", id + "|" + text, sourceDay); return true;
        }
        public bool RevealHiddenCause(string id)
        {
            if (!UsesExpedition || MasterHorizon < 8 && Day > Master.overdriveUntilDay) return false;
            MysteryRecord mystery = Mysteries.Find(m => m.consequenceNodeId == id && !m.revealed);
            CausalNode node = mystery == null ? null : CausalNodes.Find(n => n.id == id);
            if (node == null || !node.resolved || !node.originHidden) return false;
            node.originHidden = false; mystery.revealed = true; mystery.revealDay = Day;
            Command("reveal-cause", id); Emit(DomainEventKind.Synchronized, node, "HORIZON VIII · 看见隐藏来路", "这份变化来自 D" + mystery.sourceDay + " 的真实行动。"); return true;
        }
        private void InitializeExpedition()
        {
            if (!UsesExpedition) return;
            Master.knowledge = KnowledgeLibrary.All.Select(x => x.Copy()).ToList();
            foreach (KnowledgeSkill old in Master.initialKnowledge)
            { int index = Master.knowledge.FindIndex(k => k.id == old.id); if (index >= 0) Master.knowledge[index] = old.Copy(); }
            Master.initialKnowledge = Master.knowledge.Select(k => k.Copy()).ToList();
            if (Master.mode == RunMode.MirrorRun)
                Master.expedition.mirrorKey = Master.patterns.OrderByDescending(x => x.lastRun).FirstOrDefault(x => !x.broken)?.key ?? "decision-reopen";
            RefreshEngine();
        }
        private CardSpec[] ExpeditionHand(CardSpec[] hand)
        {
            if (!UsesExpedition) return hand;
            var cards = (CardSpec[])hand.Clone(); var state = Master.expedition;
            if (!string.IsNullOrEmpty(state.route) && Day >= 5 && Day % 3 == 2 && cards[0].Kind == CardKind.Temptation)
                cards[0] = CardCatalog.FindById(LifeRoutes.Cards[Array.IndexOf(LifeRoutes.Ids, state.route)]) ?? cards[0];
            if (Master.mode == RunMode.ExperimentRun && Day > 1 && Day % 3 == 0) cards[0] = MasterContent.Actions[(Day / 3 - 1) % 7];
            if (Master.mode == RunMode.ImaginationRun && Day > 1 && Day % 4 == 2) cards[0] = MasterContent.Actions[3];
            if (Master.mode == RunMode.ChaosRun && Day > 1)
                cards[0] = MasterContent.Actions[(int)((uint)(WorldSeed * 31 + Day * 17) % 7)];
            return cards;
        }
        private void RefreshExpeditionEngine()
        {
            if (!UsesExpedition) return;
            var s = Master.expedition; var e = Master.engine;
            s.environment = ((uint)(WorldSeed + Day) % 3) == 0 ? "下雨" : ((uint)(WorldSeed + Day) % 3) == 1 ? "干扰较多" : "安静";
            e.motivation = Math.Min(10, 4 + s.motivationBoost + (Master.windows.Any(w => !w.taken && !w.expired && Day <= w.momentumUntilDay) ? 3 : 0));
            e.friction = Math.Max(0, 5 + (s.environment == "下雨" ? 1 : 0) - s.frictionReduction -
                (Master.triggers.Contains("route") || Master.triggers.Contains("place") ? 1 : 0) - (InExecutionMode ? Master.decision.step : 0));
            e.alternativeReward = Math.Max(0, 3 + Actions.TakeLastPortable(3).Count(a => a.kind == CardKind.Temptation) - s.rewardReduction);
            e.socialPressure = Math.Min(10, (Master.triggers.Contains("promise") ? 3 : 0) + (Master.triggers.Contains("friend") ? 2 : 0));
            e.trigger = Math.Min(10, Master.triggers.Sum(id => id == "deadline" || id == "deposit" ? 3 : id == "place" ? 1 : 2));
        }
        public bool ChooseLifeRoute(string id)
        {
            RequireMasterChoice(); if (!UsesExpedition || !LifeRoutes.Ids.Contains(id)) return false;
            var state = Master.expedition; if (state.route == id) return false;
            int cost = string.IsNullOrEmpty(state.route) ? 0 : 1; if (Insight < cost) return false;
            ResourceDelta before = Values(); Apply(new ResourceDelta(insight: -cost));
            CausalNode n = MasterNode(CausalNodeKind.Decision, "选择人生路线 · " + LifeRoutes.Names[Array.IndexOf(LifeRoutes.Ids, id)], state.routeNode, Difference(before));
            state.route = id; state.routeNode = n.id; Command("life-route", id); RefreshEngine(); RecordResourceSample(); return true;
        }
        public bool RemoveTrigger(string id)
        {
            RequireMasterChoice(); if (!UsesExpedition || !Master.triggers.Remove(id)) return false;
            CausalNode n = MasterNode(CausalNodeKind.Trigger, "移除提示 · " + TriggerEquipment.Names[Array.IndexOf(TriggerEquipment.Ids, id)]);
            if (id == "promise") { ResourceDelta before = Values(); Apply(new ResourceDelta(relation: -1)); n.effect = Difference(before); n.effectRecorded = true; }
            Command("remove-trigger", id); SpendPreparation(n); RefreshEngine(); RecordResourceSample(); return true;
        }
        public bool AdjustEnvironment(string strategy)
        {
            RequireMasterChoice(); if (!UsesExpedition || !new[] { "remove-reward", "two-minutes", "external-promise", "fixed-place" }.Contains(strategy)) return false;
            var state = Master.expedition;
            if (state.preparationsToday >= 6 || Insight < 1 || strategy == "external-promise" && Relation < 1 ||
                strategy == "remove-reward" && state.rewardReduction >= 4 || strategy == "two-minutes" && state.frictionReduction >= 4) return false;
            ResourceDelta before = Values(); Apply(new ResourceDelta(insight: -1, relation: strategy == "external-promise" ? -1 : 0));
            if (strategy == "remove-reward") state.rewardReduction = Math.Min(4, state.rewardReduction + 2);
            if (strategy == "two-minutes" || strategy == "fixed-place") state.frictionReduction = Math.Min(4, state.frictionReduction + 2);
            if (strategy == "external-promise") { state.motivationBoost = Math.Min(4, state.motivationBoost + 2); }
            CausalNode n = MasterNode(CausalNodeKind.Execution, "调整行动条件 · " + strategy, InExecutionMode ? Master.decision.nodeId : state.routeNode, Difference(before));
            Command("environment", strategy); SpendPreparation(n); RefreshEngine(); RecordResourceSample(); return true;
        }
        private void SpendPreparation(CausalNode source)
        {
            if (!UsesExpedition) return;
            var state = Master.expedition; state.preparationsToday = Math.Min(20, state.preparationsToday + 1);
            if (state.preparationsToday != 3) return;
            foreach (OpportunityWindow w in Master.windows.Where(w => !w.taken && !w.expired && Day <= w.momentumUntilDay))
            {
                w.momentumUntilDay = Day - 1;
                CausalNode n = MasterNode(CausalNodeKind.Thought, "反复准备让行动势头消退", source?.id); CausalGraph.Link(n, w.nodeId);
                Emit(DomainEventKind.MomentumExpired, n, "MOMENTUM 已消退", "机会仍在，但即时行动的精力优惠已经结束。");
            }
        }
        public bool SetWorldview(string name, bool equipped)
        {
            RequireMasterChoice(); if (!UsesExpedition || !WorldviewDeck.Names.Contains(name)) return false;
            var deck = Master.expedition.worldviews;
            if (equipped ? deck.Contains(name) || deck.Count >= 3 : !deck.Contains(name)) return false;
            if (equipped) deck.Add(name); else deck.Remove(name);
            MasterNode(CausalNodeKind.Thought, (equipped ? "选择认知工具 · " : "收起认知工具 · ") + name);
            Command(equipped ? "worldview-add" : "worldview-remove", name); return true;
        }
        public string WorldviewPerspective(CardSpec card)
        {
            if (!UsesExpedition || Master.expedition.worldviews.Count == 0) return "不同思想可以提供视角，决定仍由你做。";
            return string.Join("\n", Master.expedition.worldviews.Select(name => name + "：" + WorldviewDeck.Perspectives[Array.IndexOf(WorldviewDeck.Names, name)] +
                "\n今天可以把它用在「" + card.Name + "」上。"));
        }
        public bool CanRecognizeSkill(string id)
        {
            if (!UsesExpedition || Master.knowledge.Find(k => k.id == id)?.stage != KnowledgeStage.Know) return false;
            switch (id)
            {
                case "face": return Master.awaitingComeback || Master.chapter?.setbackOccurred == true || Mood <= 4;
                case "rest": return Energy <= 4;
                case "small": return Master.engine.friction >= 5;
                case "evidence": return Predictions.Count > 0;
                case "control": return InExecutionMode;
                case "connection": return Relation < 6;
                case "commitment": return InExecutionMode;
                case "values": return !string.IsNullOrEmpty(Master.expedition.route);
                default: return false;
            }
        }
        public bool RecognizeSkill(string id)
        {
            RequireMasterChoice(); if (!CanRecognizeSkill(id)) return false;
            KnowledgeSkill k = Master.knowledge.Find(x => x.id == id);
            CausalNode node = MasterNode(CausalNodeKind.Thought, "识别情境 · " + k.condition, Master.lastFailureNode);
            KnowledgeForge.Advance(k, KnowledgeStage.Recognize, node.id); k.recognitionNodeId = node.id; Command("recognize-skill", id); return true;
        }
        public ThoughtMonster ActiveThought
        {
            get {
                var e = Master.engine;
                if (InExecutionMode && Master.decision.reopens > 0) return ThoughtMonsters.ById("possibility");
                if (Master.chapter?.id == "gaze" || e.socialPressure >= 3) return ThoughtMonsters.ById("gaze");
                if (e.fatigue >= 6) return ThoughtMonsters.ById("tomorrow");
                if (e.alternativeReward >= 5) return ThoughtMonsters.ById("comfort");
                if (Master.expedition.environment == "下雨") return ThoughtMonsters.ById("unsuitable");
                return ThoughtMonsters.ById(e.friction >= 5 ? "perfect" : "possibility");
            }
        }
        public bool RespondToThought(string response)
        {
            RequireMasterChoice(); if (!UsesExpedition || Master.expedition.monsterDay == Day || !new[] { "act", "delay", "reframe" }.Contains(response)) return false;
            if (response != "delay" && Insight < 1) return false;
            ThoughtMonster thought = ActiveThought; ResourceDelta before = Values();
            if (response != "delay") Apply(new ResourceDelta(insight: -1));
            CausalNode n = MasterNode(CausalNodeKind.Thought, thought.name + " · " + (response == "act" ? "带着这个念头行动" : response == "delay" ? "暂时推迟" : "重新解释情境"),
                InExecutionMode ? Master.decision.nodeId : Master.expedition.routeNode, Difference(before));
            Master.expedition.monsterId = thought.id; Master.expedition.monsterNode = n.id; Master.expedition.monsterDay = Day;
            if (response == "act") Master.expedition.frictionReduction = Math.Min(4, Master.expedition.frictionReduction + 1);
            if (response == "reframe") Master.expedition.rewardReduction = Math.Min(4, Master.expedition.rewardReduction + 1);
            if (response == "delay") { SpendPreparation(n); SpendPreparation(n); SpendPreparation(n); }
            ObservePattern("thought:" + thought.id, response != "delay", n); Command("thought", response); RefreshEngine(); RecordResourceSample(); return true;
        }
        public bool MirrorEncounterPending { get { return UsesExpedition && Master.mode == RunMode.MirrorRun && Day >= Master.expedition.mirrorDay && !Master.expedition.mirrorResolved; } }
        public bool ResolveMirror(string response)
        {
            RequireMasterChoice(); if (!MirrorEncounterPending || !new[] { "continue", "compare", "recover" }.Contains(response) || response == "continue" && Insight < 1) return false;
            var state = Master.expedition; ResourceDelta before = Values();
            if (response == "continue") Apply(new ResourceDelta(insight: -1));
            CausalNode n = MasterNode(CausalNodeKind.Pattern, "镜像节点 · " + PatternEngine.Description(state.mirrorKey), state.routeNode, Difference(before));
            state.mirrorResolved = true;
            if (response == "compare") ObservePattern(state.mirrorKey, false, n);
            else { Master.awaitingComeback = true; Master.lastFailureNode = n.id; state.lastRecovery = ""; }
            Command("mirror", response); RefreshEngine(); RecordResourceSample(); return true;
        }
        private void ExpeditionAfterChoice(ActionRecord action, CardSpec card, CausalNode node, ResourceDelta before)
        {
            if (!UsesExpedition) return;
            var state = Master.expedition;
            Master.observations.Add(new BehaviorObservation { id = "run:" + RunNumber + ":action-observation:" + Day,
                key = "action", nodeId = node.id, cardId = card.Id, energyBefore = before.energy, continued = true, run = RunNumber, day = Day,
                triggerId = Master.triggers.FirstOrDefault(id => TriggerEquipment.Supports(id, card)), triggerIds = Master.triggers.Where(id => TriggerEquipment.Supports(id, card)).ToList(),
                rewardLanguage = card.Kind == CardKind.Growth ? "progressive" : card.Kind == CardKind.Recovery ? "soft" : "instant" });
            if (!string.IsNullOrEmpty(state.routeNode)) CausalGraph.Link(node, state.routeNode);
            if (state.monsterDay == Day) CausalGraph.Link(node, state.monsterNode);
            if (Master.mode == RunMode.MirrorRun && state.mirrorResolved && Master.awaitingComeback)
            {
                if (string.IsNullOrEmpty(state.lastRecovery) && (card.Kind == CardKind.Recovery || card.GivesSupport)) { state.lastRecovery = node.id; CausalGraph.Link(node, Master.lastFailureNode); }
                else if (card.Kind == CardKind.Growth && !string.IsNullOrEmpty(state.lastRecovery))
                { CausalGraph.Link(node, state.lastRecovery); ObservePattern(state.mirrorKey, true, node); Master.awaitingComeback = false;
                    Master.insightPoints += Master.resilienceChain; Emit(DomainEventKind.Comeback, node, "FAIL & AGAIN ×" + Master.resilienceChain); }
            }
            foreach (KnowledgeSkill k in Master.knowledge.Where(k => k.stage == KnowledgeStage.Recognize && KnowledgeLibrary.Matches(k.id, card)))
            { KnowledgeForge.Advance(k, KnowledgeStage.Simulate, node.id); k.simulationRun = RunNumber; CausalGraph.Link(node, k.recognitionNodeId); }
            foreach (string tool in state.worldviews.Where(x => card.Kind == CardKind.Growth))
            { CausalNode thought = MasterNode(CausalNodeKind.Thought, tool + " · " + WorldviewDeck.Perspectives[Array.IndexOf(WorldviewDeck.Names, tool)]); CausalGraph.Link(node, thought.id); }
            if (card.Traits.risk > 0)
            {
                bool success = (uint)(WorldSeed * 13 + Day * 7) % 4 != 0;
                CausalNode risk = MasterNode(CausalNodeKind.Situation, success ? "小风险 · 打开了新经验" : "小风险 · 这次尝试受阻", node.id);
                var previous = Values(); Apply(success ? new ResourceDelta(insight: 1) : new ResourceDelta(mood: -1)); risk.effect = Difference(previous); risk.effectRecorded = true;
                if (!success) { Master.awaitingComeback = true; Master.lastFailureNode = risk.id; Master.resilienceChain = Math.Min(10, Master.resilienceChain + 1); ObservePattern("risk-recovery", false, risk); Emit(DomainEventKind.FailAndAgain, risk, "尝试受阻 · 仍可恢复"); }
            }
            if (card.Traits.commitment > 0) { Master.expedition.motivationBoost = Math.Min(4, state.motivationBoost + 1); }
            string pool = card.GivesSupport ? "connection" : card.Kind == CardKind.Recovery ? "recovery" :
                card.Traits.families.HasFlag(CardFamily.Knowledge) ? "skill" : card.Kind == CardKind.Growth ? "growth" : null;
            if (pool != null)
            { InvestmentPool investment = state.pools.Find(x => x.id == pool); if (investment == null) { investment = new InvestmentPool { id = pool }; state.pools.Add(investment); } investment.sources.Add(node.id); }
            if (card.Kind == CardKind.Temptation) ObservePattern("short-reward", false, node);
            else if (Master.patterns.Any(p => p.key == "short-reward" && !p.broken && p.failures >= 2)) ObservePattern("short-reward", true, node);
            else if (before.energy <= 3 && card.Kind == CardKind.Recovery) ObservePattern("overwork", true, node);
        }
        public void RecordChoiceDuration(int milliseconds)
        {
            if (!UsesExpedition) return;
            BehaviorObservation observation = Master.observations.LastOrDefault(o => o.key == "action" && o.day == Day);
            if (observation != null) observation.hesitationMilliseconds = Math.Max(0, Math.Min(300000, milliseconds));
        }
        private void ExpeditionAfterAdvance(List<PendingEcho> due)
        {
            if (!UsesExpedition) return;
            var state = Master.expedition; state.preparationsToday = 0; state.monsterDay = 0;
            foreach (InvestmentPool pool in state.pools)
            {
                bool ready = pool.id == "connection" ? Relation >= 6 && Energy >= 3 : pool.id == "recovery" ? Energy >= 7 && Ability >= 4 : Ability >= 6 && Mood >= 4;
                if (pool.sources.Count < 4 || !ready) continue;
                ResourceDelta before = Values(); Apply(pool.id == "connection" ? new ResourceDelta(money: 1, relation: 1) : pool.id == "recovery" ? new ResourceDelta(insight: 1, mood: 1) : new ResourceDelta(ability: 1, money: 1));
                CausalNode n = MasterNode(CausalNodeKind.Breakthrough, "储备兑现 · " + pool.id, effect: Difference(before));
                foreach (string source in pool.sources.Take(4)) CausalGraph.Link(n, source); pool.sources.RemoveRange(0, 4); pool.paid++;
                Master.insightPoints += 2; if (pool.id == "recovery") ActivateOrbit(5, n);
                Emit(DomainEventKind.Breakthrough, n, "BREAKTHROUGH", "四次真实投资与当前条件共同打开了新的余量。", 4);
                due.Add(new PendingEcho { sourceDay = CausalGraph.Ancestors(CausalNodes, n.id).Min(x => x.day), dueDay = Day, cardName = "长期储备", echoName = n.label,
                    delta = n.effect, actualDelta = n.effect, nodeId = n.id, kind = CardKind.Growth });
            }
            RefreshEngine();
        }
    }
}
