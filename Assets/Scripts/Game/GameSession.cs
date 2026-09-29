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
        public bool socialUnavailableToday;
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

        public FutureProjection(CardSpec card, bool available, int sourceDay, int targetDay,
            int energy, int mood, int insight)
        {
            CardName = card.Name;
            Available = available;
            TargetDay = targetDay;
            EchoDay = card.Delay > 0 ? sourceDay + card.Delay : 0;
            EchoName = card.EchoName;
            Energy = energy;
            Mood = mood;
            Insight = insight;
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
        public bool SocialUnavailableToday { get; private set; }
        public PredictionRecord Prediction { get; private set; }
        public bool CanPredict { get { return Day == 4 && !HasChosen && Prediction == null; } }
        public bool HasPredictionReview { get { return Prediction != null && Prediction.evaluated && !Prediction.reviewed; } }
        public RunRecord CompletedRun { get; private set; }
        public readonly List<ActionRecord> Actions = new List<ActionRecord>();
        public readonly List<PendingEcho> Pending = new List<PendingEcho>();

        public int HorizonLevel
        {
            get { return RunNumber >= 4 || (RunNumber == 3 && StationVisited) ? 3 : Math.Min(2, RunNumber); }
        }
        public CardSpec[] Hand
        {
            get
            {
                CardSpec[] hand = CardCatalog.ForDay(Day, RunNumber);
                if (SocialUnavailableToday && hand[2].GivesSupport)
                    hand[2] = CardCatalog.SoloRecovery;
                return hand;
            }
        }

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
                StationVisited = saved.stationVisited, SocialUnavailableToday = saved.socialUnavailableToday,
                Prediction = saved.prediction
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
                socialUnavailableToday = SocialUnavailableToday,
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
            return card != null && Array.Exists(Hand, candidate => candidate.Id == card.Id) &&
                !CanPredict && !HasPredictionReview && !HasChosen && CompletedRun == null &&
                Energy + card.Now.energy >= 0 && Mood + card.Now.mood >= 0 &&
                Insight + card.Now.insight >= 0;
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
            if (!available) return new FutureProjection(card, false, Day, target, Energy, Mood, Insight);

            int energy = Clamp(Energy + card.Now.energy);
            int mood = Clamp(Mood + card.Now.mood);
            int insight = Clamp(Insight + card.Now.insight);
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
            }
            return new FutureProjection(card, true, Day, target, energy, mood, insight);
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
                echoName = card.EchoName, givesSupport = card.GivesSupport,
                now = card.Now, later = card.Later
            };
            Actions.Add(action);
            if (card.Delay > 0 && action.echoDay <= LastDay)
            {
                Pending.Add(new PendingEcho
                {
                    sourceDay = Day, dueDay = action.echoDay, cardId = card.Id,
                    cardName = card.Name, echoName = card.EchoName,
                    delta = card.Later, kind = card.Kind, depth = 1
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
                Apply(echo.delta);
                ActionRecord source = Actions.Find(a => a.day == echo.sourceDay);
                if (echo.depth >= 2)
                {
                    SocialUnavailableToday = true;
                    if (source != null) source.secondaryResolved = true;
                }
                else
                {
                    if (source != null) source.echoed = true;
                    if (RunNumber >= 3 && echo.kind == CardKind.Temptation &&
                        echo.delta != null && echo.delta.energy < 0 && Energy <= 2)
                        ScheduleMissedInvitation(echo, source);
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

        private void ScheduleMissedInvitation(PendingEcho cause, ActionRecord source)
        {
            if (source == null || source.secondaryDay > 0) return;
            for (int day = Day + 1; day <= LastDay; day++)
            {
                if (!CardCatalog.ForDay(day, RunNumber)[2].GivesSupport) continue;
                // A day can lose its social invitation only once, even if several echoes arrive together.
                if (Pending.Exists(e => e.depth >= 2 && e.dueDay == day)) continue;
                var loss = new ResourceDelta(0, -1);
                source.secondaryDay = day;
                source.secondaryName = "错过了一次邀约";
                source.secondary = loss;
                Pending.Add(new PendingEcho
                {
                    sourceDay = cause.sourceDay, parentDay = Day, dueDay = day,
                    cardId = cause.cardId, cardName = cause.cardName,
                    echoName = source.secondaryName, delta = loss,
                    kind = CardKind.Temptation, depth = 2
                });
                return;
            }
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
