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
    }

    [Serializable]
    public sealed class BossResult
    {
        public bool ability;
        public bool state;
        public bool support;
        public int passed;
        public string ghost;
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
        public PredictionRecord prediction;
    }

    [Serializable]
    public sealed class RunSnapshot
    {
        public int day;
        public int runNumber;
        public int energy;
        public int mood;
        public int insight;
        public int supportActions;
        public int focusUses;
        public bool hasChosen;
        public bool stationVisited;
        public PredictionRecord prediction;
        public List<ActionRecord> actions = new List<ActionRecord>();
        public List<PendingEcho> pending = new List<PendingEcho>();
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

    public sealed class GameSession
    {
        public const int LastDay = 12;
        public const int ResourceCap = 10;

        public int Day { get; private set; }
        public int RunNumber { get; private set; }
        public int Energy { get; private set; }
        public int Mood { get; private set; }
        public int Insight { get; private set; }
        public int SupportActions { get; private set; }
        public int FocusUses { get; private set; }
        public bool HasChosen { get; private set; }
        public bool StationVisited { get; private set; }
        public PredictionRecord Prediction { get; private set; }
        public bool CanPredict { get { return Day == 4 && !HasChosen && Prediction == null; } }
        public bool HasPredictionReview { get { return Prediction != null && Prediction.evaluated && !Prediction.reviewed; } }
        public RunRecord CompletedRun { get; private set; }
        public readonly List<ActionRecord> Actions = new List<ActionRecord>();
        public readonly List<PendingEcho> Pending = new List<PendingEcho>();

        public int HorizonLevel { get { return Math.Min(3, RunNumber); } }
        public CardSpec[] Hand { get { return CardCatalog.ForDay(Day, RunNumber); } }

        public GameSession(int runNumber)
        {
            if (runNumber < 1) throw new ArgumentOutOfRangeException("runNumber");
            RunNumber = runNumber;
            Day = 1;
            Energy = 6;
            Mood = 5;
            Insight = 2;
        }

        public static GameSession Restore(RunSnapshot saved)
        {
            if (saved == null || saved.runNumber < 1 || saved.day < 1 || saved.day > LastDay ||
                saved.energy < 0 || saved.energy > ResourceCap || saved.mood < 0 ||
                saved.mood > ResourceCap || saved.insight < 0 || saved.insight > ResourceCap)
                throw new ArgumentException("Invalid run snapshot.", "saved");
            var session = new GameSession(saved.runNumber)
            {
                Day = saved.day, Energy = saved.energy, Mood = saved.mood,
                Insight = saved.insight, SupportActions = saved.supportActions,
                FocusUses = saved.focusUses, HasChosen = saved.hasChosen,
                StationVisited = saved.stationVisited, Prediction = saved.prediction
            };
            if (saved.actions != null) session.Actions.AddRange(saved.actions);
            if (saved.pending != null) session.Pending.AddRange(saved.pending);
            return session;
        }

        public RunSnapshot Snapshot()
        {
            return new RunSnapshot
            {
                day = Day, runNumber = RunNumber, energy = Energy, mood = Mood,
                insight = Insight, supportActions = SupportActions, focusUses = FocusUses,
                hasChosen = HasChosen, stationVisited = StationVisited,
                prediction = Prediction, actions = new List<ActionRecord>(Actions),
                pending = new List<PendingEcho>(Pending)
            };
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
            return !CanPredict && !HasPredictionReview && !HasChosen && CompletedRun == null &&
                Energy + card.Now.energy >= 0 && Mood + card.Now.mood >= 0 &&
                Insight + card.Now.insight >= 0;
        }

        public bool TryFocus()
        {
            if (FocusUses >= 1 || HasPredictionReview || HasChosen || CompletedRun != null) return false;
            FocusUses++;
            return true;
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
            var action = new ActionRecord
            {
                day = Day, cardId = card.Id, cardName = card.Name, kind = card.Kind,
                echoDay = card.Delay > 0 ? Day + card.Delay : 0,
                echoName = card.EchoName, givesSupport = card.GivesSupport
            };
            Actions.Add(action);
            if (card.Delay > 0 && action.echoDay <= LastDay)
            {
                Pending.Add(new PendingEcho
                {
                    sourceDay = Day, dueDay = action.echoDay, cardId = card.Id,
                    cardName = card.Name, echoName = card.EchoName,
                    delta = card.Later, kind = card.Kind
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
            var due = new List<PendingEcho>();
            for (int i = Pending.Count - 1; i >= 0; i--)
            {
                if (Pending[i].dueDay != Day) continue;
                due.Add(Pending[i]);
                Pending.RemoveAt(i);
            }
            due.Sort((a, b) => a.sourceDay.CompareTo(b.sourceDay));
            foreach (PendingEcho echo in due)
            {
                Apply(echo.delta);
                ActionRecord source = Actions.Find(a => a.day == echo.sourceDay);
                if (source != null) source.echoed = true;
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

        private void Apply(ResourceDelta delta)
        {
            Energy = Clamp(Energy + delta.energy);
            Mood = Clamp(Mood + delta.mood);
            Insight = Clamp(Insight + delta.insight);
        }

        private static int Clamp(int value) { return Math.Max(0, Math.Min(ResourceCap, value)); }

        private void Complete()
        {
            var boss = new BossResult
            {
                ability = Insight >= 7,
                state = Energy >= 4 && Mood >= 4,
                support = SupportActions >= 2
            };
            boss.passed = (boss.ability ? 1 : 0) + (boss.state ? 1 : 0) + (boss.support ? 1 : 0);
            boss.ghost = !boss.ability ? "如果更早种下一颗成长的种子，能力之门会怎样？" :
                !boss.state ? "如果某一天先休息，状态之门会怎样？" :
                !boss.support ? "如果给一个朋友发消息，支援之门会怎样？" : "你留下的路，已经连成了星图。";
            string definingAction = Actions.FindLast(a => a.kind == CardKind.Growth)?.cardName ?? Actions[0].cardName;
            CompletedRun = new RunRecord
            {
                number = RunNumber,
                title = boss.passed == 3 ? "我把未来接住了" :
                    boss.passed == 0 ? "这一次，我看见了另一条路" : "从「" + definingAction + "」开始的日子",
                boss = boss,
                actions = new List<ActionRecord>(Actions),
                finalEnergy = Energy, finalMood = Mood, finalInsight = Insight,
                prediction = Prediction
            };
        }
    }
}
