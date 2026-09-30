using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Horizon.Game
{
    public sealed class ForecastRange
    {
        public int targetDay;
        public int samples;
        public int energyMin, energyMax, moodMin, moodMax, insightMin, insightMax;
        public int relationMin, relationMax, moneyMin, moneyMax, abilityMin, abilityMax;
        public int abilityPass, statePass, supportPass;
        public string assumption;
        public List<ActionRecord> example = new List<ActionRecord>();
        public List<ActionRecord> otherExample = new List<ActionRecord>();
    }

    // These frequencies describe sampled follow-up choices, not a hidden chance
    // that an already planted echo will arrive. Forks never mutate the real save.
    public static class ForecastSimulator
    {
        public static ForecastRange Sample(GameSession source, string firstCard, int targetDay, int samples = 24)
        {
            if (source == null ||
                targetDay <= source.Day || targetDay > 30 || samples < 3 || samples > 96)
                throw new ArgumentException("A future horizon up to thirty days is required.");
            string frozen = JsonUtility.ToJson(source.Snapshot());
            var result = new ForecastRange { targetDay = targetDay, samples = samples,
                energyMin = 10, moodMin = 10, insightMin = 10,
                relationMin = 10, moneyMin = 10, abilityMin = 10,
                assumption = source.CatalogVersion >= 3 && source.RunNumber >= 3 ?
                    "比较后续行动与尚未发生的天气、邀约；已经发生的事情保持原样。" :
                    "比较多种后续选择；已埋下的回声按原规则兑现。" };
            for (int sample = 0; sample < samples; sample++)
            {
                RunSnapshot snapshot = JsonUtility.FromJson<RunSnapshot>(frozen);
                snapshot.worldSeed = unchecked(source.WorldSeed + (sample + 1) * 104729);
                GameSession fork = GameSession.ForkForSimulation(snapshot, Math.Max(12, targetDay));
                if (fork.HasChosen)
                {
                    if (fork.Day == 4 && !fork.StationVisited) fork.VisitStation();
                    fork.Advance();
                }
                int firstDay = fork.Day;
                var random = new System.Random(source.RunNumber * 7919 + source.Day * 101 + sample * 17);
                while (true)
                {
                    if (fork.HasPredictionReview) fork.MarkPredictionReviewed();
                    if (fork.CanPredict) fork.SkipPrediction();
                    CardSpec[] available = fork.Hand.Where(fork.CanPlay).ToArray();
                    CardSpec choice = fork.Day == firstDay && !string.IsNullOrEmpty(firstCard) ?
                        available.FirstOrDefault(c => c.Id == firstCard) : null;
                    if (fork.Day == firstDay && !string.IsNullOrEmpty(firstCard) && choice == null)
                        throw new ArgumentException("The first card is not playable.");
                    if (choice == null)
                    {
                        CardKind preference = (CardKind)(sample % 3);
                        choice = random.Next(100) < 65 ? available.FirstOrDefault(c => c.Kind == preference) : null;
                        choice = choice ?? available[random.Next(available.Length)];
                    }
                    fork.Choose(choice.Id);
                    if (fork.Day == 4 && !fork.StationVisited) fork.VisitStation();
                    if (fork.Day >= targetDay) break;
                    fork.Advance();
                }
                result.energyMin = Math.Min(result.energyMin, fork.Energy); result.energyMax = Math.Max(result.energyMax, fork.Energy);
                result.moodMin = Math.Min(result.moodMin, fork.Mood); result.moodMax = Math.Max(result.moodMax, fork.Mood);
                result.insightMin = Math.Min(result.insightMin, fork.Insight); result.insightMax = Math.Max(result.insightMax, fork.Insight);
                result.relationMin = Math.Min(result.relationMin, fork.Relation); result.relationMax = Math.Max(result.relationMax, fork.Relation);
                result.moneyMin = Math.Min(result.moneyMin, fork.Money); result.moneyMax = Math.Max(result.moneyMax, fork.Money);
                result.abilityMin = Math.Min(result.abilityMin, fork.Ability); result.abilityMax = Math.Max(result.abilityMax, fork.Ability);
                if (fork.Ability >= 6 && (fork.CatalogVersion < 2 || fork.Actions.Count(a => a.echoed && a.later?.ability > 0) >= 2)) result.abilityPass++;
                if (fork.Energy >= 4 && fork.Mood >= 4 && (fork.CatalogVersion < 2 || fork.Actions.Count(a => a.kind == CardKind.Recovery) >= 2)) result.statePass++;
                if (fork.SupportActions >= 2 && fork.Relation >= 6 && fork.Money >= 2) result.supportPass++;
                if (sample == 1) result.example = new List<ActionRecord>(fork.Actions);
                if (sample == 2) result.otherExample = new List<ActionRecord>(fork.Actions);
            }
            return result;
        }
    }
}
