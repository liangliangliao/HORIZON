using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Horizon
{
    public sealed partial class ArchiveData
    {
        public int NextRunNumber
        {
            get { return Math.Max(active?.runNumber ?? 0,
                runs?.Where(r => r != null).Select(r => r.number).DefaultIfEmpty(0).Max() ?? 0) + 1; }
        }

        // One acknowledgement commits both the reviewed flag and knowledge receipt.
        // Calling this again, including twice in one UI frame, is a harmless no-op.
        public bool ReviewPrediction(Game.GameSession life)
        {
            if (life == null || !life.HasPredictionReview) return false;
            bool accurate = life.Prediction.accurate;
            life.MarkPredictionReviewed();
            if (accurate) calibrations++;
            journey.Remember(Game.LifeLesson.Prediction);
            if (!accurate) journey.Remember(Game.LifeLesson.Uncertainty);
            journey.Observe(life);
            active = life.Snapshot();
            return true;
        }

        public Game.GameSession ObservationSource(Game.GameSession live = null)
        {
            if (live != null && (active == null || active.runNumber == live.RunNumber)) return Game.GameSession.ForkForSimulation(
                JsonUtility.FromJson<Game.RunSnapshot>(JsonUtility.ToJson(live.Snapshot())), 30);
            if (active != null) return Game.GameSession.ForkForSimulation(
                JsonUtility.FromJson<Game.RunSnapshot>(JsonUtility.ToJson(active)), 30);
            Game.RunRecord latest = runs?.Where(r => r != null && r.actions?.Count == Game.GameSession.RunLength(r))
                .OrderByDescending(r => r.number).FirstOrDefault();
            if (latest != null) return Game.GameSession.FromRunForObservation(latest);
            // A fresh observation has an explicit, stable seed; it is never a save.
            return new Game.GameSession(NextRunNumber, 15);
        }

        public bool TryThirtyDayObservation(Game.GameSession live, out Game.GameSession source)
        {
            source = null;
            if (journey.Chapter < 7) return false;
            if (active == null) { source = ObservationSource(live); return true; }
            Game.GameSession current = live != null && live.RunNumber == active.runNumber ? live :
                Game.GameSession.Restore(JsonUtility.FromJson<Game.RunSnapshot>(JsonUtility.ToJson(active)));
            if (!current.TryFocus()) return false;
            active = current.Snapshot();
            source = ObservationSource(current);
            return true;
        }

        public void ProtectBehavior(Game.ActionRecord action)
        {
            if (action == null) return;
            preferredCardId = action.cardId;
            preferredIntent = action.kind.ToString();
        }

        public string ConcernName
        {
            get { return Game.CardCatalog.FindById(preferredCardId)?.Name ??
                (preferredIntent == "Growth" ? "成长" : preferredIntent == "Recovery" ? "照顾自己" : "快乐"); }
        }
    }
}

namespace Horizon.Game
{
    public sealed partial class GameSession
    {
        public static GameSession FromRunForObservation(RunRecord run)
        {
            ValidateReplay(run);
            var result = ReplayPrefix(run, new Dictionary<int, string>(), RunLength(run), true);
            if (result == null) throw new ArgumentException("The archived choices cannot be replayed.");
            return result;
        }
    }
}
