using System;
using System.Collections.Generic;
using System.Linq;

namespace Horizon.Game
{
    public enum ImaginePhase { VictoryAnchor, Preparation, Effort, Failure, Recover, Retry, Adjust, Victory, Complete }
    public enum RecoveryAction { Rest, AskHelp, LowerTarget, ChangeMethod }
    public enum PreparationAction { SmallStep, FixedTime, WithSupport }
    [Serializable]
    public sealed class ImagineBeat
    {
        public int index;
        public ImaginePhase phase;
        public string text, actionKey;
        public ImagineBeat Copy() { return (ImagineBeat)MemberwiseClone(); }
    }
    [Serializable]
    public sealed class FutureMemory
    {
        public string id, goalId, goal, context, actionKey, text, imaginationNodeId, simulationNodeId;
        public int value = 1, imaginationRun, simulationRun, simulationDay;
        public bool recalled;
        public FutureMemory Copy() { return (FutureMemory)MemberwiseClone(); }
    }
    [Serializable]
    public sealed class ImagineRun
    {
        public string id, goalId, goal;
        public int difficulty = 1, failures, recovered, multiplier = 1;
        public int pathVersion;
        public int plannedFailures, energy = 6, focus = 4, support = 4;
        public string adaptation, goalFamily;
        public string preparationKey;
        public ImaginePhase phase;
        public List<ImagineBeat> timeline = new List<ImagineBeat>();
        public List<FutureMemory> memories = new List<FutureMemory>();
        public int RequiredFailures { get { return plannedFailures > 0 ? plannedFailures : difficulty == 1 ? 1 : difficulty + 1; } }
        public ImagineRun Copy()
        { var c = (ImagineRun)MemberwiseClone(); c.timeline = timeline.Select(b => b.Copy()).ToList(); c.memories = memories.Select(m => m.Copy()).ToList(); return c; }
        public void Validate()
        {
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(goalId) || string.IsNullOrWhiteSpace(goal) || goal.Length > 100 ||
                difficulty < 1 || difficulty > 3 || pathVersion < 0 || pathVersion > 2 || plannedFailures < 0 || plannedFailures > 4 || energy < 0 || energy > 10 || focus < 0 || focus > 10 || support < 0 || support > 10 || failures < 0 || failures > RequiredFailures || recovered < 0 || recovered > failures ||
                !Enum.IsDefined(typeof(ImaginePhase), phase) || timeline == null || memories == null || timeline.Count > 64 ||
                memories.Count != recovered || multiplier != 1 + recovered ||
                timeline.Where((b, i) => b == null || b.index != i).Any() ||
                timeline.Count > 0 && timeline.Last().phase != phase ||
                timeline.Count(b => b.phase == ImaginePhase.Failure) != failures || timeline.Count(b => b.phase == ImaginePhase.Retry) != recovered ||
                phase == ImaginePhase.Complete && (failures != RequiredFailures || recovered != failures)) throw new ArgumentException("Invalid imagination timeline.");
        }
    }
    public sealed class TimelineDivergence
    {
        public int index;
        public string imagined, actual;
        public bool converged;
    }

    // A finite state machine: the victory scene is an anchor, never a shortcut past failure.
    public static class ImaginationEngine
    {
        public static ImagineRun Begin(string id, string goalId, string goal, int difficulty = 1, int pathVersion = 0, int failures = 0, string adaptation = "")
        {
            var run = new ImagineRun { id = id, goalId = goalId, goal = (goal ?? "").Trim(), difficulty = difficulty, pathVersion = pathVersion,
                plannedFailures = failures, adaptation = AIText.Bound(adaptation, 300), goalFamily = GoalFamily(goal) };
            run.Validate(); Append(run, ImaginePhase.VictoryAnchor, "你已抵达「" + run.goal + "」。记住这一瞬，然后让时间倒退。", "anchor"); return run;
        }
        private static void Append(ImagineRun run, ImaginePhase phase, string text, string action)
        { run.phase = phase; run.timeline.Add(new ImagineBeat { index = run.timeline.Count, phase = phase, text = text, actionKey = action }); }
        public static void Continue(ImagineRun run)
        {
            if (run == null) throw new ArgumentNullException("run"); run.Validate();
            switch (run.phase)
            {
                case ImaginePhase.VictoryAnchor: Append(run, ImaginePhase.Preparation, "回到今天。准备一个最小步骤和一个行动提示。", "prepare"); break;
                case ImaginePhase.Preparation:
                    if (run.pathVersion > 0) throw new InvalidOperationException("Choose how to prepare the path.");
                    Append(run, ImaginePhase.Effort, "你开始努力。过程比胜利画面漫长，诱惑仍在。", "effort"); break;
                case ImaginePhase.Effort:
                case ImaginePhase.Adjust:
                    if (run.pathVersion >= 2) { run.energy = Math.Max(0, run.energy - 2); run.focus = Math.Max(0, run.focus - 1); }
                    if (run.failures < run.RequiredFailures) { run.failures++; Append(run, ImaginePhase.Failure,
                        "第 " + run.failures + " 次挫折：" + (run.pathVersion > 0 ? DifficultyFor(run) : "你的尝试没有得到预期回应。") + "失败后的明天怎么办？", "failure"); }
                    else Append(run, ImaginePhase.Victory, "困难仍然存在，但你已经学会恢复、求助与调整。现在抵达你的未来。", "victory"); break;
                case ImaginePhase.Failure: Append(run, ImaginePhase.Recover, "允许失败。选择一个具体恢复动作，再重新开始。", "recover"); break;
                case ImaginePhase.Retry: Append(run, ImaginePhase.Adjust, "再次行动：保留有效的部分，改变一个方法。", "adjust"); break;
                case ImaginePhase.Victory: Append(run, ImaginePhase.Complete, "想象已留下未来记忆。下一次相似困难到来时，你有一条见过的路。", "complete"); break;
                default: throw new InvalidOperationException("Choose a recovery action or finish this run.");
            }
        }
        public static void Prepare(ImagineRun run, PreparationAction action)
        {
            if (run == null || run.phase != ImaginePhase.Preparation || run.pathVersion < 1 || !Enum.IsDefined(typeof(PreparationAction), action))
                throw new InvalidOperationException("Preparation belongs at the beginning of the imagined path.");
            run.Validate(); run.preparationKey = action.ToString();
            if (run.pathVersion >= 2) { run.energy = Math.Max(0, run.energy - 1); run.focus = Math.Max(0, run.focus - (action == PreparationAction.FixedTime ? 2 : 1)); if (action == PreparationAction.WithSupport) run.support = Math.Max(0, run.support - 1); }
            string[] paths = { "你把「" + run.goal + "」缩成两分钟能开始的一步。开始更容易，但今天的进展较小。",
                "你为「" + run.goal + "」留出固定时间并设置提醒。时间到了，其他诱惑也可能出现。",
                "你约好一个人一起准备「" + run.goal + "」。有了支持，也要面对对方暂时没空的可能。" };
            Append(run, ImaginePhase.Effort, paths[(int)action], "prepare:" + action);
        }
        private static string DifficultyFor(ImagineRun run)
        {
            if (run.pathVersion >= 2)
            {
                if (run.failures == 1 && !string.IsNullOrWhiteSpace(run.adaptation)) return "上次实际路径的分歧再次出现：" + run.adaptation;
                string[] career = { "没有收到回应，你怀疑自己是否准备好了。", "修改作品时遇到不会的部分。", "面试前睡眠不足，开始想放弃。", "新的要求迫使你调整方法。" };
                string[] health = { "天气变化，原来的路线不适合今天。", "身体疲惫，原来的强度太大。", "昨天中断了练习，今天很难重新开始。", "计划与工作时间发生冲突。" };
                string[] relationship = { "消息暂时没有回应。", "约好的人临时没有空。", "一次误解让你想停止交流。", "自己的需要和对方的期待不同。" };
                string[] learning = { "第一次尝试没有达到预期。", "遇到了不会的内容，进展变慢。", "疲惫让熟悉的任务也变得困难。", "原来的方法失效了，需要换一条路。" };
                string[] situations = run.goalFamily == "career" ? career : run.goalFamily == "health" ? health : run.goalFamily == "relationship" ? relationship : learning;
                return situations[Math.Min(3, run.failures - 1)] + (run.energy <= 2 ? "先给恢复留出空间。" : "困难仍然可以准备。");
            }
            if (run.preparationKey == "WithSupport") return "约好的人今天没空，你独自停在开始之前。";
            if (run.preparationKey == "FixedTime") return "提醒响起时你已经疲惫，想把「" + run.goal + "」推到明天。";
            return "你完成了小步骤，但「" + run.goal + "」的下一步仍比预想困难。";
        }
        public static FutureMemory Recover(ImagineRun run, RecoveryAction action)
        {
            if (run == null || run.phase != ImaginePhase.Recover || !Enum.IsDefined(typeof(RecoveryAction), action)) throw new InvalidOperationException("Recovery is only available after failure.");
            string[] lines = { "失败后的第二天，我先恢复精力，再重新开始。", "失败后的第二天，我向一个人请求帮助。", "失败后的第二天，我把目标缩成能完成的一步。", "失败后的第二天，我换了一种方法继续尝试。" };
            run.recovered++; run.multiplier = 1 + run.recovered;
            if (run.pathVersion >= 2)
            { run.energy = Math.Min(10, run.energy + (action == RecoveryAction.Rest ? 4 : 1)); run.focus = Math.Min(10, run.focus + (action == RecoveryAction.ChangeMethod ? 3 : 1)); run.support = Math.Min(10, run.support + (action == RecoveryAction.AskHelp ? 2 : 0)); }
            var memory = new FutureMemory { id = run.id + ":memory:" + run.recovered, goalId = run.goalId, goal = run.goal,
                context = "setback", actionKey = action.ToString(), text = lines[(int)action], value = run.multiplier,
                imaginationNodeId = run.id + ":beat:" + run.timeline.Count };
            run.memories.Add(memory); Append(run, ImaginePhase.Retry, memory.text + "  AGAIN ×" + run.multiplier, memory.actionKey); return memory;
        }
        public static string GoalFamily(string goal)
        {
            goal = goal ?? "";
            if (new[] { "面试", "工作", "招聘", "作品", "求职" }.Any(goal.Contains)) return "career";
            if (new[] { "运动", "身体", "健康", "锻炼", "跑步" }.Any(goal.Contains)) return "health";
            if (new[] { "朋友", "关系", "家人", "交流", "联系" }.Any(goal.Contains)) return "relationship";
            return "learning";
        }
        public static TimelineDivergence Compare(IList<string> imagined, IList<string> reality)
        {
            if (imagined == null || reality == null) throw new ArgumentNullException("timelines");
            int end = Math.Min(imagined.Count, reality.Count), i = 0;
            while (i < end && imagined[i] == reality[i]) i++;
            return new TimelineDivergence { index = i, converged = i == imagined.Count && i == reality.Count,
                imagined = i < imagined.Count ? imagined[i] : "终点", actual = i < reality.Count ? reality[i] : "尚未发生" };
        }
        public static bool Matches(FutureMemory memory, CardSpec card)
        {
            if (memory == null || card == null) return false;
            return memory.actionKey == "Rest" && card.Kind == CardKind.Recovery && !card.GivesSupport ||
                memory.actionKey == "AskHelp" && card.GivesSupport || memory.actionKey == "LowerTarget" &&
                (card.Id == "shortstudy" || card.Id == "trigger" || card.Id == "plan") ||
                memory.actionKey == "ChangeMethod" && (card.Id == "review" || card.Id == "forge" || card.Id == "perspective" || card.Id == "explore");
        }
    }
}
