using System;
using System.Collections.Generic;
using System.Linq;

namespace Horizon.Game
{
    [Serializable]
    public sealed class RewardMemento
    {
        public string id, eventId, nodeId, title, detail;
        public int day, run;
        public RewardObjectKind kind;
        public List<RewardEvidence> evidence = new List<RewardEvidence>();
        public List<MemoryFrame> frames = new List<MemoryFrame>();
    }
    [Serializable]
    public sealed class RewardCollection
    {
        public int version = 1;
        public List<RewardMemento> items = new List<RewardMemento>();
        public void Repair()
        {
            if (items == null) items = new List<RewardMemento>();
            items = items.Where(x => x != null && !string.IsNullOrEmpty(x.id)).GroupBy(x => x.id).Select(g => g.First()).ToList();
        }
        public bool Capture(DomainEvent e, int run)
        {
            if (e == null || string.IsNullOrEmpty(e.id) || !RewardDirector.IsMilestone(e.kind) || items.Any(x => x.eventId == e.id)) return false;
            RewardObjectKind? kind = RewardDirector.ObjectFor(e.kind); if (!kind.HasValue) return false;
            items.Add(new RewardMemento { id = "memento:" + e.id, eventId = e.id, nodeId = e.nodeId, title = e.title,
                detail = e.detail, day = e.day, run = run, kind = kind.Value,
                frames = MemoryFrame.CopyFrames(e.receipt?.frames),
                evidence = (e.receipt?.causes ?? new List<RewardEvidence>()).Select(x => x.Copy()).ToList() }); return true;
        }
    }
}
