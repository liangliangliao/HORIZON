using System;
using System.Collections.Generic;
using System.Linq;

namespace Horizon.Game
{
    public enum DecisionStatus { Locked, Executing, Ready, Completed, Unlocked }
    [Serializable]
    public sealed class DecisionRecord
    {
        public int day, step, reopens;
        public string cardId, nodeId;
        public DecisionStatus status;
        public List<string> steps = new List<string> { "准备材料", "降低一步摩擦", "开始行动", "抵达并完成" };
        public DecisionRecord Copy() { var c = (DecisionRecord)MemberwiseClone(); c.steps = new List<string>(steps); return c; }
        public void Validate(int today)
        { if (day < 1 || day > today || string.IsNullOrEmpty(cardId) || steps == null || steps.Count != 4 || step < 0 || step > steps.Count ||
              !Enum.IsDefined(typeof(DecisionStatus), status) || reopens < 0 || (status == DecisionStatus.Ready || status == DecisionStatus.Completed) && step != steps.Count)
              throw new ArgumentException("Invalid decision lock."); }
    }

    [Serializable]
    public sealed class ActionEngineState
    {
        public int motivation = 5, ability = 5, trigger, friction = 5, emotion = 5, alternativeReward = 4, fatigue = 4, socialPressure;
        public int ReadyScore { get { return motivation + ability + trigger + emotion + socialPressure - friction - alternativeReward - fatigue; } }
        public ActionEngineState Copy() { return (ActionEngineState)MemberwiseClone(); }
    }
    public sealed class CouncilVoice
    {
        public string name;
        public int weight;
        public List<string> sources = new List<string>();
    }
    public sealed class ThoughtMonster
    {
        public string id, name, reasonablePart, tradeoff, response;
    }
    public static class InnerCouncil
    {
        public static List<CouncilVoice> Explain(ActionEngineState e)
        {
            var voices = new List<CouncilVoice> {
                new CouncilVoice { name = "舒适", weight = 5 + e.fatigue + e.friction + e.alternativeReward, sources = new List<string> { "疲劳 " + e.fatigue, "环境摩擦 " + e.friction, "替代奖励 " + e.alternativeReward } },
                new CouncilVoice { name = "成长", weight = 4 + e.motivation + e.ability, sources = new List<string> { "当前动机 " + e.motivation, "行动能力 " + e.ability } },
                new CouncilVoice { name = "害怕失败", weight = 3 + 10 - e.emotion, sources = new List<string> { "情绪余量 " + e.emotion, "结果仍有不确定性" } },
                new CouncilVoice { name = "承诺", weight = 2 + e.trigger + e.socialPressure, sources = new List<string> { "环境提示 " + e.trigger, "外部约定 " + e.socialPressure } }
            };
            int sum = voices.Sum(v => v.weight), allocated = 0;
            for (int i = 0; i < voices.Count; i++) { voices[i].weight = i == voices.Count - 1 ? 100 - allocated : voices[i].weight * 100 / sum; allocated += voices[i].weight; }
            return voices.OrderByDescending(v => v.weight).ToList();
        }
    }
    public static class ThoughtMonsters
    {
        public static ThoughtMonster Current(ActionEngineState e, int reopens)
        {
            if (reopens > 0) return new ThoughtMonster { id = "possibility", name = "另一个可能", reasonablePart = "比较能帮助你避免草率决定。", tradeoff = "执行时重新比较会消耗已经打开的窗口。", response = "保留备选，先完成已经锁定的一步。" };
            if (e.friction >= 5) return new ThoughtMonster { id = "perfect", name = "完美计划", reasonablePart = "准备能降低不确定性。", tradeoff = "增加步骤也会提高开始的门槛。", response = "删掉一个准备步骤，让行动开始。" };
            if (e.fatigue >= 6) return new ThoughtMonster { id = "tomorrow", name = "明天再说", reasonablePart = "疲惫的时候需要休息。", tradeoff = "有些机会会在休息期间关闭。", response = "安排恢复，并留下下一步的提示。" };
            return new ThoughtMonster { id = "comfort", name = "舒服一点", reasonablePart = "眼前的快乐也有价值。", tradeoff = "看看它会给三天后的你留下什么。", response = "权衡之后，亲自选择。" };
        }
    }
    public static class TriggerEquipment
    {
        public static readonly string[] Ids = { "alarm", "appointment", "friend", "promise", "deposit", "route", "ticket", "place", "deadline", "environment" };
        public static readonly string[] Names = { "闹钟", "预约", "朋友提醒", "公开承诺", "押金", "路线准备", "已买好的票", "固定地点", "Deadline", "环境限制" };
    }
}
