using System;
using System.Collections.Generic;
using System.Linq;

namespace Horizon.Game
{
    [Serializable]
    public sealed class RealityQuest
    {
        public string id, localDate, goalId, title, memoryId;
        public bool completed;
        public string completedAt;
    }
    [Serializable]
    public sealed class RealityNode
    {
        public string id, label, occurredAt, memoryId;
        public CausalNodeKind kind;
        public List<string> parents = new List<string>();
    }
    [Serializable]
    public sealed class RealityConstellation
    {
        public List<RealityQuest> quests = new List<RealityQuest>();
        public List<RealityNode> nodes = new List<RealityNode>();
        public List<string> convergenceMemories = new List<string>();
        public List<DomainEvent> events = new List<DomainEvent>();
        public void Repair()
        {
            if (quests == null) quests = new List<RealityQuest>(); if (nodes == null) nodes = new List<RealityNode>();
            if (convergenceMemories == null) convergenceMemories = new List<string>(); if (events == null) events = new List<DomainEvent>();
        }
        public RealityQuest Offer(DateTime now, string goalId, string title, string memoryId = null)
        {
            string date = now.ToString("yyyy-MM-dd");
            RealityQuest existing = quests.Find(q => q.localDate == date); if (existing != null) return existing;
            if (string.IsNullOrWhiteSpace(title) || title.Length > 100 || string.IsNullOrWhiteSpace(goalId)) throw new ArgumentException("A small real action is required.");
            var q = new RealityQuest { id = "reality:" + date, localDate = date, goalId = goalId, title = title.Trim(), memoryId = memoryId };
            quests.Add(q); return q;
        }
        public bool Complete(string id, DateTime now, IList<FutureMemory> memories)
        {
            RealityQuest quest = quests.Find(q => q.id == id);
            if (quest == null || quest.completed || quest.localDate != now.ToString("yyyy-MM-dd")) return false;
            FutureMemory m = memories?.FirstOrDefault(x => x.id == quest.memoryId && x.goalId == quest.goalId);
            string stamp = now.ToUniversalTime().ToString("o");
            var real = new RealityNode { id = quest.id, label = quest.title, kind = CausalNodeKind.Reality, occurredAt = stamp, memoryId = m?.id };
            quest.completed = true; quest.completedAt = stamp;
            nodes.Add(real);
            events.Add(new DomainEvent { id = real.id + ":event", kind = DomainEventKind.RealityNode, tier = RewardTier.Major,
                day = 1, nodeId = real.id, title = "REALITY NODE", detail = "Reality Node 已建立。一次亲自确认的现实行动已进入星座。",
                receipt = new RewardReceipt { action = quest.title, causes = new List<RewardEvidence> {
                    new RewardEvidence { id = real.id, label = quest.title, kind = CausalNodeKind.Reality } } } });
            if (m != null && !string.IsNullOrEmpty(m.imaginationNodeId) && !string.IsNullOrEmpty(m.simulationNodeId) && !convergenceMemories.Contains(m.id))
            {
                string imagineId = "imagination:" + m.imaginationRun + ":" + m.imaginationNodeId, simulateId = "simulation:" + m.simulationRun + ":" + m.simulationNodeId;
                if (!nodes.Any(n => n.id == imagineId)) nodes.Add(new RealityNode { id = imagineId, label = m.text, kind = CausalNodeKind.Imagination, memoryId = m.id });
                if (!nodes.Any(n => n.id == simulateId)) nodes.Add(new RealityNode { id = simulateId, label = "D" + m.simulationDay + " · 游戏中的再行动", kind = CausalNodeKind.Action, parents = new List<string> { imagineId }, memoryId = m.id });
                real.parents.Add(simulateId); convergenceMemories.Add(m.id);
                events.Add(new DomainEvent { id = real.id + ":convergence", kind = DomainEventKind.RealityConvergence, tier = RewardTier.Mythic,
                    day = 1, nodeId = real.id, title = "REALITY CONVERGENCE", detail = "你曾经想象。你曾经预演。现在，它真实发生了。",
                    receipt = new RewardReceipt { action = quest.title, causes = new List<RewardEvidence> {
                        new RewardEvidence { id = imagineId, label = m.text, kind = CausalNodeKind.Imagination },
                        new RewardEvidence { id = simulateId, day = m.simulationDay, label = "D" + m.simulationDay + " · 游戏中的再行动", kind = CausalNodeKind.Action },
                        new RewardEvidence { id = real.id, label = quest.title, kind = CausalNodeKind.Reality } } } });
            }
            return true;
        }
    }
}
