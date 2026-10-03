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
        public string cardId, rewardLanguage;
        public int energyBefore, hesitationMilliseconds;
        public List<string> triggerIds = new List<string>();
        public bool continued;
        public BehaviorObservation Copy() { var copy = (BehaviorObservation)MemberwiseClone(); copy.triggerIds = new List<string>(triggerIds ?? new List<string>()); return copy; }
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
        public List<string> calibratedEvidence = new List<string>();
        public int imaginationMatches, imaginationDifferences;
        public void ObserveCalibration(string id, bool matched)
        {
            if (calibratedEvidence == null) calibratedEvidence = new List<string>();
            if (calibratedEvidence.Contains(id)) return;
            calibratedEvidence.Add(id);
            if (matched) imaginationMatches++; else imaginationDifferences++;
        }
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
        public int SuccessfulActionsWith(string id) { return recent.Count(o => !string.IsNullOrEmpty(o.cardId) && o.triggerIds != null && o.triggerIds.Contains(id) && o.continued); }
        public string RecentPreference
        {
            get { var choices = recent.Where(o => !string.IsNullOrEmpty(o.cardId)).GroupBy(o => CardCatalog.FindById(o.cardId)?.Kind).OrderByDescending(g => g.Count()).FirstOrDefault();
                return choices == null ? "还需要更多实际行动记录" : choices.Key == CardKind.Growth ? "近期更多选择成长投资" : choices.Key == CardKind.Recovery ? "近期更多选择恢复和支持" : "近期更多选择即时奖励"; }
        }
        public string RestOrPush
        {
            get { var actions = recent.Where(o => !string.IsNullOrEmpty(o.cardId)).TakeLastPortable(8).ToList();
                return actions.Count < 3 ? "先观察状态，再决定下一步" : actions.Count(o => o.energyBefore <= 3) >= 3 ? "近期多次精力偏低，可以给恢复留一个位置" :
                    actions.Count(o => o.hesitationMilliseconds >= 30000) >= 3 ? "近期在选择前停留较久，可以试一个更小的开始" : "当前记录适合继续探索，不需要把它变成永久标签"; }
        }
    }
    public static class PatternEngine
    {
        public static List<PatternRecord> Detect(IEnumerable<BehaviorObservation> observations)
        {
            var result = new List<PatternRecord>();
            foreach (var group in observations.Where(o => !string.IsNullOrEmpty(o.key) && o.key != "action").GroupBy(o => o.key))
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
        { return key == "decision-reopen" ? "决定 → 接近执行 → 再次比较 → 退出" : key == "setback" || key == "risk-recovery" ?
            "遭遇挫折 → 停下 → 寻找重新开始的路径" : key == "short-reward" ? "即时奖励 → 之后的疲惫 → 再次寻找即时奖励" : key == "overwork" ?
            "精力不足 → 仍想继续 → 给恢复留空间" : key.StartsWith("thought:", StringComparison.Ordinal) ? "念头出现 → 推迟行动 → 再次遇到相似情境" : "计划 → 不确定 → 推迟执行"; }
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
        public string recognitionNodeId;
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

    public static class KnowledgeLibrary
    {
        public static readonly KnowledgeSkill[] All = {
            Skill("face", "面对而非逃避", "受挫或心情低落，想退出时", "恢复、求助或缩小一步，然后继续"),
            Skill("rest", "恢复是行动的一部分", "精力不超过4时", "先恢复，再回到原来的行动"),
            Skill("small", "缩小第一步", "准备摩擦至少5时", "只打开材料，完成最小练习"),
            Skill("evidence", "校准而非责备", "已经作出一份预测时", "对照证据，再调整理解"),
            Skill("control", "区分控制与结果", "决定已经锁定时", "完成下一步执行，而非保证结果"),
            Skill("connection", "支持也需要投资", "关系不足6时", "联系一个人，建立互相支持"),
            Skill("commitment", "决定以后执行", "进入执行阶段时", "用准备和提示让决定落地"),
            Skill("values", "让价值进入动作", "已经选择人生路线时", "用一次探索或思考对照自己的理由")
        };
        private static KnowledgeSkill Skill(string id, string principle, string condition, string action)
        { var k = KnowledgeForge.Learn(id, condition, action); k.principle = principle; return k; }
        public static bool Matches(string id, CardSpec card)
        {
            if (card == null) return false;
            if (id == "rest") return card.Kind == CardKind.Recovery;
            if (id == "connection") return card.GivesSupport;
            if (id == "small" || id == "control" || id == "commitment") return card.Kind == CardKind.Growth;
            if (id == "evidence") return card.Id == "review" || card.Id == "forge" || card.Id == "perspective";
            if (id == "values") return card.Id == "explore" || card.Id == "perspective";
            return card.GivesSupport || card.Id == "again" || card.Id == "forge";
        }
    }
}
