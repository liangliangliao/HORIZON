using System;
using System.Collections.Generic;
using System.Linq;
using Horizon.Game;

namespace Horizon
{
    [Serializable]
    public sealed partial class ArchiveData
    {
        public List<RunRecord> runs = new List<RunRecord>();
        public RunSnapshot active;
        public bool seenFirstEcho;
        public bool seenSecondLife;
        public int calibrations;
        public string preferredIntent;
        public string preferredCardId;
        public RewardWallet wallet = new RewardWallet();
        public FeedbackRecord pendingFeedback;
        public JourneyProgress journey = new JourneyProgress();
        public RareMoment pendingMoment;
        public List<RareMoment> moments = new List<RareMoment>();
        public int nextRareRun = 3;
        public int stationRun;
        public int stationBeat;
        public bool stationMemoryOpen, ghostOpen;
        public int stationMemoryIndex, stationMemoryBeat, ghostRun, ghostBeat;
        public PlayerPreferences preferences = new PlayerPreferences();

        public void Repair()
        {
            if (runs == null) runs = new List<RunRecord>();
            if (wallet == null) wallet = new RewardWallet();
            if (preferences == null || preferences.version < 1) preferences = new PlayerPreferences();
            wallet.Repair();
            if (journey == null) journey = new JourneyProgress();
            journey.Repair();
            RepairMasterArchive();
            if (runs.Any(r => r != null && r.number >= 3)) journey.learnedStage = Math.Max(6, journey.learnedStage);
            if (moments == null) moments = new List<RareMoment>();
            if (nextRareRun < 3) nextRareRun = 3;
            // Unity can deserialize a null nested class as an empty instance.
            if (active != null && active.runNumber < 1) active = null;
            if (pendingMoment != null && (active == null || pendingMoment.runNumber != active.runNumber || pendingMoment.day < 1))
                pendingMoment = null;
            if (pendingFeedback != null && (pendingFeedback.runNumber < 1 ||
                (pendingFeedback.kind == FeedbackKind.Deadline ? runs.Count == 0 : active == null)))
                pendingFeedback = null;
            if (active == null || stationRun != active.runNumber || stationBeat != 1) stationMemoryOpen = false;
            if (pendingFeedback == null || pendingFeedback.kind != FeedbackKind.Deadline ||
                !runs.Any(r => r != null && r.number == ghostRun)) ghostOpen = false;
            foreach (RunRecord run in runs)
            {
                if (!GameSession.ValidPrediction(run.prediction, GameSession.RunLength(run))) run.prediction = null;
                if (run.boss != null && run.boss.ghostTimeline != null && run.boss.ghostTimeline.sourceDay < 1)
                    run.boss.ghostTimeline = null;
            }
        }
    }

}
