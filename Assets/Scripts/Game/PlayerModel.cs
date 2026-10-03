using System;
using System.Collections.Generic;
using System.Linq;

namespace Horizon.Game
{
    [Serializable]
    public sealed class BehaviorObservation
    {
        public string id, key, nodeId, triggerId;
        public int run, day;
        public bool continued;
        public BehaviorObservation Copy() { return (BehaviorObservation)MemberwiseClone(); }
    }
    [Serializable]
    public sealed class PatternRecord
    {
        public string key, description;
        public int failures, breakthroughs, lastRun, lastBreakRun;
        public bool broken;
        public List<string> failureNodes = new List<string>();
        public List<int> failureRuns = new List<int>();
        public PatternRecord Copy() { var c = (PatternRecord)MemberwiseClone(); c.failureNodes = new List<string>(failureNodes); c.failureRuns = new List<int>(failureRuns); return c; }
    }
    [Serializable]
    public sealed class PlayerBehavioralModel
    {
        public List<BehaviorObservation> recent = new List<BehaviorObservation>();
        public List<PatternRecord> patterns = new List<PatternRecord>();
        public int failedRuns, foresightPoints;
        public List<int> observedRuns = new List<int>();
        public int HorizonLevel { get { return new[] { 0, 4, 12, 22, 36, 50, 70, 90 }.Count(v => foresightPoints >= v); } }
        public void Observe(IEnumerable<BehaviorObservation> observations)
        {
            foreach (var o in observations ?? Enumerable.Empty<BehaviorObservation>())
                if (o != null && !recent.Any(x => x.id == o.id)) recent.Add(o.Copy());
            recent = recent.OrderBy(o => o.run).ThenBy(o => o.day).TakeLastPortable(60).ToList();
            patterns = PatternEngine.Detect(recent);
        }
        public void Repair()
        { if (recent == null) recent = new List<BehaviorObservation>(); if (observedRuns == null) observedRuns = new List<int>(); Observe(null); failedRuns = Math.Max(0, Math.Min(9, failedRuns)); }
        public string Summary
        { get { PatternRecord p = patterns.OrderByDescending(x => x.lastRun).FirstOrDefault(); return p == null ?
            "还在观察近期选择。模式会随新行动改变。" : "最近出现的模式：" + p.description + (p.broken ? "。你已经走过一次。" : "。下一次仍有另一条路。"); } }
        public int TriggerSuccesses(string id) { return recent.Count(o => o.triggerId == id && o.continued); }
    }
    public static class PatternEngine
    {
        public static List<PatternRecord> Detect(IEnumerable<BehaviorObservation> observations)
        {
            var result = new List<PatternRecord>();
            foreach (var group in observations.Where(o => !string.IsNullOrEmpty(o.key)).GroupBy(o => o.key))
            {
                var p = new PatternRecord { key = group.Key, description = Description(group.Key) };
                foreach (var o in group.OrderBy(x => x.run).ThenBy(x => x.day))
                {
                    p.lastRun = o.run;
                    if (!o.continued) { p.failures++; p.failureNodes.Add(o.nodeId); if (!p.failureRuns.Contains(o.run)) p.failureRuns.Add(o.run); p.broken = false; }
                    else if (p.failureRuns.Count >= 2 && p.lastBreakRun != o.run) { p.breakthroughs++; p.broken = true; p.lastBreakRun = o.run; }
                }
                if (p.failureRuns.Count >= 2) result.Add(p);
            }
            return result;
        }
        public static string Description(string key)
        { return key == "decision-reopen" ? "决定 → 接近执行 → 再次比较 → 退出" : key == "setback" ?
            "遭遇挫折 → 停下 → 寻找重新开始的路径" : "计划 → 不确定 → 推迟执行"; }
    }
    internal static class PortableEnumerable
    {
        public static IEnumerable<T> TakeLastPortable<T>(this IEnumerable<T> source, int count)
        { var values = source.ToList(); return values.Skip(Math.Max(0, values.Count - count)); }
    }
    public enum KnowledgeStage { Know, Recognize, Simulate, Execute, Experience }
    [Serializable]
    public sealed class KnowledgeSkill
    {
        public string id, principle, condition, action;
        public KnowledgeStage stage;
        public int simulationRun;
        public string simulationNodeId, realityNodeId;
        public KnowledgeSkill Copy() { return (KnowledgeSkill)MemberwiseClone(); }
    }
    public static class KnowledgeForge
    {
        public static KnowledgeSkill Learn(string id, string condition, string action)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(condition) || string.IsNullOrWhiteSpace(action)) throw new ArgumentException("An actionable If / Then skill is required.");
            return new KnowledgeSkill { id = id, principle = "面对而非逃避", condition = condition, action = action };
        }
        public static void Advance(KnowledgeSkill skill, KnowledgeStage stage, string evidence)
        {
            if (skill == null || stage != (KnowledgeStage)((int)skill.stage + 1) || string.IsNullOrWhiteSpace(evidence)) throw new InvalidOperationException("Knowledge requires evidence at every stage.");
            if (stage == KnowledgeStage.Simulate) skill.simulationNodeId = evidence;
            if (stage == KnowledgeStage.Execute) skill.realityNodeId = evidence;
            skill.stage = stage;
        }
    }
    public static class WorldviewDeck
    {
        public static readonly string[] Names = { "James", "ACT", "CBT", "Stoicism", "Tal Ben-Shahar", "Nietzsche", "Schopenhauer", "Existentialism" };
        public static readonly string[] Perspectives = {
            "先做一个小动作，观察感受怎样变化。", "允许不舒服存在，再朝重视的方向行动。", "把想法、证据与可验证的行动分开。", "区分能控制的动作与无法保证的结果。", "给快乐和意义都留出空间。", "你愿意为哪一种生活承担代价？", "看到欲望怎样带来短暂满足与新的需求。", "选择与责任属于你；用行动写出意义。" };
    }
}
