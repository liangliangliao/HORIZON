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
        public ResourceDelta actualNow;
        public ResourceDelta actualLater;
        public bool actualNowRecorded;
        public bool actualLaterRecorded;
        public int secondaryDay;
        public string secondaryName;
        public ResourceDelta secondary;
        public bool secondaryResolved;
        public string nodeId;
        public string parentNodeId;
        public List<string> parentNodeIds = new List<string>();
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
        public ResourceDelta actualDelta;
        public CardKind kind;
        public int depth;
        public int parentDay;
        public string nodeId;
        public string parentNodeId;
        public string replacementId;
        public CardKind replacementSlot;
    }

    public enum CausalNodeKind { Action, Echo, Choice, Gate, Situation, World, Mystery,
        Decision, Execution, Trigger, Thought, Imagination, Memory, Pattern, Insight, Opportunity, Breakthrough, Prediction, Reality }

    [Serializable]
    public sealed class CausalNode
    {
        public string id;
        public string parentId;
        public List<string> parentIds = new List<string>();
        public CausalNodeKind type;
        public int day;
        public int depth;
        public string label;
        public string cardId;
        public bool resolved;
        public string replacementId;
        public CardKind replacementSlot;
        public bool gatePassed;
        public ResourceDelta effect;
        public bool effectRecorded;
        public bool originHidden;
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
        public int growthEchoes;
        public int recoveries;
        public int supports;
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
        public int baseRelation, baseMoney, baseAbility;
        public int relation, money, ability;
        public int actualRelation, actualMoney, actualAbility;
        public bool sixAxes;
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
        public MasterRunState master;
        public int catalogVersion;
        public int deadline;
        public int worldSeed;
        public int deckSeed;
        public bool deckSeedRecorded;
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
        public List<PredictionRecord> predictions = new List<PredictionRecord>();
        public List<int> skippedPredictionDays = new List<int>();
        public List<int> stationDays = new List<int>();
        public List<CausalNode> causalNodes = new List<CausalNode>();
        public List<MysteryRecord> mysteries = new List<MysteryRecord>();
    }

    [Serializable]
    public sealed class RunSnapshot
    {
        public MasterRunState master;
        public int rulesVersion;
        public int catalogVersion;
        public int deadline;
        public int worldSeed;
        public int deckSeed;
        public bool deckSeedRecorded;
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
        public List<PredictionRecord> predictions = new List<PredictionRecord>();
        public List<int> skippedPredictionDays = new List<int>();
        public List<int> stationDays = new List<int>();
        public List<ActionRecord> actions = new List<ActionRecord>();
        public List<PendingEcho> pending = new List<PendingEcho>();
        public List<CausalNode> causalNodes = new List<CausalNode>();
        public List<MysteryRecord> mysteries = new List<MysteryRecord>();
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
                horizonLevel >= 2 ? echo.kind == CardKind.Temptation ? ObservationDesign.EchoType(echo) :
                    echo.kind == CardKind.Growth ? "芽" : "回应" :
                echo.kind == CardKind.Temptation ? "一处微弱的" + ObservationDesign.EchoType(echo) : "一颗尚未发芽的种子";
            if (calibrations == 0 || echo.delta == null) return clue;
            int[] effects = { echo.delta.energy, echo.delta.mood, echo.delta.insight,
                echo.delta.relation, echo.delta.money, echo.delta.ability };
            string[] names = { "精力", "心情", "专注", "关系", "金钱", "能力" };
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

    public sealed partial class GameSession
    {
        public const int LastDay = 12;
        public const int ResourceCap = 10;
        public const int RulesVersion = 10;
        public const int AbilityGate = 6;
        public const int RelationGate = 6;
        public const int MoneyGate = 2;

        public int Day { get; private set; }
        public int Deadline { get; private set; }
        public int CatalogVersion { get; private set; }
        public int WorldSeed { get; private set; }
        public int DeckSeed { get; private set; }
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
        public string Situation { get { return CausalNodes.FindLast(n => n.type == CausalNodeKind.World && n.day == Day)?.label ??
            CausalNodes.FindLast(n => n.type == CausalNodeKind.Situation && n.day == Day)?.label; } }
        public PredictionRecord Prediction { get; private set; }
        public bool PredictionSkipped { get; private set; }
        public bool CanPredict { get { return IsPredictionDay && !HasChosen &&
            !HasPredictionReview &&
            !SkippedPredictionDays.Contains(Day) && !Predictions.Exists(p => p.sourceDay == Day) &&
            !(Day == 4 && PredictionSkipped); } }
        public bool HasPredictionReview { get { return Predictions.Exists(p => p.evaluated && !p.reviewed); } }
        public RunRecord CompletedRun { get; private set; }
        public readonly List<PredictionRecord> Predictions = new List<PredictionRecord>();
        public readonly List<int> SkippedPredictionDays = new List<int>();
        public readonly List<int> StationDays = new List<int>();
        public readonly List<ActionRecord> Actions = new List<ActionRecord>();
        public readonly List<PendingEcho> Pending = new List<PendingEcho>();
        public readonly List<CausalNode> CausalNodes = new List<CausalNode>();
        public readonly List<MysteryRecord> Mysteries = new List<MysteryRecord>();
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
                new ResourceDelta(), "有人愿意并肩准备"),
            new CausalRule(CardKind.Temptation,
                (s, e) => s.CatalogVersion >= 6 && e.delta != null && e.delta.mood < 0 && s.Money <= 2,
                s => s.Day + 1, CardKind.Recovery, CardCatalog.FindById("budget"),
                new ResourceDelta(), "给钱和心情留一点空间"),
            new CausalRule(CardKind.Temptation,
                (s, e) => s.CatalogVersion >= 6 && e.delta != null && e.delta.mood > 0 && s.Mood >= 8,
                s => s.Day + 1, CardKind.Growth, CardCatalog.FindById("create"),
                new ResourceDelta(), "快乐带来了创作灵感"),
            new CausalRule(CardKind.Growth,
                (s, e) => s.CatalogVersion >= 6 && s.Relation >= 6 && s.Ability >= 6 && s.Insight < 6,
                s => s.Day + 1, CardKind.Growth, CardCatalog.FindById("teach"),
                new ResourceDelta(), "有人想听你分享经验"),
            new CausalRule(CardKind.Recovery,
                (s, e) => s.CatalogVersion >= 6 && s.Energy >= 8 && s.Relation < 7,
                s => s.Day + 1, CardKind.Growth, CardCatalog.FindById("resilience"),
                new ResourceDelta(), "休息给了你重启的空间")
        };

        public int HorizonLevel
        {
            get { return CatalogVersion >= 6 && Deadline == 30 || RunNumber >= 4 || (RunNumber == 3 && StationVisited) ? 3 : Math.Min(2, RunNumber); }
        }
        public CardSpec[] Hand
        {
            get
            {
                CardSpec[] hand = Day > LastDay && CatalogVersion >= 3 ? CardCatalog.ForOutlookDay(Day, DeckSeed, CatalogVersion) :
                    CardCatalog.ForDay((Day - 1) % LastDay + 1, RunNumber, CatalogVersion, DeckSeed);
                foreach (CausalNode node in CausalNodes)
                    if ((node.type == CausalNodeKind.Situation || node.type == CausalNodeKind.World) && node.day == Day && node.resolved)
                    {
                        CardSpec replacement = node.replacementId == "play" && CatalogVersion >= 3 ?
                            CardCatalog.BalancedPlay : CardCatalog.FindById(node.replacementId);
                        if (replacement != null) hand[(int)node.replacementSlot] = replacement;
                    }
                foreach (CausalNode node in CausalNodes)
                {
                    if (node.type != CausalNodeKind.Choice || !node.resolved || node.day != Day) continue;
                    CardSpec replacement = CardCatalog.FindById(node.replacementId);
                    if (replacement != null) hand[(int)node.replacementSlot] = replacement;
                }
                // For pre-graph saves which have already arrived at this day.
                if (SocialUnavailableToday && hand[2].GivesSupport) hand[2] = CardCatalog.SoloRecovery;
                return UsesMasterRules ? MasterContent.Hand(hand, Day, Master.mode, CatalogVersion, RunNumber, Master.awaitingComeback) : hand;
            }
        }

        public GameSession(int runNumber) : this(runNumber, false, CardCatalog.CurrentVersion, Guid.NewGuid().GetHashCode()) { }
        public GameSession(int runNumber, int worldSeed, int catalogVersion = CardCatalog.CurrentVersion)
            : this(runNumber, false, catalogVersion, worldSeed) { }

        private GameSession(int runNumber, bool replaying, int catalogVersion = 4, int worldSeed = 0)
        {
            if (runNumber < 1) throw new ArgumentOutOfRangeException("runNumber");
            RunNumber = runNumber;
            Deadline = LastDay;
            CatalogVersion = catalogVersion;
            WorldSeed = worldSeed;
            DeckSeed = worldSeed;
            this.replaying = replaying;
            Day = 1;
            Energy = 6;
            Mood = 5;
            Insight = 2;
            Relation = 4;
            Money = 5;
            Ability = 2;
            InitializeMaster();
        }

        public static GameSession Restore(RunSnapshot saved)
        {
            return Restore(saved, false, saved != null && saved.deadline == 30 ? 30 : LastDay);
        }

        public static GameSession ForkForSimulation(RunSnapshot saved, int deadline)
        {
            if (deadline < LastDay || deadline > 30) throw new ArgumentOutOfRangeException("deadline");
            return Restore(saved, true, deadline);
        }

        private static GameSession Restore(RunSnapshot saved, bool simulation, int deadline)
        {
            if (saved == null || saved.runNumber < 1 || saved.day < 1 || saved.day > deadline || !simulation && saved.deadline != 0 && saved.deadline != 12 && saved.deadline != 30 ||
                saved.energy < 0 || saved.energy > ResourceCap || saved.mood < 0 ||
                saved.mood > ResourceCap || saved.insight < 0 || saved.insight > ResourceCap ||
                saved.rulesVersion > RulesVersion || saved.rulesVersion < 0 || saved.catalogVersion > CardCatalog.CurrentVersion || saved.catalogVersion < 0)
                throw new ArgumentException("Invalid run snapshot.", "saved");
            int catalogVersion = saved.catalogVersion > 0 ? saved.catalogVersion : saved.rulesVersion < 4 ? 1 :
                saved.rulesVersion < 5 ? 2 : saved.rulesVersion < 6 ? 3 : 4;
            if (saved.rulesVersion < 2) MigrateLegacyResources(saved);
            if (saved.rulesVersion < 3 || saved.causalNodes == null ||
                (saved.causalNodes.Count == 0 && saved.actions != null && saved.actions.Count > 0))
            {
                saved.causalNodes = RebuildGraph(saved.actions, saved.pending, deadline);
                saved.rulesVersion = RulesVersion;
            }
            if (saved.relation < 0 || saved.relation > ResourceCap ||
                saved.money < 0 || saved.money > ResourceCap ||
                saved.ability < 0 || saved.ability > ResourceCap)
                throw new ArgumentException("Invalid extended resources.", "saved");
            var session = new GameSession(saved.runNumber, simulation, catalogVersion, saved.worldSeed)
            {
                DeckSeed = saved.deckSeedRecorded ? saved.deckSeed : saved.worldSeed,
                Deadline = deadline,
                Day = saved.day, Energy = saved.energy, Mood = saved.mood,
                Insight = saved.insight, Relation = saved.relation,
                Money = saved.money, Ability = saved.ability,
                SupportActions = saved.supportActions,
                FocusUses = saved.focusUses, HasChosen = saved.hasChosen,
                StationVisited = saved.stationVisited, SocialUnavailableToday = saved.socialUnavailableToday,
                Prediction = ValidPrediction(saved.prediction, deadline) ? saved.prediction : null,
                PredictionSkipped = saved.predictionSkipped
            };
            session.RestoreDecisions(saved);
            if (saved.actions != null) session.Actions.AddRange(saved.actions);
            if (saved.pending != null) session.Pending.AddRange(saved.pending);
            session.CausalNodes.AddRange(saved.causalNodes);
            if (saved.mysteries != null) session.Mysteries.AddRange(saved.mysteries);
            if (session.UsesMasterRules && saved.master != null)
            { saved.master.Validate(saved.day, deadline); session.Master = saved.master.Copy(); }
            if (simulation && deadline > (saved.deadline > 0 ? saved.deadline : LastDay))
                foreach (ActionRecord action in session.Actions)
                    if (!action.echoed && action.echoDay > (saved.deadline > 0 ? saved.deadline : LastDay) &&
                        action.echoDay > session.Day && action.echoDay <= deadline &&
                        !session.Pending.Exists(e => e.sourceDay == action.day && e.depth == 1))
                    {
                        CausalNode node = AddNode(session.CausalNodes, action.nodeId, CausalNodeKind.Echo,
                            action.echoDay, action.echoName, action.cardId, false);
                        session.Pending.Add(new PendingEcho { sourceDay = action.day, dueDay = action.echoDay,
                            cardId = action.cardId, cardName = action.cardName, echoName = action.echoName,
                            delta = action.later, kind = action.kind, depth = 1,
                            nodeId = node.id, parentNodeId = action.nodeId });
                    }
            session.EnsureSituation();
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
                master = Master?.Copy(),
                rulesVersion = RulesVersion, catalogVersion = CatalogVersion, worldSeed = WorldSeed,
                deckSeed = DeckSeed, deckSeedRecorded = true, deadline = Deadline,
                day = Day, runNumber = RunNumber,
                energy = Energy, mood = Mood, insight = Insight,
                relation = Relation, money = Money, ability = Ability,
                supportActions = SupportActions, focusUses = FocusUses,
                hasChosen = HasChosen, stationVisited = StationVisited,
                socialUnavailableToday = SocialUnavailableToday,
                prediction = Prediction, predictionSkipped = PredictionSkipped,
                predictions = new List<PredictionRecord>(Predictions),
                skippedPredictionDays = new List<int>(SkippedPredictionDays), stationDays = new List<int>(StationDays),
                actions = new List<ActionRecord>(Actions),
                pending = new List<PendingEcho>(Pending),
                causalNodes = new List<CausalNode>(CausalNodes),
                mysteries = new List<MysteryRecord>(Mysteries)
            };
        }

        // Older six-resource saves retain their actual effects. Reconstruct only the
        // missing links; pending events keep their original due dates and deltas.
        private static List<CausalNode> RebuildGraph(List<ActionRecord> actions, List<PendingEcho> pending, int length = LastDay)
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
                if (action.echoDay > action.day && action.echoDay <= length)
                {
                    CausalNode echo = AddNode(nodes, origin.id, CausalNodeKind.Echo,
                        action.echoDay, action.echoName, action.cardId, action.echoed);
                    if (pending != null)
                        foreach (PendingEcho item in pending)
                            if (item != null && item.depth == 1 && item.sourceDay == action.day)
                            { item.nodeId = echo.id; item.parentNodeId = origin.id; }
                    if (action.secondaryDay > action.echoDay && action.secondaryDay <= length)
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
                run.causalNodes : RebuildGraph(run.actions, null, RunLength(run));
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
            if (!CanPredict) throw new InvalidOperationException("No prediction is offered now.");
            if (Math.Abs(energy) > 3 || Math.Abs(mood) > 3 || Math.Abs(insight) > 3)
                throw new ArgumentOutOfRangeException("Prediction offsets must be between -3 and 3.");
            LockPrediction(new ResourceDelta(energy, mood, insight), 3);
        }

        public void SkipPrediction()
        {
            if (!CanPredict) throw new InvalidOperationException("No prediction is offered now.");
            SkippedPredictionDays.Add(Day);
            if (Day == 4) PredictionSkipped = true;
        }

        public void VisitStation()
        {
            if (!NeedsStation) throw new InvalidOperationException("No future station is waiting.");
            StationDays.Add(Day);
            if (Day == 4) StationVisited = true;
        }

        public void MarkPredictionReviewed()
        {
            if (!HasPredictionReview) throw new InvalidOperationException("No prediction is ready to review.");
            Prediction.reviewed = true;
            RefreshPredictionCursor();
        }

        public bool CanPlay(CardSpec card)
        {
            return card != null && Array.Exists(Hand, candidate => candidate.Id == card.Id) &&
                MasterAllows(card) && !NeedsImagination(card) &&
                !CanPredict && !HasPredictionReview && !HasChosen && CompletedRun == null &&
                CanAfford(card);
        }

        public bool CanAfford(CardSpec card)
        {
            if (card == null) return false;
            ResourceDelta cost = ImmediateEffect(card);
            return Energy + cost.energy >= 0 && Mood + cost.mood >= 0 &&
                Insight + cost.insight >= 0 && Relation + cost.relation >= 0 &&
                Money + cost.money >= 0 && Ability + cost.ability >= 0;
        }

        public bool TryFocus()
        {
            if (FocusUses >= 1 || HasPredictionReview || HasChosen || CompletedRun != null) return false;
            FocusUses++;
            return true;
        }

        // A conditional view, not a promise: future choices and newly formed chains are unknown.
        public FutureProjection ProjectFuture(string cardId, bool observationUnlocked = false)
        {
            if ((!observationUnlocked && HorizonLevel < 3) || HasChosen || CanPredict || HasPredictionReview || CompletedRun != null)
                throw new InvalidOperationException("Two futures unlock after the third station.");
            CardSpec card = Array.Find(Hand, c => c.Id == cardId);
            if (card == null) throw new ArgumentException("Card not in today's hand.", "cardId");
            int target = Math.Min(Deadline, Day + 3);
            bool available = CanPlay(card);
            if (!available) return new FutureProjection(card, false, Day, target,
                Energy, Mood, Insight, Relation, Money, Ability);

            ResourceDelta immediate = ImmediateEffect(card);
            int energy = Clamp(Energy + immediate.energy);
            int mood = Clamp(Mood + immediate.mood);
            int insight = Clamp(Insight + immediate.insight);
            int relation = Clamp(Relation + immediate.relation);
            int money = Clamp(Money + immediate.money);
            int ability = Clamp(Ability + immediate.ability);
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

            ResourceDelta before = Values();
            ResourceDelta immediate = ImmediateEffect(card);
            Apply(immediate);
            if (card.GivesSupport) SupportActions++;
            CausalNode changedChoice = CausalNodes.FindLast(n => (n.type == CausalNodeKind.Choice ||
                n.type == CausalNodeKind.Situation || n.type == CausalNodeKind.World) &&
                n.resolved && n.day == Day && n.replacementId == card.Id);
            CausalNode origin = AddNode(CausalNodes, changedChoice == null ? null : changedChoice.id,
                CausalNodeKind.Action, Day, card.Name, card.Id, true);
            origin.effect = Difference(before); origin.effectRecorded = true;
            var action = new ActionRecord
            {
                day = Day, cardId = card.Id, cardName = card.Name, kind = card.Kind,
                echoDay = card.Delay > 0 ? Day + card.Delay : 0,
                echoName = card.EchoName, givesSupport = card.GivesSupport,
                now = immediate, later = card.Later,
                actualNow = origin.effect, actualNowRecorded = true,
                nodeId = origin.id, parentNodeId = origin.parentId,
                parentNodeIds = new List<string>(CausalGraph.Parents(origin))
            };
            Actions.Add(action);
            if (card.Delay > 0 && action.echoDay <= Deadline)
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
            MasterAfterChoice(action, card, before);
            if (Day == Deadline) Complete();
            return action;
        }

        public DayTransition Advance()
        {
            if (!HasChosen || Day == Deadline) throw new InvalidOperationException("Choose before advancing.");
            if (NeedsStation)
                throw new InvalidOperationException("Visit the future station before advancing.");
            Day++;
            RevealMysteryOrigins();
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
                ResourceDelta before = Values();
                if (echo.delta != null) Apply(echo.delta);
                echo.actualDelta = Difference(before);
                CausalNode node = CausalNodes.Find(n => n.id == echo.nodeId);
                if (node != null) { node.resolved = true; node.effect = echo.actualDelta; node.effectRecorded = true; }
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
                    if (source != null)
                    { source.echoed = true; source.actualLater = echo.actualDelta; source.actualLaterRecorded = true; }
                    if (RunNumber >= 3 || CatalogVersion >= 6 && Deadline == 30) ScheduleConsequences(echo, source);
                }
                MasterAfterEcho(echo);
            }
            ResolveWorldEvent();
            MasterAfterAdvance(due);
            EvaluatePredictions();
            EnsureSituation();
            return new DayTransition(Day, due);
        }

        private void EnsureSituation()
        {
            if (RunNumber < 2 && Deadline != 30 || CatalogVersion < 2 || CausalNodes.Exists(n => n.type == CausalNodeKind.Situation && n.day == Day)) return;
            if (Day > LastDay && CatalogVersion >= 3)
            {
                string milestone = Day == 14 ? "project" : Day == 21 ? "collaborate" : Day == 28 ? "publish" : null;
                if (milestone != null)
                {
                    CardSpec next = CardCatalog.FindById(milestone);
                    CausalNode n = AddNode(CausalNodes, null, CausalNodeKind.Situation, Day, next.Name + "的机会到了", next.Id, true);
                    n.replacementId = next.Id; n.replacementSlot = next.Kind;
                }
                return;
            }
            int day = (Day - 1) % LastDay + 1;
            string card = day == 7 ? "shortstudy" : day == 9 ? "smalljob" : day == 10 ? "play" : day == 12 ? "friend" : null;
            if (card == null) return;
            string label = day == 7 ? "午后多了半小时空档" : day == 9 ? "有人请你临时帮忙" :
                day == 10 ? "朋友推荐了一个小游戏" : "朋友约你聊聊今天";
            CausalNode situation = AddNode(CausalNodes, null, CausalNodeKind.Situation, Day, label, card, true);
            situation.replacementId = card;
            situation.replacementSlot = day == 10 ? CardKind.Temptation : day == 12 ? CardKind.Recovery : CardKind.Growth;
        }

        private void ResolveWorldEvent()
        {
            if (CatalogVersion < 3 || RunNumber < 3 && !(CatalogVersion >= 6 && Deadline == 30) || CausalNodes.Exists(n => n.type == CausalNodeKind.World && n.day == Day)) return;
            WorldEventSpec spec = Array.Find(WorldEvents.ForCatalog(CatalogVersion), e => e.Day == Day);
            if (spec == null || !WorldEvents.Occurs(WorldSeed, Day, spec.Chance)) return;
            ResourceDelta before = Values(); Apply(spec.Delta);
            CausalNode world = AddNode(CausalNodes, null, CausalNodeKind.World, Day, spec.Name, spec.ReplacementId, true);
            world.replacementId = spec.ReplacementId; world.replacementSlot = spec.Slot;
            world.effect = Difference(before); world.effectRecorded = true;
        }

        private int NextSupportDay()
        {
            for (int day = Day + 1; day <= Deadline; day++)
                if (((day > LastDay && CatalogVersion >= 3 ? CardCatalog.ForOutlookDay(day, DeckSeed, CatalogVersion) :
                        CardCatalog.ForDay((day - 1) % LastDay + 1, RunNumber, CatalogVersion, DeckSeed))[2].GivesSupport ||
                    CatalogVersion >= 2 && RunNumber >= 2 && (day - 1) % LastDay + 1 == 12) &&
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
                if (day <= Day || day > Deadline || Pending.Exists(e => e.depth >= 2 &&
                    e.dueDay == day && e.replacementSlot == rule.Slot)) continue;
                CausalNode eventNode = AddNode(CausalNodes, cause.nodeId,
                    CausalNodeKind.Choice, day, rule.Label, cause.cardId, false);
                eventNode.replacementId = rule.Replacement.Id;
                eventNode.replacementSlot = rule.Slot;
                if (CatalogVersion >= 4 && rule.Replacement.Id == CardCatalog.Opportunity.Id)
                    foreach (CausalNode hidden in CausalNodes.FindAll(n => n.type == CausalNodeKind.Mystery && n.resolved &&
                        n.day <= Day && n.effectRecorded && n.effect?.insight > 0)) CausalGraph.Link(eventNode, hidden.id);
                if (CatalogVersion >= 2 && rule.Replacement.Id == CardCatalog.Together.Id)
                {
                    CausalNode growth = CausalNodes.FindLast(n => n.type == CausalNodeKind.Echo && n.resolved &&
                        n.day <= Day && CardCatalog.FindById(n.cardId)?.Kind == CardKind.Growth);
                    if (growth != null) CausalGraph.Link(eventNode, growth.id);
                }
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

        private ResourceDelta Values()
        { return new ResourceDelta(Energy, Mood, Insight, Relation, Money, Ability); }

        private ResourceDelta Difference(ResourceDelta before)
        { return new ResourceDelta(Energy - before.energy, Mood - before.mood, Insight - before.insight,
            Relation - before.relation, Money - before.money, Ability - before.ability); }

        private static int Clamp(int value) { return Math.Max(0, Math.Min(ResourceCap, value)); }

        // Reuse the actual rules for a counterfactual; after the changed day, keep the
        // player's recorded choices when legal and use recovery if that path disappeared.
        public static RunRecord ReplayAlternative(RunRecord original, int sourceDay, string alternativeId)
        {
            if (sourceDay < 1 || sourceDay > RunLength(original) || string.IsNullOrEmpty(alternativeId))
                throw new ArgumentException("A valid alternative day and action are required.");
            ValidateReplay(original);
            if (original.actions[sourceDay - 1]?.cardId == alternativeId) return null;
            return ReplayChoices(original, new Dictionary<int, string> { { sourceDay, alternativeId } });
        }

        public static RunRecord ReplayChoices(RunRecord original, Dictionary<int, string> changes)
        {
            ValidateReplay(original);
            if (changes == null || changes.Count > RunLength(original)) throw new ArgumentException("Invalid branch choices.");
            foreach (var change in changes)
                if (change.Key < 1 || change.Key > RunLength(original) || string.IsNullOrEmpty(change.Value))
                    throw new ArgumentException("Invalid branch choice.");
            return ReplayPrefix(original, changes, RunLength(original), true)?.CompletedRun;
        }

        public static CardSpec[] AlternativesForDay(RunRecord original, int day, Dictionary<int, string> changes = null)
        {
            ValidateReplay(original);
            if (day < 1 || day > RunLength(original)) throw new ArgumentOutOfRangeException("day");
            GameSession prefix = ReplayPrefix(original, changes ?? new Dictionary<int, string>(), day, false);
            return prefix == null ? new CardSpec[0] : Array.FindAll(prefix.Hand, prefix.CanPlay);
        }

        private static void ValidateReplay(RunRecord original)
        {
            if (original == null || original.actions == null || original.actions.Count != RunLength(original) || original.number < 1)
                throw new ArgumentException("A complete archived life is required.");
        }

        private static GameSession ReplayPrefix(RunRecord original, Dictionary<int, string> changes, int stopDay, bool chooseLast)
        {
            var replay = new GameSession(original.number, true, original.catalogVersion > 0 ? original.catalogVersion : 1, original.worldSeed);
            replay.Deadline = RunLength(original);
            replay.DeckSeed = original.deckSeedRecorded ? original.deckSeed : original.worldSeed;
            if (replay.UsesMasterRules && original.master != null)
                replay.InitializeMaster(original.master.mode, original.master.initialPatterns, original.master.initialMemories, original.master.initialFailures, original.master.initialInsightPoints, original.master.initialKnowledge);
            int firstChange = replay.Deadline + 1;
            foreach (int day in changes.Keys) firstChange = Math.Min(firstChange, day);
            for (int day = 1; day <= stopDay; day++)
            {
                if (original.mysteries != null)
                    foreach (MysteryRecord mystery in original.mysteries)
                        if (mystery.day == day) replay.ApplyMystery(mystery.sourceDay, mystery.sourceCardId,
                            mystery.delta, mystery.delta == null);
                while (replay.HasPredictionReview) replay.MarkPredictionReviewed();
                if (replay.CanPredict)
                {
                    PredictionRecord prediction = original.predictions?.Find(p => p.sourceDay == day) ??
                        (original.prediction?.sourceDay == day ? original.prediction : null);
                    if (prediction == null) replay.SkipPrediction();
                    else replay.LockPrediction(new ResourceDelta(prediction.energy, prediction.mood, prediction.insight,
                        prediction.relation, prediction.money, prediction.ability), prediction.dueDay - prediction.sourceDay);
                }
                if (day == stopDay && !chooseLast) return replay;
                replay.ReplayMasterCommands(original, day);
                ActionRecord recorded = original.actions[day - 1];
                if (recorded == null) return null;
                bool explicitChoice = changes.TryGetValue(day, out string desired);
                if (!explicitChoice) desired = recorded.cardId;
                if (replay.InExecutionMode && desired != replay.Master.decision.cardId) replay.UnlockDecision();
                CardSpec choice = Array.Find(replay.Hand, c => c.Id == desired);
                if (choice == null || !replay.CanPlay(choice))
                {
                    if (explicitChoice || day < firstChange) return null;
                    choice = replay.Hand[2];
                    if (!replay.CanPlay(choice)) return null;
                }
                replay.Choose(choice.Id);
                if (replay.NeedsStation) replay.VisitStation();
                if (day < stopDay) replay.Advance();
            }
            return replay;
        }

        private static int ClampForecast(int value) { return Math.Max(-3, Math.Min(3, value)); }

        private static int Deficit(RunRecord run, BossResult baseline)
        {
            int gap = 0;
            int needed = RunLength(run) == 30 ? 3 : 2;
            if (!baseline.ability) gap += Math.Max(0, AbilityGate - run.finalAbility);
            if (!baseline.ability && run.catalogVersion >= 2) gap += Math.Max(0, needed - run.boss.growthEchoes);
            if (!baseline.state) gap += Math.Max(0, 4 - run.finalEnergy) + Math.Max(0, 4 - run.finalMood);
            if (!baseline.state && run.catalogVersion >= 2) gap += Math.Max(0, needed - run.boss.recoveries);
            if (!baseline.support)
            {
                int supports = run.boss.supports;
                gap += Math.Max(0, needed - supports) +
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
            for (int day = 1; day <= RunLength(original); day++)
            {
                foreach (CardSpec card in AlternativesForDay(original, day))
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
                    for (int later = day + 1; later <= RunLength(original); later++)
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
                        echoDay = changed.echoDay <= RunLength(original) ? changed.echoDay : 0,
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
            int growthEchoes = ProductExperience.GrowthEvidence(this);
            int recoveries = ProductExperience.RecoveryEvidence(this);
            var boss = new BossResult
            {
                ability = ProductExperience.GateReady(this, 0),
                state = ProductExperience.GateReady(this, 1),
                support = ProductExperience.GateReady(this, 2),
                growthEchoes = growthEchoes, recoveries = recoveries, supports = ProductExperience.SupportEvidence(this)
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
            if (CatalogVersion >= 2)
            {
                List<int>[] evidence = { boss.abilityDays, boss.stateDays, boss.supportDays };
                string[] names = { "能力", "状态", "支援" };
                bool[] passed = { boss.ability, boss.state, boss.support };
                for (int gate = 0; gate < 3; gate++)
                {
                    CausalNode node = AddNode(CausalNodes, null, CausalNodeKind.Gate, Day,
                        names[gate] + "门 · " + (passed[gate] ? "点亮" : "还差一点"), "", true);
                    node.gatePassed = passed[gate];
                    foreach (int sourceDay in evidence[gate])
                    {
                        ActionRecord source = Actions.Find(a => a.day == sourceDay);
                        if (source == null) continue;
                        CausalNode echo = CausalNodes.Find(n => n.parentId == source.nodeId &&
                            n.type == CausalNodeKind.Echo && n.resolved);
                        CausalGraph.Link(node, echo != null ? echo.id : source.nodeId);
                    }
                    foreach (CausalNode mystery in CausalNodes.FindAll(n => n.type == CausalNodeKind.Mystery &&
                        n.resolved && n.effectRecorded && n.effect != null))
                        if (gate == 0 && mystery.effect.ability > 0 || gate == 1 &&
                            (mystery.effect.energy > 0 || mystery.effect.mood > 0)) CausalGraph.Link(node, mystery.id);
                }
            }
            MasterComplete(boss);
            CompletedRun = new RunRecord
            {
                master = Master?.Copy(),
                catalogVersion = CatalogVersion, deadline = Deadline, worldSeed = WorldSeed, deckSeed = DeckSeed, deckSeedRecorded = true,
                number = RunNumber,
                boss = boss,
                actions = new List<ActionRecord>(Actions),
                finalEnergy = Energy, finalMood = Mood, finalInsight = Insight,
                finalRelation = Relation, finalMoney = Money, finalAbility = Ability,
                prediction = Prediction, predictions = new List<PredictionRecord>(Predictions),
                skippedPredictionDays = new List<int>(SkippedPredictionDays), stationDays = new List<int>(StationDays),
                causalNodes = new List<CausalNode>(CausalNodes),
                mysteries = new List<MysteryRecord>(Mysteries)
            };
            CompletedRun.title = ExperienceContent.LifeTitle(CompletedRun);
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
