using System;
using System.Collections.Generic;
using System.Linq;
using Horizon.Game;

namespace Horizon
{
    public sealed partial class ArchiveData
    {
        public string productBaseline = MasterSpecification.Version;
        public PlayerBehavioralModel me = new PlayerBehavioralModel();
        public RealityConstellation reality = new RealityConstellation();
        public List<FutureMemory> futureMemories = new List<FutureMemory>();
        public List<string> chapterSeenSetbacks = new List<string>();
        public ImagineRun imagination;
        public List<ImagineRun> imaginedLives = new List<ImagineRun>();
        public List<string> worldview = new List<string>();
        public List<FutureMessage> futureMessages = new List<FutureMessage>();
        public List<KnowledgeSkill> knowledgeSkills = new List<KnowledgeSkill>();
        public AnalyticsLedger analytics = new AnalyticsLedger();
        public AISettings ai = new AISettings();
        public void RepairMasterArchive()
        {
            productBaseline = MasterSpecification.Version;
            if (me == null) me = new PlayerBehavioralModel(); me.Repair();
            if (reality == null) reality = new RealityConstellation(); reality.Repair();
            if (futureMemories == null) futureMemories = new List<FutureMemory>();
            if (chapterSeenSetbacks == null) chapterSeenSetbacks = new List<string>();
            if (imaginedLives == null) imaginedLives = new List<ImagineRun>();
            if (imaginationComparisons == null) imaginationComparisons = new List<ImaginationCalibration>();
            imaginationComparisons.RemoveAll(c => c == null || c.run < 1 || c.plannedDay < 1 || c.plannedDay > 60);
            if (worldview == null) worldview = new List<string>();
            if (futureMessages == null) futureMessages = new List<FutureMessage>();
            if (knowledgeSkills == null) knowledgeSkills = new List<KnowledgeSkill>();
            if (analytics == null || analytics.counts == null) analytics = new AnalyticsLedger();
            if (ai == null) ai = new AISettings(); ai.Repair();
            if (imagination != null && string.IsNullOrEmpty(imagination.id)) imagination = null;
        }
        public void CaptureMaster(GameSession life)
        {
            if (life == null || !life.UsesMasterRules) return;
            ObserveImagination(life);
            me.Observe(life.Master.observations);
            me.foresightPoints = Math.Max(me.foresightPoints, life.Master.insightPoints);
            analytics.Observe(life.RunNumber, life.Master.events);
            foreach (KnowledgeSkill k in life.Master.knowledge)
            { int i = knowledgeSkills.FindIndex(x => x.id == k.id); if (i < 0) knowledgeSkills.Add(k.Copy());
                else if (knowledgeSkills[i].stage < k.stage || knowledgeSkills[i].stage == k.stage && k.stage == KnowledgeStage.Simulate) knowledgeSkills[i] = k.Copy(); }
            foreach (FutureMemory m in life.Master.memories)
            {
                int i = futureMemories.FindIndex(x => x.id == m.id);
                if (i < 0) futureMemories.Add(m.Copy()); else futureMemories[i] = m.Copy();
            }
            if (life.CompletedRun != null)
            {
                life.CompletedRun.master = life.Master.Copy();
                if (!me.observedRuns.Contains(life.RunNumber))
                { me.observedRuns.Add(life.RunNumber); bool failed = life.Master.chapter != null ? life.Master.chapter.outcome != ChapterOutcome.Arrived : life.CompletedRun.boss.passed < 3;
                    me.failedRuns = failed ? Math.Min(9, me.failedRuns + 1) : 0; }
            }
        }
        public void KeepImagination(ImagineRun run)
        {
            run.Validate(); if (run.phase != ImaginePhase.Complete) throw new InvalidOperationException("Finish recovery first.");
            if (imaginedLives.Any(x => x.id == run.id)) return;
            imaginedLives.Add(run.Copy());
            foreach (FutureMemory m in run.memories) if (!futureMemories.Any(x => x.id == m.id)) futureMemories.Add(m.Copy());
        }
    }
}

namespace Horizon.Game
{
    [Serializable]
    public sealed class FutureMessage
    {
        public string id, text, sourceTitle;
        public int day;
    }
    public sealed class ParallelComparison
    {
        public int divergenceDay, firstGates, secondGates;
        public string firstChoice, secondChoice;
    }
    public static class ParallelLives
    {
        public static ParallelComparison Compare(RunRecord a, RunRecord b)
        {
            if (a == null || b == null || a.worldSeed != b.worldSeed || a.catalogVersion != b.catalogVersion ||
                GameSession.RunLength(a) != GameSession.RunLength(b) || a.number != b.number)
                throw new ArgumentException("Parallel lives require the same starting scenario.");
            int day = 0;
            for (int i = 0; i < a.actions.Count; i++) if (a.actions[i].cardId != b.actions[i].cardId) { day = i + 1; break; }
            return new ParallelComparison { divergenceDay = day, firstGates = a.boss.passed, secondGates = b.boss.passed,
                firstChoice = day == 0 ? "相同路径" : a.actions[day - 1].cardName, secondChoice = day == 0 ? "相同路径" : b.actions[day - 1].cardName };
        }
        public static FutureMessage Message(RunRecord run, string text)
        {
            if (run == null || run.boss == null || string.IsNullOrWhiteSpace(text) || text.Length > 200) throw new ArgumentException("A completed life and a short experience are required.");
            return new FutureMessage { id = Guid.NewGuid().ToString("N"), text = text.Trim(), sourceTitle = run.title, day = GameSession.RunLength(run) };
        }
    }
}
