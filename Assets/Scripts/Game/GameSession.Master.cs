using System;
using System.Collections.Generic;
using System.Linq;

namespace Horizon.Game
{
    public sealed partial class GameSession
    {
        public MasterRunState Master { get; private set; }
        public bool UsesMasterRules { get { return CatalogVersion >= 7; } }
        public event Action<DomainEvent> DomainEventRaised;
        public int MasterHorizon
        { get { if (!UsesMasterRules) return HorizonLevel; int[] thresholds = { 0, 4, 12, 22, 36, 50, 70, 90 };
            int level = thresholds.Count(v => Master.insightPoints >= v); return Math.Min(8, level + (Day <= Master.overdriveUntilDay ? 2 : 0)); } }
        public bool InExecutionMode { get { return UsesMasterRules && Master.decision != null && Master.decision.day == Day &&
            Master.decision.status != DecisionStatus.Unlocked && Master.decision.status != DecisionStatus.Completed; } }

        public static GameSession StartMasterLife(int number, int seed, RunMode mode, PlayerBehavioralModel model = null, IEnumerable<FutureMemory> memories = null, IEnumerable<KnowledgeSkill> knowledge = null)
        {
            if (!Enum.IsDefined(typeof(RunMode), mode)) throw new ArgumentException("Unknown run mode.");
            var s = new GameSession(number, seed);
            if (mode == RunMode.ThirtyDays || mode == RunMode.LongRun) s.Deadline = 30;
            s.InitializeMaster(mode, model?.patterns, memories, model?.failedRuns ?? 0, model?.foresightPoints ?? 0, knowledge);
            return s;
        }
        private void InitializeMaster(RunMode mode = RunMode.Quick, IEnumerable<PatternRecord> patterns = null, IEnumerable<FutureMemory> memories = null, int failures = 0, int insight = 0, IEnumerable<KnowledgeSkill> knowledge = null)
        {
            if (!UsesMasterRules) return;
            Master = new MasterRunState { mode = mode, initialFailures = failures, awaitingComeback = failures > 0,
                resilienceChain = Math.Min(10, failures + 1), insightPoints = insight, initialInsightPoints = insight };
            Master.initialPatterns = (patterns ?? Enumerable.Empty<PatternRecord>()).Select(p => p.Copy()).ToList();
            Master.patterns = Master.initialPatterns.Select(p => p.Copy()).ToList();
            Master.initialMemories = (memories ?? Enumerable.Empty<FutureMemory>()).TakeLastPortable(32).Select(m => m.Copy()).ToList();
            Master.memories = Master.initialMemories.Select(m => m.Copy()).ToList();
            Master.knowledge.Add(KnowledgeForge.Learn("face", "遇到不确定，想退出时", "恢复、求助或缩小一步，然后继续"));
            KnowledgeSkill learned = knowledge?.FirstOrDefault(k => k.id == "face"); if (learned != null) Master.knowledge[0] = learned.Copy();
            Master.initialKnowledge = Master.knowledge.Select(k => k.Copy()).ToList();
            Master.resources.Add(new ResourceSample { day = Day, values = Values() });
            RefreshEngine();
        }
        private void RefreshEngine()
        {
            if (!UsesMasterRules) return;
            Master.engine.ability = Ability; Master.engine.emotion = Mood; Master.engine.fatigue = 10 - Energy;
            Master.engine.trigger = Math.Min(10, Master.triggers.Count * 2);
            Master.engine.socialPressure = Master.triggers.Contains("promise") || Master.triggers.Contains("friend") ? 2 : 0;
        }
        private DomainEvent Emit(DomainEventKind kind, CausalNode node, string title, string detail = "", int chain = 0)
        {
            DomainEvent e = RewardEngine.Emit(Master, RunNumber, Day, kind, node?.id, title, detail, chain);
            // Optional observers cannot alter a transaction or prevent a choice from completing.
            var handlers = DomainEventRaised;
            if (handlers != null) foreach (Action<DomainEvent> handler in handlers.GetInvocationList())
                try { handler(e.Copy()); } catch (Exception) { /* Presentation failure does not change rules. */ }
            return e;
        }
        private CausalNode MasterNode(CausalNodeKind kind, string label, string parent = null, ResourceDelta effect = null)
        {
            CausalNode n = AddNode(CausalNodes, parent, kind, Day, label, "", true);
            if (effect != null) { n.effect = effect; n.effectRecorded = true; } return n;
        }
        private void Command(string operation, string argument = null, int value = 0, ImagineRun imagination = null)
        { Master.commands.Add(new MasterCommand { day = Day, operation = operation, argument = argument, value = value, imagination = imagination?.Copy() }); }
        private void RequireMasterChoice()
        { if (!UsesMasterRules || HasChosen || CompletedRun != null || CanPredict || HasPredictionReview) throw new InvalidOperationException("Finish the current result before preparing an action."); }

        public void LockDecision(string cardId)
        {
            RequireMasterChoice();
            if (InExecutionMode) throw new InvalidOperationException("A decision is already locked.");
            CardSpec card = Array.Find(Hand, c => c.Id == cardId);
            if (card == null || !CanPlay(card)) throw new ArgumentException("Choose an available action.");
            int reopens = Master.decision?.day == Day ? Master.decision.reopens : 0;
            CausalNode n = MasterNode(CausalNodeKind.Decision, "LOCK · " + card.Name, Master.decision?.day == Day ? Master.decision.nodeId : null);
            Master.decision = new DecisionRecord { day = Day, cardId = cardId, nodeId = n.id, reopens = reopens, status = DecisionStatus.Locked };
            Command("lock", cardId); ChargeOverdrive(8, n); Emit(DomainEventKind.DecisionLocked, n, "DECISION LOCK", "决定已完成。现在只处理下一步执行。");
        }
        public void UnlockDecision()
        {
            RequireMasterChoice(); if (!InExecutionMode) throw new InvalidOperationException("No locked decision.");
            Master.decision.reopens++; Master.decision.status = DecisionStatus.Unlocked;
            CausalNode n = MasterNode(CausalNodeKind.Decision, "执行阶段重新开启决策", Master.decision.nodeId);
            Command("unlock"); ObservePattern("decision-reopen", false, n);
            Emit(DomainEventKind.DecisionReopened, n, "重新比较", "你仍然可以改变决定。这次重新比较已经留下情境记录。");
        }
        public void ExecuteDecisionStep()
        {
            RequireMasterChoice(); if (!InExecutionMode || Master.decision.step >= Master.decision.steps.Count) throw new InvalidOperationException("No execution step.");
            var d = Master.decision;
            CausalNode n = MasterNode(CausalNodeKind.Execution, d.steps[d.step], d.nodeId); d.nodeId = n.id;
            d.step++; d.status = d.step == d.steps.Count ? DecisionStatus.Ready : DecisionStatus.Executing;
            Master.engine.friction = Math.Max(0, Master.engine.friction - 1);
            Command("step"); ChargeOverdrive(4, n); Emit(DomainEventKind.ExecutionStep, n, "EXECUTION " + d.step + " / " + d.steps.Count);
        }
        public bool EquipTrigger(string id)
        {
            RequireMasterChoice(); if (!TriggerEquipment.Ids.Contains(id)) throw new ArgumentException("Unknown trigger.");
            if (Master.triggers.Contains(id) || Master.triggers.Count >= 3) return false;
            Master.triggers.Add(id); RefreshEngine(); Command("trigger", id);
            CausalNode n = MasterNode(CausalNodeKind.Trigger, "Trigger · " + TriggerEquipment.Names[Array.IndexOf(TriggerEquipment.Ids, id)], InExecutionMode ? Master.decision.nodeId : null);
            Emit(DomainEventKind.TriggerEquipped, n, "行动提示已装备"); return true;
        }
        public bool LowerFriction()
        {
            RequireMasterChoice(); if (Master.engine.friction <= 1) return false;
            Master.engine.friction--; Command("friction"); MasterNode(CausalNodeKind.Execution, "删去一个准备步骤", InExecutionMode ? Master.decision.nodeId : null); return true;
        }
        private bool MasterAllows(CardSpec card)
        { return !UsesMasterRules || !InExecutionMode || Master.decision.cardId == card.Id && Master.decision.status == DecisionStatus.Ready; }
        public bool NeedsImagination(CardSpec card)
        {
            return CatalogVersion >= 8 && card?.Id == "imagine" && !Master.commands.Any(c =>
                c.day == Day && c.operation == "imagine" && c.imagination?.goalId == card.Id && c.imagination.phase == ImaginePhase.Complete);
        }
        public bool CanPrepareImagination(CardSpec card)
        {
            return NeedsImagination(card) && !InExecutionMode && !HasChosen && !CanPredict && !HasPredictionReview &&
                CompletedRun == null && Array.Exists(Hand, c => c.Id == card.Id) && CanAfford(card);
        }
        public ResourceDelta ImmediateEffect(CardSpec card)
        {
            ResourceDelta effect = ResourceMath.Copy(card.Now);
            if (UsesMasterRules && Master.windows.Any(w => !w.taken && !w.expired && w.cardId == card.Id && Day <= w.momentumUntilDay) && effect.energy < 0)
                effect.energy++;
            return effect;
        }

        public void AttachImagination(ImagineRun run)
        {
            RequireMasterChoice(); run.Validate();
            if (run.phase != ImaginePhase.Complete) throw new InvalidOperationException("Finish the imagined path, including recovery.");
            if (Master.memories.Any(m => m.id.StartsWith(run.id + ":memory:", StringComparison.Ordinal))) return;
            string parent = null;
            foreach (ImagineBeat beat in run.timeline)
            {
                CausalNode n = MasterNode(CausalNodeKind.Imagination, "想象 · " + beat.text, parent); parent = n.id;
                foreach (FutureMemory m in run.memories.Where(m => m.imaginationNodeId == run.id + ":beat:" + beat.index))
                { var copy = m.Copy(); copy.imaginationNodeId = n.id; copy.imaginationRun = RunNumber; Master.memories.Add(copy); }
            }
            Command("imagine", imagination: run);
            CausalNode last = CausalNodes.Find(n => n.id == parent);
            Master.insightPoints += run.recovered; Emit(DomainEventKind.FutureMemory, last, "FUTURE MEMORY", "你预演了 " + run.failures + " 次失败，也练习了 " + run.recovered + " 次重新开始。");
        }
        public void RecognizeKnowledge()
        {
            RequireMasterChoice(); KnowledgeSkill k = Master.knowledge[0];
            if (k.stage != KnowledgeStage.Know) return;
            CausalNode n = MasterNode(CausalNodeKind.Thought, "识别：不确定不是退出的命令");
            KnowledgeForge.Advance(k, KnowledgeStage.Recognize, n.id); Command("recognize");
        }

        private void ObservePattern(string key, bool continued, CausalNode cause)
        {
            var o = new BehaviorObservation { id = "run:" + RunNumber + ":observation:" + Master.observations.Count,
                key = key, run = RunNumber, day = Day, nodeId = cause.id, continued = continued, triggerId = Master.triggers.LastOrDefault() };
            Master.observations.Add(o);
            PatternRecord p = Master.patterns.Find(x => x.key == key);
            if (p == null) { p = new PatternRecord { key = key, description = PatternEngine.Description(key) }; Master.patterns.Add(p); }
            if (!continued) { p.failures++; p.failureNodes.Add("run:" + RunNumber + ":" + cause.id); if (!p.failureRuns.Contains(RunNumber)) p.failureRuns.Add(RunNumber); p.broken = false;
                if (p.failureRuns.Count >= 2) Emit(DomainEventKind.PatternReinforced, cause, "PATTERN REINFORCED", "最近重复出现：" + p.description); }
            else if (p.failureRuns.Count >= 2 && !p.broken && p.lastBreakRun != RunNumber)
            {
                p.broken = true; p.breakthroughs++; p.lastBreakRun = RunNumber; Master.insightPoints += 8;
                CausalNode n = MasterNode(CausalNodeKind.Pattern, "PATTERN BROKEN · " + p.description, cause.id);
                foreach (string id in p.failureNodes.TakeLastPortable(4))
                { CausalNode memory = MasterNode(CausalNodeKind.Memory, "曾在这里停下 · " + id); CausalGraph.Link(n, memory.id); }
                Emit(DomainEventKind.PatternBroken, n, "PATTERN\nBROKEN", "过去的时间线停在这里。这一次，你继续向前。");
            }
            p.lastRun = RunNumber;
        }
        private void ChargeOverdrive(int points, CausalNode cause)
        {
            if (!UsesMasterRules || Day <= Master.overdriveUntilDay) return;
            if (Master.overdriveGainDay != Day) { Master.overdriveGainDay = Day; Master.overdriveGain = 0; }
            points = Math.Min(points, Math.Max(0, 25 - Master.overdriveGain)); Master.overdriveGain += points;
            Master.overdriveEnergy = Math.Min(100, Master.overdriveEnergy + points);
            if (Master.overdriveEnergy < 100) return;
            Master.overdriveEnergy = 0; Master.overdriveUntilDay = Day + 2;
            CausalNode n = MasterNode(CausalNodeKind.Insight, "HORIZON OVERDRIVE", cause?.id);
            Emit(DomainEventKind.Overdrive, n, "HORIZON\nOVERDRIVE", "两天内视野扩大两层。资源与回声规则保持公平。");
        }
        private void ActivateOrbit(int bit, CausalNode cause)
        {
            int mask = 1 << bit; if ((Master.orbitBits & mask) != 0) return;
            Master.orbitBits |= mask; Emit(DomainEventKind.OrbitActivated, cause, "FUTURE ORBIT · " + MasterSpecification.OrbitNames[bit]);
            if (Master.orbitBits == 255 && !Master.allLinked)
            { Master.allLinked = true; Emit(DomainEventKind.AllLinked, cause, "ALL LINKED", "八个未来节点彼此相连。未来的自己记住了这条人生。"); }
        }
        private void RecordResourceSample()
        {
            ResourceSample sample = Master.resources.Find(s => s.day == Day);
            if (sample == null) Master.resources.Add(new ResourceSample { day = Day, values = Values() }); else sample.values = Values();
        }
        private void MasterAfterChoice(ActionRecord action, CardSpec card, ResourceDelta before)
        {
            if (!UsesMasterRules) return;
            CausalNode n = CausalNodes.Find(x => x.id == action.nodeId);
            bool executed = InExecutionMode;
            if (CatalogVersion >= 8 && card.Id == "trigger" && Master.triggers.Count < 3 && !Master.triggers.Contains(card.Traits.trigger))
            {
                Master.triggers.Add(card.Traits.trigger);
                CausalNode equipment = MasterNode(CausalNodeKind.Trigger, "行动环境 · 闹钟提示已准备", n.id);
                Emit(DomainEventKind.TriggerEquipped, equipment, "行动提示已装备", "这次准备留下了一个可继续使用的环境提示。");
            }
            if (InExecutionMode) { CausalGraph.Link(n, Master.decision.nodeId); Master.decision.status = DecisionStatus.Completed;
                ObservePattern("decision-reopen", true, n); ChargeOverdrive(10, n); }
            if (Master.awaitingComeback && (card.Kind == CardKind.Growth || card.Kind == CardKind.Recovery))
            {
                Master.awaitingComeback = false; Master.insightPoints += Master.resilienceChain;
                if (!string.IsNullOrEmpty(Master.lastFailureNode)) CausalGraph.Link(n, Master.lastFailureNode);
                ObservePattern("setback", true, n); ChargeOverdrive(15, n);
                Emit(DomainEventKind.Comeback, n, "FAIL & AGAIN  ×" + Master.resilienceChain, "倍率代表重新开始的韧性链，提升理解与演出。");
            }
            if (card.Kind == CardKind.Growth) { Master.reservoir++; Master.reservoirSources.Add(n.id); ChargeOverdrive(5, n); ActivateOrbit(1, n); }
            if (card.GivesSupport) ActivateOrbit(3, n);
            if (card.Kind == CardKind.Recovery && Energy >= 5) ActivateOrbit(4, n);
            if (card.Now.money > 0 || card.Later.money > 0) ActivateOrbit(2, n);
            if (card.Id == "opportunity" || card.Id == "portfolio" || card.Id == "project") ActivateOrbit(0, n);
            if (card.Traits.families.HasFlag(CardFamily.Worldview) || card.Traits.families.HasFlag(CardFamily.Exploration)) ActivateOrbit(6, n);
            foreach (FutureMemory memory in Master.memories.Where(m => m.simulationRun != RunNumber && ImaginationEngine.Matches(m, card)))
                if (before.energy <= 4 || before.mood <= 4 || Master.resilienceChain > 1 || executed || card.Id == "again")
                {
                    memory.simulationNodeId = n.id; memory.simulationRun = RunNumber; memory.simulationDay = Day; memory.recalled = true;
                    CausalNode imagined = memory.imaginationRun == RunNumber ? CausalNodes.Find(x => x.id == memory.imaginationNodeId && x.type == CausalNodeKind.Imagination) : null;
                    if (imagined == null) imagined = MasterNode(CausalNodeKind.Memory, "Future Memory · " + memory.text);
                    CausalGraph.Link(n, imagined.id);
                    Emit(DomainEventKind.DejaVu, n, "YOU HAVE SEEN\nTHIS BEFORE", memory.text); break;
                }
            KnowledgeSkill skill = Master.knowledge[0];
            if (skill.stage == KnowledgeStage.Recognize && (card.GivesSupport || card.Id == "again" || card.Id == "forge"))
            { KnowledgeForge.Advance(skill, KnowledgeStage.Simulate, n.id); skill.simulationRun = RunNumber; }
            else if (skill.stage == KnowledgeStage.Simulate && (card.GivesSupport || card.Id == "again" || card.Id == "forge"))
            { skill.simulationNodeId = n.id; skill.simulationRun = RunNumber; }
            foreach (OpportunityWindow w in Master.windows.Where(w => !w.taken && !w.expired && w.cardId == card.Id && w.expiresDay >= Day))
            { w.taken = true; CausalGraph.Link(n, w.nodeId); if (Day <= w.momentumUntilDay) ChargeOverdrive(8, n); }
            action.parentNodeId = n.parentId; action.parentNodeIds = CausalGraph.Parents(n);
            Master.insightPoints++; RefreshEngine(); RecordResourceSample(); Emit(DomainEventKind.ActionTaken, n, card.Name);
        }
        private void MasterAfterEcho(PendingEcho echo)
        {
            if (!UsesMasterRules) return;
            CausalNode n = CausalNodes.Find(x => x.id == echo.nodeId);
            int chain = CausalGraph.Ancestors(CausalNodes, echo.nodeId).Count;
            Emit(DomainEventKind.TimeEcho, n, "TIME ECHO", "D" + echo.sourceDay + " 的「" + echo.cardName + "」回来了。", chain);
            if (chain >= 5) { Master.insightPoints += 2; Emit(chain >= 9 ? DomainEventKind.CausalSingularity : DomainEventKind.Cascade, n,
                chain >= 9 ? "CAUSAL SINGULARITY" : "CASCADE ×" + chain, "过去多个选择形成了真实因果路径。", chain); }
            if (echo.delta != null && (echo.delta.energy < 0 || echo.delta.mood < 0) && (Energy <= 3 || Mood <= 3))
            {
                if (!Master.awaitingComeback) Master.resilienceChain = Math.Min(10, Master.resilienceChain + 1);
                Master.awaitingComeback = true; Master.lastFailureNode = n?.id;
                if (n != null) ObservePattern("setback", false, n);
                Emit(DomainEventKind.FailAndAgain, n, "FAIL · 下一步仍可调整", "恢复、求助或降低一步，然后再次行动。");
            }
            else ChargeOverdrive(4, n);
            if (echo.replacementId == "opportunity" || echo.replacementId == "together")
                Master.windows.Add(new OpportunityWindow { id = echo.nodeId, nodeId = echo.nodeId, cardId = echo.replacementId,
                    openedDay = Day, expiresDay = Day, momentumUntilDay = Day });
        }
        private void MasterAfterAdvance(List<PendingEcho> due)
        {
            if (!UsesMasterRules) return;
            foreach (OpportunityWindow w in Master.windows.Where(w => !w.taken && !w.expired && w.expiresDay < Day))
            { w.expired = true; CausalNode n = MasterNode(CausalNodeKind.Opportunity, "机会窗口已关闭", w.nodeId);
                Emit(DomainEventKind.MomentumExpired, n, "MOMENTUM 已消退", "动机也有时间窗口。准备下一次更容易开始的环境。");
                Emit(DomainEventKind.OpportunityExpired, n, "OPPORTUNITY WINDOW", "有些路线只有当时能走。下一次仍会出现新的机会。"); }
            if (Master.reservoir >= 6 && Ability >= 6 && Relation >= 5 && Energy >= 4 && Money >= 4)
            {
                Master.reservoir -= 6; var before = Values(); Apply(new ResourceDelta(0, 1, 0, 0, 1, 1));
                CausalNode n = MasterNode(CausalNodeKind.Breakthrough, "长期积累 · BREAKTHROUGH", effect: Difference(before));
                foreach (string source in Master.reservoirSources.Take(6)) CausalGraph.Link(n, source); Master.reservoirSources.RemoveRange(0, 6);
                due.Add(new PendingEcho { sourceDay = CausalGraph.Ancestors(CausalNodes, n.id).Min(x => x.day), dueDay = Day,
                    cardName = "长期投资", echoName = n.label, delta = n.effect, actualDelta = n.effect, nodeId = n.id, depth = 3, kind = CardKind.Growth });
                Master.insightPoints += 3; ActivateOrbit(5, n); Emit(DomainEventKind.Breakthrough, n, "BREAKTHROUGH", "能力、状态、关系与机会条件同时成熟。积累正在兑现。");
            }
            RefreshEngine(); RecordResourceSample();
        }
        private void MasterPredictionLocked()
        { if (!UsesMasterRules) return; CausalNode n = MasterNode(CausalNodeKind.Prediction, "LOCK PREDICTION · D" + Prediction.dueDay); ChargeOverdrive(8, n); Emit(DomainEventKind.PredictionLocked, n, "LOCK PREDICTION"); }
        private void MasterPredictionEvaluated(PredictionRecord p)
        {
            if (!UsesMasterRules) return;
            CausalNode origin = CausalNodes.Find(x => x.type == CausalNodeKind.Prediction && x.day == p.sourceDay);
            CausalNode n = MasterNode(CausalNodeKind.Prediction, p.accurate ? "SYNCHRONIZED" : "SURPRISE", origin?.id);
            foreach (CausalNode cause in CausalNodes.FindAll(x => x.effectRecorded && x.day >= p.sourceDay && x.day <= p.dueDay)) CausalGraph.Link(n, cause.id);
            Master.insightPoints += p.accurate ? 2 : 1; ChargeOverdrive(8, n); Emit(p.accurate ? DomainEventKind.Synchronized : DomainEventKind.Surprise, n,
                p.accurate ? "SYNCHRONIZED" : "SURPRISE", "对照差异，再看看过去的哪些行动回来了。");
        }
        private void MasterComplete(BossResult boss)
        {
            if (!UsesMasterRules) return;
            CausalNode gate = CausalNodes.FindLast(n => n.type == CausalNodeKind.Gate);
            if (boss.passed == 3) { ActivateOrbit(7, gate); Emit(DomainEventKind.Victory, gate, "LIVE THE FUTURE", "成长、状态与支援一起抵达截止日。"); }
            else { if (gate != null) ObservePattern("setback", false, gate); Emit(DomainEventKind.FailAndAgain, gate, "FAIL & AGAIN", "这条人生留下了来路，下一条可以从这里继续探索。"); }
        }
        private void ReplayMasterCommands(RunRecord original, int day)
        {
            if (!UsesMasterRules || original.master == null) return;
            foreach (MasterCommand c in original.master.commands.Where(c => c.day == day))
            {
                switch (c.operation)
                {
                    case "lock": if (!InExecutionMode && Array.Exists(Hand, card => card.Id == c.argument && CanPlay(card))) LockDecision(c.argument); break;
                    case "unlock": if (InExecutionMode) UnlockDecision(); break;
                    case "step": if (InExecutionMode && Master.decision.status != DecisionStatus.Ready) ExecuteDecisionStep(); break;
                    case "trigger": EquipTrigger(c.argument); break;
                    case "friction": LowerFriction(); break;
                    case "imagine": AttachImagination(c.imagination); break;
                    case "recognize": RecognizeKnowledge(); break;
                }
            }
        }
    }
}
