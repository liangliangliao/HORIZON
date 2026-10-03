using System;
using System.Collections.Generic;
using System.Linq;

namespace Horizon.Game
{
    public sealed partial class GameSession
    {
        public static int RunLength(RunRecord run) { return run?.deadline == 30 ? 30 : LastDay; }
        public static GameSession StartLongLife(int number, int seed)
        { return new GameSession(number, seed) { Deadline = 30 }; }
        public bool UsesSixPredictionAxes { get { return CatalogVersion >= 6 && (RunNumber >= 3 || Deadline == 30); } }
        private bool IsPredictionDay
        { get { return Day == 4 || CatalogVersion >= 6 && (RunNumber >= 2 || Deadline == 30) &&
            (Day == 8 || Deadline == 30 && (Day == 14 || Day == 21 || Day == 27)); } }
        public bool NeedsStation
        { get { return HasChosen && IsStationDay(Day) && !StationDays.Contains(Day) && !(Day == 4 && StationVisited); } }
        private bool IsStationDay(int day)
        { return day == 4 || Deadline == 30 && (day == 12 || day == 14 || day == 21 || day == 28); }

        public void LockPrediction(ResourceDelta expected, int days)
        {
            if (!CanPredict) throw new InvalidOperationException("No prediction is offered now.");
            if (expected == null || (days != 1 && days != 3 && days != 7) || Day + days > Deadline ||
                CatalogVersion < 6 && days != 3 ||
                new[] { expected.energy, expected.mood, expected.insight, expected.relation, expected.money, expected.ability }
                    .Any(v => Math.Abs(v) > 3) || !UsesSixPredictionAxes &&
                    (expected.relation != 0 || expected.money != 0 || expected.ability != 0))
                throw new ArgumentException("Prediction must use available axes and an in-life horizon.");
            Prediction = new PredictionRecord {
                sourceDay = Day, dueDay = Day + days, baseEnergy = Energy, baseMood = Mood, baseInsight = Insight,
                baseRelation = Relation, baseMoney = Money, baseAbility = Ability,
                energy = expected.energy, mood = expected.mood, insight = expected.insight,
                relation = expected.relation, money = expected.money, ability = expected.ability,
                sixAxes = UsesSixPredictionAxes
            };
            Predictions.Add(Prediction);
            MasterPredictionLocked();
        }

        private void RefreshPredictionCursor()
        {
            PredictionRecord waiting = Predictions.Where(p => p.evaluated && !p.reviewed).OrderBy(p => p.dueDay).FirstOrDefault();
            if (waiting != null) Prediction = waiting;
        }
        private void EvaluatePredictions()
        {
            foreach (PredictionRecord p in Predictions.Where(p => !p.evaluated && Day >= p.dueDay))
            {
                p.actualEnergy = Energy - p.baseEnergy; p.actualMood = Mood - p.baseMood; p.actualInsight = Insight - p.baseInsight;
                p.actualRelation = Relation - p.baseRelation; p.actualMoney = Money - p.baseMoney; p.actualAbility = Ability - p.baseAbility;
                int distance = Math.Abs(p.energy - p.actualEnergy) + Math.Abs(p.mood - p.actualMood) + Math.Abs(p.insight - p.actualInsight);
                if (p.sixAxes) distance += Math.Abs(p.relation - p.actualRelation) + Math.Abs(p.money - p.actualMoney) + Math.Abs(p.ability - p.actualAbility);
                p.accurate = distance <= (p.sixAxes ? 4 : 2); p.evaluated = true;
                MasterPredictionEvaluated(p);
            }
            RefreshPredictionCursor();
        }

        public static bool ValidPrediction(PredictionRecord p, int deadline)
        {
            if (p == null || p.sourceDay < 1 || p.dueDay > deadline) return false;
            int span = p.dueDay - p.sourceDay;
            return (span == 1 || span == 3 || span == 7) &&
                new[] { p.energy, p.mood, p.insight, p.relation, p.money, p.ability }.All(v => v >= -3 && v <= 3);
        }

        private void RestoreDecisions(RunSnapshot saved)
        {
            foreach (PredictionRecord p in saved.predictions ?? new List<PredictionRecord>())
                if (ValidPrediction(p, Deadline) && p.sourceDay <= Day && !Predictions.Exists(x => x.sourceDay == p.sourceDay))
                    Predictions.Add(p);
            if (Prediction != null)
            {
                PredictionRecord match = Predictions.Find(p => p.sourceDay == Prediction.sourceDay);
                if (match != null) Prediction = match; else Predictions.Add(Prediction);
            }
            foreach (int day in saved.skippedPredictionDays ?? new List<int>())
                if (day > 0 && day <= Day && !SkippedPredictionDays.Contains(day)) SkippedPredictionDays.Add(day);
            if (saved.predictionSkipped && !SkippedPredictionDays.Contains(4)) SkippedPredictionDays.Add(4);
            foreach (int day in saved.stationDays ?? new List<int>())
                if (day <= Day && IsStationDay(day) && !StationDays.Contains(day)) StationDays.Add(day);
            if (saved.stationVisited && !StationDays.Contains(4)) StationDays.Add(4);
            RefreshPredictionCursor();
        }
    }
}
