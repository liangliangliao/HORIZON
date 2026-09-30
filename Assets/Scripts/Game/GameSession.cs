using System;
using System.Collections.Generic;

namespace Horizon.Game
{
    [Serializable]
    public sealed class ActionRecord
    {
        public int day;
        public string cardId;
        public string cardName;
        public CardKind kind;
        public int echoDay;
        public bool echoed;
        public string echoName;
        public bool givesSupport;
        public ResourceDelta now;
        public ResourceDelta later;
        public int secondaryDay;
        public string secondaryName;
        public ResourceDelta secondary;
        public bool secondaryResolved;
        public string nodeId;
        public string parentNodeId;
    }

    [Serializable]
    public sealed class PendingEcho
    {
        public int sourceDay;
        public int dueDay;
        public string cardId;
        public string cardName;
        public string echoName;
        public ResourceDelta delta;
        public CardKind kind;
        public int depth;
        public int parentDay;
        public string nodeId;
        public string parentNodeId;
        public string replacementId;
        public CardKind replacementSlot;
    }

    public enum CausalNodeKind { Action, Echo, Choice }

    [Serializable]
    public sealed class CausalNode
    {
        public string id;
        public string parentId;
        public CausalNodeKind type;
        public int day;
        public int depth;
        public string label;
        public string cardId;
        public bool resolved;
        public string replacementId;
        public CardKind replacementSlot;
    }

    [Serializable]
    public sealed class BossResult
    {
        public bool ability;
        public bool state;
        public bool support;
        public int passed;
        public string ghost;
        public List<int> abilityDays = new List<int>();
        public List<int> stateDays = new List<int>();
        public List<int> supportDays = new List<int>();
        public GhostTimeline ghostTimeline;
    }

    [Serializable]
    public sealed class GhostTimeline
    {
        public int sourceDay;
        public string originalName;
        public string alternativeId;
        public string alternativeName;
        public int echoDay;
        public string echoName;
        public int changedChoiceDay;
        public string changedChoiceName;
        public string gateName;
        public bool gateOpens;
        public int beforePassed;
        public int afterPassed;
        public int finalEnergy;
        public int finalMood;
        public int finalInsight;
        public int finalRelation;
        public int finalMoney;
        public int finalAbility;
    }

    [Serializable]
    public sealed class PredictionRecord
    {
        public int sourceDay;
        public int dueDay;
        public int baseEnergy;
        public int baseMood;
        public int baseInsight;
        public int energy;
        public int mood;
        public int insight;
        public int actualEnergy;
        public int actualMood;
        public int actualInsight;
        public bool evaluated;
        public bool accurate;
        public bool reviewed;
    }

    [Serializable]
    public sealed class RunRecord
    {
        public int number;
        public string title;
        public BossResult boss;
        public List<ActionRecord> actions = new List<ActionRecord>();
        public int finalEnergy;
        public int finalMood;
        public int finalInsight;
        public int finalRelation;
        public int finalMoney;
        public int finalAbility;
        public PredictionRecord prediction;
        public List<CausalNode> causalNodes = new List<CausalNode>();
    }

    [Serializable]
    public sealed class RunSnapshot
    {
        public int rulesVersion;
        public int day;
        public int runNumber;
        public int energy;
        public int mood;
        public int insight;
        public int relation;
        public int money;
        public int ability;
        public int supportActions;
        public int focusUses;
        public bool hasChosen;
        public bool stationVisited;
        public bool socialUnavailableToday;
        public bool predictionSkipped;
        public PredictionRecord prediction;
        public List<ActionRecord> actions = new List<ActionRecord>();
        public List<PendingEcho> pending = new List<PendingEcho>();
        public List<CausalNode> causalNodes = new List<CausalNode>();
    }

    public sealed class DayTransition
    {
        public readonly int Day;
        public readonly List<PendingEcho> Echos;

        public DayTransition(int day, List<PendingEcho> echos)
        {
            Day = day;
            Echos = echos;
        }
    }

    public sealed class FutureProjection
    {
        public readonly string CardName;
        public readonly bool Available;
        public readonly int TargetDay;
        public readonly int EchoDay;
        public readonly string EchoName;
        public readonly int Energy;
        public readonly int Mood;
        public readonly int Insight;
        public readonly int Relation;
        public readonly int Money;
        public readonly int Ability;

        public FutureProjection(CardSpec card, bool available, int sourceDay, int targetDay,
            int energy, int mood, int insight, int relation, int money, int ability)
        {
            CardName = card.Name;
            Available = available;
            TargetDay = targetDay;
            EchoDay = card.Delay > 0 ? sourceDay + card.Delay : 0;
            EchoName = card.EchoName;
            Energy = energy;
            Mood = mood;
            Insight = insight;
            Relation = relation;
            Money = money;
            Ability = ability;
        }
    }

    // Calibration only changes what the player can inspect. It never changes a
    // resource, the chance of an event, or the outcome of a choice.
    public static class ForecastKnowledge
    {
        public static string Clue(PendingEcho echo, int horizonLevel, int calibrations)
        {
            if (echo == null) throw new ArgumentNullException("echo");
            string clue = echo.depth >= 2 ?
                calibrations >= 10 ? echo.echoName + " · 源自 D" + echo.sourceDay :
                horizonLevel >= 2 ? "一次选择可能改变" : "一处尚未看清的回声" :
                horizonLevel >= 3 ? echo.echoName :
                horizonLevel >= 2 ? echo.kind == CardKind.Temptation ? "火种" :
                    echo.kind == CardKind.Growth ? "芽" : "回应" :
                echo.kind == CardKind.Temptation ? "一处微弱的火种" : "一颗尚未发芽的种子";
            if (calibrations == 0 || echo.delta == null) return clue;
            int[] effects = { echo.delta.energy, echo.delta.mood, echo.delta.insight,
                echo.delta.relation, echo.delta.money, echo.delta.ability };
            string[] names = { "精力", "心情", "洞察", "关系", "金钱", "能力" };
            int strongest = -1;
            for (int i = 0; i < effects.Length; i++)
                if (effects[i] != 0 && (strongest < 0 ||
                    Math.Abs(effects[i]) > Math.Abs(effects[strongest]))) strongest = i;
            if (strongest < 0) return clue;
            string strength = calibrations >= 3 && Math.Abs(effects[strongest]) >= 2 ? "明显" : "";
            return clue + " · " + names[strongest] + strength +
                (effects[strongest] > 0 ? "↑" : "↓");
        }

        public static string UnlockAt(int successfulPredictions)
        {
            return successfulPredictions == 1 ? "解锁 · 未来方向" :
                successfulPredictions == 3 ? "解锁 · 影响强度" :
                successfulPredictions == 10 ? "解锁 · 二阶影响" : "未来方向更加清晰";
        }
    }

    public sealed class GameSession
    {
        public const int LastDay = 12;
        public const int ResourceCap = 10;
        public const int RulesVersion = 3;
        public const int AbilityGate = 6;
        public const int RelationGate = 6;
        public const int MoneyGate = 2;

        public int Day { get; private set; }
        public int RunNumber { get; private set; }
        public int Energy { get; private set; }
        public int Mood { get; private set; }
        public int Insight { get; private set; }
        public int Relation { get; private set; }
        public int Money { get; private set; }
        public int Ability { get; private set; }
        public int SupportActions { get; private set; }
        public int FocusUses { get; private set; }
        public bool HasChosen { get; private set; }
        public bool StationVisited { get; private set; }
        public bool SocialUnavailableToday { get; private set; }
        public PredictionRecord Prediction { get; private set; }
        public bool PredictionSkipped { get; private set; }
        public bool CanPredict { get { return Day == 4 && !HasChosen && Prediction == null && !PredictionSkipped; } }
        public bool HasPredictionReview { get { return Prediction != null && Prediction.evaluated && !Prediction.reviewed; } }
        public RunRecord CompletedRun { get; private set; }
        public readonly List<ActionRecord> Actions = new List<ActionRecord>();
        public readonly List<PendingEcho> Pending = new List<PendingEcho>();
        public readonly List<CausalNode> CausalNodes = new List<CausalNode>();
        private readonly bool replaying;

        private sealed class CausalRule
        {
            public readonly CardKind Trigger;
            public readonly Func<GameSession, PendingEcho, bool> Matches;
            public readonly Func<GameSession, int> DueDay;
            public readonly CardKind Slot;
            public readonly CardSpec Replacement;
            public readonly ResourceDelta Delta;
            public readonly string Label;

            public CausalRule(CardKind trigger, Func<GameSession, PendingEcho, bool> matches,
                Func<GameSession, int> dueDay, CardKind slot, CardSpec replacement,
                ResourceDelta delta, string label)
            {
                Trigger = trigger; Matches = matches; DueDay = dueDay;
                Slot = slot; Replacement = replacement; Delta = delta; Label = label;
            }
        }

        // A rule turns an arriving echo into a future choice event. The event may be
        // ignored, or its replacement action can produce another echo and more links.
        private static readonly CausalRule[] CausalRules =
        {
            new CausalRule(CardKind.Temptation,
                (s, e) => e.delta != null && e.delta.energy < 0 && s.Energy <= 2,
                s => s.NextSupportDay(), CardKind.Recovery, CardCatalog.SoloRecovery,
                new ResourceDelta(0, -1, 0, -1), "错过了一次邀约"),
            new CausalRule(CardKind.Growth,
                (s, e) => s.Insight >= 6,
                s => s.Day + 1, CardKind.Growth, CardCatalog.Opportunity,
                new ResourceDelta(), "一次新的机会"),
            new CausalRule(CardKind.Recovery,
                (s, e) => CardCatalog.FindById(e.cardId)?.GivesSupport == true && s.Relation >= 7,
                s => s.Day + 1, CardKind.Growth, CardCatalog.Together,
                new ResourceDelta(), "有人愿意并肩准备")
        };

        public int HorizonLevel
        {
            get { return RunNumber >= 4 || (RunNumber == 3 && StationVisited) ? 3 : Math.Min(2, RunNumber); }
        }
        public CardSpec[] Hand
        {
            get
            {
                CardSpec[] hand = CardCatalog.ForDay(Day, RunNumber);
                foreach (CausalNode node in CausalNodes)
                {
                    if (node.type != CausalNodeKind.Choice || !node.resolved || node.day != Day) continue;
                    CardSpec replacement = CardCatalog.FindById(node.replacementId);
                    if (replacement != null) hand[(int)node.replacementSlot] = replacement;
                }
                // For pre-graph saves which have already arrived at this day.
                if (SocialUnavailableToday && hand[2].GivesSupport) hand[2] = CardCatalog.SoloRecovery;
                return hand;
            }
        }

        public GameSession(int runNumber) : this(runNumber, false) { }

        private GameSession(int runNumber, bool replaying)
        {
            if (runNumber < 1) throw new ArgumentOutOfRangeException("runNumber");
            RunNumber = runNumber;
            this.replaying = replaying;
            Day = 1;
            Energy = 6;
            Mood = 5;
            Insight = 2;
            Relation = 4;
            Money = 5;
            Ability = 2;
        }

        public static GameSession Restore(RunSnapshot saved)
        {
            if (saved == null || saved.runNumber < 1 || saved.day < 1 || saved.day > LastDay ||
                saved.energy < 0 || saved.energy > ResourceCap || saved.mood < 0 ||
                saved.mood > ResourceCap || saved.insight < 0 || saved.insight > ResourceCap ||
                saved.rulesVersion > RulesVersion || saved.rulesVersion < 0)
                throw new ArgumentException("Invalid run snapshot.", "saved");
            if (saved.rulesVersion < 2) MigrateLegacyResources(saved);
            if (saved.rulesVersion < 3 || saved.causalNodes == null ||
                (saved.causalNodes.Count == 0 && saved.actions != null && saved.actions.Count > 0))
            {
                saved.causalNodes = RebuildGraph(saved.actions, saved.pending);
                saved.rulesVersion = RulesVersion;
            }
            if (saved.relation < 0 || saved.relation > ResourceCap ||
                saved.money < 0 || saved.money > ResourceCap ||
                saved.ability < 0 || saved.ability > ResourceCap)
                throw new ArgumentException("Invalid extended resources.", "saved");
            var session = new GameSession(saved.runNumber)
            {
                Day = saved.day, Energy = saved.energy, Mood = saved.mood,
                Insight = saved.insight, Relation = saved.relation,
                Money = saved.money, Ability = saved.ability,
                SupportActions = saved.supportActions,
                FocusUses = saved.focusUses, HasChosen = saved.hasChosen,
                StationVisited = saved.stationVisited, SocialUnavailableToday = saved.socialUnavailableToday,
                Prediction = saved.prediction, PredictionSkipped = saved.predictionSkipped
            };
            if (saved.actions != null) session.Actions.AddRange(saved.actions);
            if (saved.pending != null) session.Pending.AddRange(saved.pending);
            session.CausalNodes.AddRange(saved.causalNodes);
            return session;
        }

        // Old saves contained three resources. Rebuild new dimensions in day order so
        // returning players keep their current run, including already paid-out echoes.
        private static void MigrateLegacyResources(RunSnapshot saved)
        {
            int relation = 4, money = 5, ability = 2;
            if (saved.actions != null)
            {
                foreach (ActionRecord action in saved.actions)
                {
                    if (action == null) continue;
                    CardSpec card = CardCatalog.FindById(action.cardId);
                    if (card == null) continue;
                    action.now = card.Now;
                    action.later = card.Later;
                    if (action.secondaryDay > 0)
                        action.secondary = new ResourceDelta(0, -1, 0, -1);
                }
            }
            if (saved.pending != null)
            {
                foreach (PendingEcho echo in saved.pending)
                {
                    if (echo == null) continue;
                    CardSpec card = CardCatalog.FindById(echo.cardId);
                    if (echo.depth >= 2) echo.delta = new ResourceDelta(0, -1, 0, -1);
                    else if (card != null) echo.delta = card.Later;
                }
            }
            for (int day = 1; day <= saved.day; day++)
            {
                if (saved.actions == null) continue;
                foreach (ActionRecord action in saved.actions)
                {
                    if (action == null) continue;
                    if (action.echoed && action.echoDay == day)
                        ApplyAdditional(action.later, ref relation, ref money, ref ability);
                    if (action.secondaryResolved && action.secondaryDay == day)
                        ApplyAdditional(action.secondary, ref relation, ref money, ref ability);
                }
                foreach (ActionRecord action in saved.actions)
                    if (action != null && action.day == day)
                        ApplyAdditional(action.now, ref relation, ref money, ref ability);
            }
            saved.relation = relation;
            saved.money = money;
            saved.ability = ability;
            saved.rulesVersion = 2;
        }

        private static void ApplyAdditional(ResourceDelta delta, ref int relation, ref int money, ref int ability)
        {
            if (delta == null) return;
            relation = Clamp(relation + delta.relation);
            money = Clamp(money + delta.money);
            ability = Clamp(ability + delta.ability);
        }

        public RunSnapshot Snapshot()
        {
            return new RunSnapshot
            {
                rulesVersion = RulesVersion, day = Day, runNumber = RunNumber,
                energy = Energy, mood = Mood, insight = Insight,
                relation = Relation, money = Money, ability = Ability,
                supportActions = SupportActions, focusUses = FocusUses,
                hasChosen = HasChosen, stationVisited = StationVisited,
                socialUnavailableToday = SocialUnavailableToday,
                prediction = Prediction, predictionSkipped = PredictionSkipped,
                actions = new List<ActionRecord>(Actions),
                pending = new List<PendingEcho>(Pending),
                causalNodes = new List<CausalNode>(CausalNodes)
            };
        }

        // Older six-resource saves retain their actual effects. Reconstruct only the
        // missing links; pending events keep their original due dates and deltas.
        private static List<CausalNode> RebuildGraph(List<ActionRecord> actions, List<PendingEcho> pending)
        {
            var nodes = new List<CausalNode>();
            if (actions == null) return nodes;
            foreach (ActionRecord action in actions)
            {
                if (action == null) continue;
                CausalNode choice = nodes.Find(n => n.type == CausalNodeKind.Choice &&
                    n.day == action.day && n.replacementId == action.cardId);
                var origin = AddNode(nodes, choice == null ? null : choice.id,
                    CausalNodeKind.Action, action.day, action.cardName, action.cardId, true);
                action.nodeId = origin.id;
                action.parentNodeId = origin.parentId;
                if (action.echoDay > action.day && action.echoDay <= LastDay)
                {
                    CausalNode echo = AddNode(nodes, origin.id, CausalNodeKind.Echo,
                        action.echoDay, action.echoName, action.cardId, action.echoed);
                    if (pending != null)
                        foreach (PendingEcho item in pending)
                            if (item != null && item.depth == 1 && item.sourceDay == action.day)
                            { item.nodeId = echo.id; item.parentNodeId = origin.id; }
                    if (action.secondaryDay > action.echoDay && action.secondaryDay <= LastDay)
                    {
                        CausalNode consequence = AddNode(nodes, echo.id, CausalNodeKind.Choice,
                            action.secondaryDay, action.secondaryName, action.cardId, action.secondaryResolved);
                        consequence.replacementId = CardCatalog.SoloRecovery.Id;
                        consequence.replacementSlot = CardKind.Recovery;
                        if (pending != null)
                            foreach (PendingEcho item in pending)
                                if (item != null && item.depth >= 2 && item.sourceDay == action.day &&
                                    item.dueDay == action.secondaryDay)
                                {
                                    item.nodeId = consequence.id; item.parentNodeId = echo.id;
                                    item.replacementId = consequence.replacementId;
                                    item.replacementSlot = consequence.replacementSlot;
                                }
                    }
                }
            }
            return nodes;
        }

        public static List<CausalNode> GraphForRun(RunRecord run)
        {
            if (run == null) return new List<CausalNode>();
            return run.causalNodes != null && run.causalNodes.Count > 0 ?
                run.causalNodes : RebuildGraph(run.actions, null);
        }

        private static CausalNode AddNode(List<CausalNode> nodes, string parentId,
            CausalNodeKind type, int day, string label, string cardId, bool resolved)
        {
            CausalNode parent = nodes.Find(n => n.id == parentId);
            var node = new CausalNode
            {
                id = "n" + (nodes.Count + 1), parentId = parentId, type = type,
                day = day, depth = parent == null ? 1 : parent.depth + 1,
                label = label, cardId = cardId, resolved = resolved
            };
            nodes.Add(node);
            return node;
        }

        public List<CausalNode> CausalPath(string nodeId)
        {
            return CausalPath(CausalNodes, nodeId);
        }

        public static List<CausalNode> CausalPath(List<CausalNode> graph, string nodeId)
        {
            var path = new List<CausalNode>();
            if (graph == null || string.IsNullOrEmpty(nodeId)) return path;
            string cursor = nodeId;
            // A malformed save must never freeze the game on a cycle.
            for (int i = 0; i < graph.Count && !string.IsNullOrEmpty(cursor); i++)
            {
                CausalNode node = graph.Find(n => n != null && n.id == cursor);
                if (node == null || path.Contains(node)) break;
                path.Insert(0, node);
                cursor = node.parentId;
            }
            return path;
        }

        public void LockPrediction(int energy, int mood, int insight)
        {
            if (!CanPredict) throw new InvalidOperationException("Prediction is available once on day four.");
            if (Math.Abs(energy) > 3 || Math.Abs(mood) > 3 || Math.Abs(insight) > 3)
                throw new ArgumentOutOfRangeException("Prediction must be within three points per resource.");
            Prediction = new PredictionRecord
            {
                sourceDay = Day, dueDay = Day + 3,
                baseEnergy = Energy, baseMood = Mood, baseInsight = Insight,
                energy = energy, mood = mood, insight = insight
            };
        }

        public void SkipPrediction()
        {
            if (!CanPredict) throw new InvalidOperationException("No prediction is offered now.");
            PredictionSkipped = true;
        }

        public void VisitStation()
        {
            if (Day != 4 || !HasChosen || StationVisited)
                throw new InvalidOperationException("The station follows the fourth action.");
            StationVisited = true;
        }

        public void MarkPredictionReviewed()
        {
            if (!HasPredictionReview) throw new InvalidOperationException("No prediction is ready to review.");
            Prediction.reviewed = true;
        }

        public bool CanPlay(CardSpec card)
        {
            return card != null && Array.Exists(Hand, candidate => candidate.Id == card.Id) &&
                !CanPredict && !HasPredictionReview && !HasChosen && CompletedRun == null &&
                Energy + card.Now.energy >= 0 && Mood + card.Now.mood >= 0 &&
                Insight + card.Now.insight >= 0 && Relation + card.Now.relation >= 0 &&
                Money + card.Now.money >= 0 && Ability + card.Now.ability >= 0;
        }

        public bool TryFocus()
        {
            if (FocusUses >= 1 || HasPredictionReview || HasChosen || CompletedRun != null) return false;
            FocusUses++;
            return true;
        }

        // A conditional view, not a promise: future choices and newly formed chains are unknown.
        public FutureProjection ProjectFuture(string cardId)
        {
            if (HorizonLevel < 3 || HasChosen || CanPredict || HasPredictionReview || CompletedRun != null)
                throw new InvalidOperationException("Two futures unlock after the third station.");
            CardSpec card = Array.Find(Hand, c => c.Id == cardId);
            if (card == null) throw new ArgumentException("Card not in today's hand.", "cardId");
            int target = Math.Min(LastDay, Day + 3);
            bool available = CanPlay(card);
            if (!available) return new FutureProjection(card, false, Day, target,
                Energy, Mood, Insight, Relation, Money, Ability);

            int energy = Clamp(Energy + card.Now.energy);
            int mood = Clamp(Mood + card.Now.mood);
            int insight = Clamp(Insight + card.Now.insight);
            int relation = Clamp(Relation + card.Now.relation);
            int money = Clamp(Money + card.Now.money);
            int ability = Clamp(Ability + card.Now.ability);
            var projected = new List<PendingEcho>(Pending);
            if (card.Delay > 0 && Day + card.Delay <= target)
                projected.Add(new PendingEcho
                {
                    sourceDay = Day, dueDay = Day + card.Delay,
                    delta = card.Later, depth = 1
                });
            projected.Sort((a, b) => a.dueDay != b.dueDay ? a.dueDay.CompareTo(b.dueDay) :
                a.depth != b.depth ? a.depth.CompareTo(b.depth) : a.sourceDay.CompareTo(b.sourceDay));
            foreach (PendingEcho echo in projected)
            {
                if (echo.dueDay <= Day || echo.dueDay > target || echo.delta == null) continue;
                energy = Clamp(energy + echo.delta.energy);
                mood = Clamp(mood + echo.delta.mood);
                insight = Clamp(insight + echo.delta.insight);
                relation = Clamp(relation + echo.delta.relation);
                money = Clamp(money + echo.delta.money);
                ability = Clamp(ability + echo.delta.ability);
            }
            return new FutureProjection(card, true, Day, target,
                energy, mood, insight, relation, money, ability);
        }

        public ActionRecord Choose(string cardId)
        {
            if (HasChosen || CompletedRun != null) throw new InvalidOperationException("Day already played.");
            if (HasPredictionReview) throw new InvalidOperationException("Review the prediction first.");
            if (CanPredict) throw new InvalidOperationException("Lock a prediction before the fourth choice.");
            CardSpec card = Array.Find(Hand, c => c.Id == cardId);
            if (card == null) throw new ArgumentException("Card not in today's hand.", "cardId");
            if (!CanPlay(card)) throw new InvalidOperationException("Insufficient resources.");

            Apply(card.Now);
            if (card.GivesSupport) SupportActions++;
            CausalNode changedChoice = CausalNodes.Find(n => n.type == CausalNodeKind.Choice &&
                n.resolved && n.day == Day && n.replacementId == card.Id);
            CausalNode origin = AddNode(CausalNodes, changedChoice == null ? null : changedChoice.id,
                CausalNodeKind.Action, Day, card.Name, card.Id, true);
            var action = new ActionRecord
            {
                day = Day, cardId = card.Id, cardName = card.Name, kind = card.Kind,
                echoDay = card.Delay > 0 ? Day + card.Delay : 0,
                echoName = card.EchoName, givesSupport = card.GivesSupport,
                now = card.Now, later = card.Later,
                nodeId = origin.id, parentNodeId = origin.parentId
            };
            Actions.Add(action);
            if (card.Delay > 0 && action.echoDay <= LastDay)
            {
                CausalNode result = AddNode(CausalNodes, origin.id, CausalNodeKind.Echo,
                    action.echoDay, card.EchoName, card.Id, false);
                Pending.Add(new PendingEcho
                {
                    sourceDay = Day, dueDay = action.echoDay, cardId = card.Id,
                    cardName = card.Name, echoName = card.EchoName,
                    delta = card.Later, kind = card.Kind, depth = 1,
                    nodeId = result.id, parentNodeId = origin.id
                });
            }
            HasChosen = true;
            if (Day == LastDay) Complete();
            return action;
        }

        public DayTransition Advance()
        {
            if (!HasChosen || Day == LastDay) throw new InvalidOperationException("Choose before advancing.");
            if (Day == 4 && !StationVisited)
                throw new InvalidOperationException("Visit the future station before advancing.");
            Day++;
            HasChosen = false;
            FocusUses = 0;
            SocialUnavailableToday = false;
            var due = new List<PendingEcho>();
            for (int i = Pending.Count - 1; i >= 0; i--)
            {
                if (Pending[i].dueDay != Day) continue;
                due.Add(Pending[i]);
                Pending.RemoveAt(i);
            }
            due.Sort((a, b) => a.depth != b.depth ? a.depth.CompareTo(b.depth) :
                a.sourceDay.CompareTo(b.sourceDay));
            foreach (PendingEcho echo in due)
            {
                if (echo.delta != null) Apply(echo.delta);
                CausalNode node = CausalNodes.Find(n => n.id == echo.nodeId);
                if (node != null) node.resolved = true;
                ActionRecord source = Actions.Find(a => a.day == echo.sourceDay);
                if (echo.depth >= 2)
                {
                    if (echo.replacementId == CardCatalog.SoloRecovery.Id)
                    {
                        SocialUnavailableToday = true;
                        if (source != null) source.secondaryResolved = true;
                    }
                }
                else
                {
                    if (source != null) source.echoed = true;
                    if (RunNumber >= 3) ScheduleConsequences(echo, source);
                }
            }
            if (Prediction != null && !Prediction.evaluated && Day >= Prediction.dueDay)
            {
                Prediction.actualEnergy = Energy - Prediction.baseEnergy;
                Prediction.actualMood = Mood - Prediction.baseMood;
                Prediction.actualInsight = Insight - Prediction.baseInsight;
                int distance = Math.Abs(Prediction.energy - Prediction.actualEnergy) +
                    Math.Abs(Prediction.mood - Prediction.actualMood) +
                    Math.Abs(Prediction.insight - Prediction.actualInsight);
                Prediction.accurate = distance <= 2;
                Prediction.evaluated = true;
            }
            return new DayTransition(Day, due);
        }

        private int NextSupportDay()
        {
            for (int day = Day + 1; day <= LastDay; day++)
                if (CardCatalog.ForDay(day, RunNumber)[2].GivesSupport &&
                    !Pending.Exists(e => e.depth >= 2 && e.dueDay == day &&
                        e.replacementSlot == CardKind.Recovery)) return day;
            return 0;
        }

        private void ScheduleConsequences(PendingEcho cause, ActionRecord source)
        {
            if (source == null) return;
            foreach (CausalRule rule in CausalRules)
            {
                if (cause.kind != rule.Trigger || !rule.Matches(this, cause)) continue;
                int day = rule.DueDay(this);
                if (day <= Day || day > LastDay || Pending.Exists(e => e.depth >= 2 &&
                    e.dueDay == day && e.replacementSlot == rule.Slot)) continue;
                CausalNode eventNode = AddNode(CausalNodes, cause.nodeId,
                    CausalNodeKind.Choice, day, rule.Label, cause.cardId, false);
                eventNode.replacementId = rule.Replacement.Id;
                eventNode.replacementSlot = rule.Slot;
                Pending.Add(new PendingEcho
                {
                    sourceDay = cause.sourceDay, parentDay = Day, dueDay = day,
                    cardId = cause.cardId, cardName = cause.cardName,
                    echoName = rule.Label, delta = rule.Delta, kind = cause.kind,
                    depth = 2, nodeId = eventNode.id, parentNodeId = cause.nodeId,
                    replacementId = rule.Replacement.Id, replacementSlot = rule.Slot
                });
                if (rule.Replacement.Id == CardCatalog.SoloRecovery.Id && source.secondaryDay == 0)
                {
                    source.secondaryDay = day;
                    source.secondaryName = rule.Label;
                    source.secondary = rule.Delta;
                }
            }
        }

        private void Apply(ResourceDelta delta)
        {
            Energy = Clamp(Energy + delta.energy);
            Mood = Clamp(Mood + delta.mood);
            Insight = Clamp(Insight + delta.insight);
            Relation = Clamp(Relation + delta.relation);
            Money = Clamp(Money + delta.money);
            Ability = Clamp(Ability + delta.ability);
        }

        private static int Clamp(int value) { return Math.Max(0, Math.Min(ResourceCap, value)); }

        // Reuse the actual rules for a counterfactual; after the changed day, keep the
        // player's recorded choices when legal and use recovery if that path disappeared.
        public static RunRecord ReplayAlternative(RunRecord original, int sourceDay, string alternativeId)
        {
            if (original == null || original.actions == null || original.actions.Count != LastDay ||
                original.number < 1 || sourceDay < 1 || sourceDay > LastDay ||
                string.IsNullOrEmpty(alternativeId))
                throw new ArgumentException("A completed twelve-day run and one alternative are required.");
            var replay = new GameSession(original.number, true);
            for (int day = 1; day <= LastDay; day++)
            {
                if (replay.HasPredictionReview) replay.MarkPredictionReviewed();
                if (day == 4)
                {
                    PredictionRecord prediction = original.prediction;
                    if (prediction == null) replay.SkipPrediction();
                    else replay.LockPrediction(ClampForecast(prediction.energy),
                        ClampForecast(prediction.mood), ClampForecast(prediction.insight));
                }
                ActionRecord recorded = original.actions[day - 1];
                if (recorded == null) return null;
                string desired = day == sourceDay ? alternativeId : recorded.cardId;
                CardSpec choice = Array.Find(replay.Hand, c => c.Id == desired);
                if (day == sourceDay && (desired == recorded.cardId || choice == null || !replay.CanPlay(choice)))
                    return null;
                if (choice == null || !replay.CanPlay(choice))
                {
                    if (day <= sourceDay) return null; // The original prefix must be identical.
                    choice = replay.Hand[2];
                    if (!replay.CanPlay(choice)) return null;
                }
                replay.Choose(choice.Id);
                if (day == 4) replay.VisitStation();
                if (day < LastDay) replay.Advance();
            }
            return replay.CompletedRun;
        }

        private static int ClampForecast(int value) { return Math.Max(-3, Math.Min(3, value)); }

        private static int Deficit(RunRecord run, BossResult baseline)
        {
            int gap = 0;
            if (!baseline.ability) gap += Math.Max(0, AbilityGate - run.finalAbility);
            if (!baseline.state) gap += Math.Max(0, 4 - run.finalEnergy) + Math.Max(0, 4 - run.finalMood);
            if (!baseline.support)
            {
                int supports = run.actions.FindAll(a => a.givesSupport).Count;
                gap += Math.Max(0, 2 - supports) +
                    Math.Max(0, RelationGate - run.finalRelation) +
                    Math.Max(0, MoneyGate - run.finalMoney);
            }
            return gap;
        }

        private static GhostTimeline BuildGhost(RunRecord original)
        {
            BossResult before = original.boss;
            GhostTimeline best = null;
            int bestScore = -1;
            int originalGap = Deficit(original, before);
            for (int day = 1; day <= LastDay; day++)
            {
                foreach (CardSpec card in CardCatalog.ForDay(day, original.number))
                {
                    RunRecord alternate = ReplayAlternative(original, day, card.Id);
                    if (alternate == null) continue;
                    BossResult after = alternate.boss;
                    int opened = (!before.ability && after.ability ? 1 : 0) +
                        (!before.state && after.state ? 1 : 0) +
                        (!before.support && after.support ? 1 : 0);
                    int closed = (before.ability && !after.ability ? 1 : 0) +
                        (before.state && !after.state ? 1 : 0) +
                        (before.support && !after.support ? 1 : 0);
                    int score = opened * 100 - closed * 120 +
                        (originalGap - Deficit(alternate, before)) * 6;
                    if (score <= bestScore) continue;
                    string gate = !before.ability && after.ability ? "能力" :
                        !before.state && after.state ? "状态" :
                        !before.support && after.support ? "支援" :
                        !before.ability ? "能力" : !before.state ? "状态" : "支援";
                    ActionRecord changed = alternate.actions[day - 1];
                    int shiftDay = 0;
                    string shiftName = "";
                    for (int later = day + 1; later <= LastDay; later++)
                    {
                        if (alternate.actions[later - 1].cardId == original.actions[later - 1].cardId) continue;
                        shiftDay = later;
                        shiftName = alternate.actions[later - 1].cardName;
                        break;
                    }
                    bestScore = score;
                    best = new GhostTimeline
                    {
                        sourceDay = day, originalName = original.actions[day - 1].cardName,
                        alternativeId = changed.cardId,
                        alternativeName = changed.cardName,
                        echoDay = changed.echoDay <= LastDay ? changed.echoDay : 0,
                        echoName = changed.echoName,
                        changedChoiceDay = shiftDay, changedChoiceName = shiftName,
                        gateName = gate, gateOpens = opened > 0,
                        beforePassed = before.passed, afterPassed = after.passed,
                        finalEnergy = alternate.finalEnergy, finalMood = alternate.finalMood,
                        finalInsight = alternate.finalInsight,
                        finalRelation = alternate.finalRelation,
                        finalMoney = alternate.finalMoney,
                        finalAbility = alternate.finalAbility
                    };
                }
            }
            return best;
        }

        private void Complete()
        {
            var boss = new BossResult
            {
                ability = Ability >= AbilityGate,
                state = Energy >= 4 && Mood >= 4,
                support = SupportActions >= 2 && Relation >= RelationGate && Money >= MoneyGate
            };
            boss.passed = (boss.ability ? 1 : 0) + (boss.state ? 1 : 0) + (boss.support ? 1 : 0);
            foreach (ActionRecord action in Actions)
            {
                ResourceDelta now = action.now;
                ResourceDelta later = action.echoed ? action.later : null;
                if ((now != null && now.ability > 0) || (later != null && later.ability > 0))
                    boss.abilityDays.Add(action.day);
                if ((now != null && (now.energy != 0 || now.mood != 0)) ||
                    (later != null && (later.energy != 0 || later.mood != 0)) || action.secondaryResolved)
                    boss.stateDays.Add(action.day);
                if (action.givesSupport ||
                    (now != null && (now.relation > 0 || now.money > 0)) ||
                    (later != null && (later.relation > 0 || later.money > 0)))
                    boss.supportDays.Add(action.day);
            }
            string definingAction = Actions.FindLast(a => a.kind == CardKind.Growth)?.cardName ?? Actions[0].cardName;
            CompletedRun = new RunRecord
            {
                number = RunNumber,
                title = boss.passed == 3 ? "我把未来接住了" :
                    boss.passed == 0 ? "这一次，我看见了另一条路" : "从「" + definingAction + "」开始的日子",
                boss = boss,
                actions = new List<ActionRecord>(Actions),
                finalEnergy = Energy, finalMood = Mood, finalInsight = Insight,
                finalRelation = Relation, finalMoney = Money, finalAbility = Ability,
                prediction = Prediction, causalNodes = new List<CausalNode>(CausalNodes)
            };
            if (boss.passed < 3 && !replaying) boss.ghostTimeline = BuildGhost(CompletedRun);
            boss.ghost = boss.passed == 3 ? "你留下的路，已经连成了星图。" :
                boss.ghostTimeline == null ? "没有一个单点改变能保证通过，下一条路还要继续探索。" :
                boss.ghostTimeline.gateOpens ? "如果 D" + boss.ghostTimeline.sourceDay +
                    " 改为「" + boss.ghostTimeline.alternativeName + "」，" + boss.ghostTimeline.gateName +
                    "之门会打开。" : "这条支线接近了" + boss.ghostTimeline.gateName +
                    "之门，但仍未通过。";
        }
    }
}
