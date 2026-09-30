using System;
using System.Linq;
using UnityEngine;

namespace Horizon.Game
{
    public sealed class ForecastRange
    {
        public int targetDay;
        public int samples;
        public int energyMin, energyMax, moodMin, moodMax, abilityMin, abilityMax;
        public int abilityPass, statePass, supportPass;
        public string assumption;
    }

    // These frequencies describe sampled follow-up choices, not a hidden chance
    // that an already planted echo will arrive. Forks never mutate the real save.
    public static class ForecastSimulator
    {
        public static ForecastRange Sample(GameSession source, string firstCard, int targetDay, int samples = 24)
        {
            if (source == null || source.HasChosen || source.CompletedRun != null ||
                targetDay <= source.Day || targetDay > 30 || samples < 3 || samples > 96)
                throw new ArgumentException("An unplayed day and a future horizon up to thirty days are required.");
            string frozen = JsonUtility.ToJson(source.Snapshot());
            var result = new ForecastRange { targetDay = targetDay, samples = samples,
                energyMin = 10, moodMin = 10, abilityMin = 10,
                assumption = "比较多种后续选择；已埋下的回声按原规则兑现。" };
            for (int sample = 0; sample < samples; sample++)
            {
                GameSession fork = GameSession.ForkForSimulation(JsonUtility.FromJson<RunSnapshot>(frozen), Math.Max(12, targetDay));
                var random = new System.Random(source.RunNumber * 7919 + source.Day * 101 + sample * 17);
                while (true)
                {
                    if (fork.HasPredictionReview) fork.MarkPredictionReviewed();
                    if (fork.CanPredict) fork.SkipPrediction();
                    CardSpec[] available = fork.Hand.Where(fork.CanPlay).ToArray();
                    CardSpec choice = fork.Day == source.Day && !string.IsNullOrEmpty(firstCard) ?
                        available.FirstOrDefault(c => c.Id == firstCard) : null;
                    if (fork.Day == source.Day && !string.IsNullOrEmpty(firstCard) && choice == null)
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
                result.abilityMin = Math.Min(result.abilityMin, fork.Ability); result.abilityMax = Math.Max(result.abilityMax, fork.Ability);
                if (fork.Ability >= 6 && (fork.CatalogVersion < 2 || fork.Actions.Count(a => a.echoed && a.later?.ability > 0) >= 2)) result.abilityPass++;
                if (fork.Energy >= 4 && fork.Mood >= 4 && (fork.CatalogVersion < 2 || fork.Actions.Count(a => a.kind == CardKind.Recovery) >= 2)) result.statePass++;
                if (fork.SupportActions >= 2 && fork.Relation >= 6 && fork.Money >= 2) result.supportPass++;
            }
            return result;
        }
    }
}
