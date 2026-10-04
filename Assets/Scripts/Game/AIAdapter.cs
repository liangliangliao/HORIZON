using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Horizon.Game
{
    public enum NarrativePurpose { FutureSelf, PersonalQuest, Pattern, Imagination, Knowledge, Npc }
    public sealed class NarrativeContext
    {
        public readonly string Goal, RecentPattern;
        public readonly string[] Evidence;
        public readonly NarrativePurpose Purpose;
        public NarrativeContext(string goal, string pattern, IEnumerable<string> evidence, NarrativePurpose purpose = NarrativePurpose.FutureSelf)
        { Goal = AIText.Bound(goal, 300); RecentPattern = AIText.Bound(pattern, 400);
            if (!Enum.IsDefined(typeof(NarrativePurpose), purpose)) throw new ArgumentException("Unknown narrative purpose."); Purpose = purpose;
            Evidence = (evidence ?? Array.Empty<string>()).TakeLastPortable(6).Select(x => AIText.Bound(x, 180)).ToArray(); }
        public static NarrativeContext From(GameSession life, string goal = "")
        { return new NarrativeContext(goal, life.Master?.patterns.LastOrDefault()?.description,
            CausalGraph.ObservedGraph(life.CausalNodes).Where(n => n.resolved && n.type != CausalNodeKind.Imagination).Select(n => "D" + n.day + " · " + n.label)); }
    }
    [Serializable]
    public sealed class PersonalContent
    {
        // No resource, probability, victory, or rule mutation is present in the content contract.
        public string futureSelfLine, quest, patternExplanation;
        public bool generatedByAI;
        public string source, status;
    }
    public interface IAIAdapter
    { Task<PersonalContent> Personalize(NarrativeContext context, CancellationToken cancellation); }
    public sealed class LocalContentAdapter : IAIAdapter
    {
        public Task<PersonalContent> Personalize(NarrativeContext context, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            return Task.FromResult(new PersonalContent { generatedByAI = false, source = "本地内容", status = "离线模式",
                futureSelfLine = context.Purpose == NarrativePurpose.Imagination ? "为这个目标努力几天后，第一次反馈仍然没有达到预期。先留出恢复，再选择一个可以修改的步骤。" :
                    context.Purpose == NarrativePurpose.Npc ? "想暂时停下来也有理由。你可以照顾状态，再看看今天愿意承担哪一个小步骤。" :
                    context.Purpose == NarrativePurpose.Knowledge ? "把这条知识放到一个具体时刻：先认出条件，再亲自采取一个足够小的动作。" :
                    context.Evidence.Length == 0 ? "我还在形成。今天的选择会留下我的来路。" : "我记得：" + context.Evidence.Last() + "。下一次，你准备怎样继续？",
                quest = string.IsNullOrEmpty(context.Goal) ? "站起来，开始一个两分钟的小步骤" : "为「" + context.Goal + "」准备并开始一个最小步骤",
                patternExplanation = string.IsNullOrEmpty(context.RecentPattern) ? "近期记录还不足以说明重复模式。" : "最近观察到「" + context.RecentPattern + "」。它可以被新的选择改变。" });
        }
    }
    [Serializable]
    public sealed class EventCount { public DomainEventKind kind; public int count; }
    [Serializable]
    public sealed class AnalyticsLedger
    {
        public int lastRun, cursor;
        public List<EventCount> counts = new List<EventCount>();
        public void Observe(int run, IList<DomainEvent> events)
        {
            if (run < lastRun) return;
            if (run != lastRun) { lastRun = run; cursor = 0; }
            while (cursor < events.Count)
            { var e = events[cursor++]; EventCount c = counts.Find(x => x.kind == e.kind); if (c == null) { c = new EventCount { kind = e.kind }; counts.Add(c); } c.count++; }
        }
    }
}
