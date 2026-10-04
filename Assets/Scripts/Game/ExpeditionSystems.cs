using System;
using System.Collections.Generic;
using System.Linq;

namespace Horizon.Game
{
    [Serializable]
    public sealed class InvestmentPool
    {
        public string id;
        public List<string> sources = new List<string>();
        public int paid;
        public InvestmentPool Copy() { return new InvestmentPool { id = id, paid = paid, sources = new List<string>(sources) }; }
    }

    [Serializable]
    public sealed class ExpeditionState
    {
        public string route, routeNode, environment = "安静", mirrorKey, monsterId, monsterNode, lastRecovery;
        public int frictionReduction, rewardReduction, motivationBoost, preparationsToday, monsterDay, mirrorDay = 4;
        public bool mirrorResolved;
        public List<string> worldviews = new List<string>();
        public List<InvestmentPool> pools = new List<InvestmentPool>();
        public List<string> receivedMessages = new List<string>();
        public ExpeditionState Copy()
        {
            var copy = (ExpeditionState)MemberwiseClone(); copy.worldviews = new List<string>(worldviews);
            copy.pools = pools.Select(x => x.Copy()).ToList(); copy.receivedMessages = new List<string>(receivedMessages ?? new List<string>()); return copy;
        }
        public void Validate()
        {
            if (receivedMessages == null) receivedMessages = new List<string>();
            if (worldviews == null || pools == null || worldviews.Count > 3 || worldviews.Distinct().Count() != worldviews.Count ||
                worldviews.Any(x => !WorldviewDeck.Names.Contains(x)) || pools.Count > 4 || pools.Any(x => x == null || x.sources == null || x.sources.Count > 120) ||
                preparationsToday < 0 || preparationsToday > 20 || frictionReduction < 0 || frictionReduction > 4 || rewardReduction < 0 || rewardReduction > 4 ||
                !string.IsNullOrEmpty(route) && !LifeRoutes.Ids.Contains(route)) throw new ArgumentException("Invalid expedition state.");
        }
    }

    public static class LifeRoutes
    {
        public static readonly string[] Ids = { "growth", "relationship", "income", "recovery", "exploration" };
        public static readonly string[] Names = { "成长", "关系", "赚钱", "恢复", "探索" };
        public static readonly string[] Tradeoffs = { "更多练习机会；今天仍须付出精力", "更早建立支持；占用成长时间", "更关注收入；留意疲劳与关系", "照顾状态；能力兑现较慢", "扩大选择空间；承担未知成本" };
        public static readonly string[] Cards = { "study", "message", "smalljob", "walk", "explore" };
    }

    public sealed class BossDefinition
    {
        public string id, name, title, goal, challenge, preparation, lesson;
        public BossDefinition(string id, string name, string title, string goal, string challenge, string preparation, string lesson)
        { this.id = id; this.name = name; this.title = title; this.goal = goal; this.challenge = challenge; this.preparation = preparation; this.lesson = lesson; }
    }
    public static class BossCatalog
    {
        public static readonly BossDefinition[] All = {
            new BossDefinition("uncertainty", "不确定", "穿过不确定", "带着作品参加面试", "第一次反馈：结果仍然无法保证", "先预测一次，或请人帮你核对", "不知道结果，也能选择下一步"),
            new BossDefinition("tomorrow", "明天", "在机会关闭之前", "接住一次邀约", "职位窗口提前关闭；再等等会失去这次机会", "先安排提醒，再留一个恢复日", "机会有自己的时间"),
            new BossDefinition("perfection", "完美", "让不完美的作品出发", "交出一份作品", "作品还有缺点；打磨会消耗状态", "先写可交付的最低版本", "完成初稿也可以是抵达"),
            new BossDefinition("deadline", "截止日", "让积累准时回来", "完成一次技能展示", "留得太晚的成长回声赶不上展示", "让两次成长回声在截止日前抵达", "时间安排决定能用上的准备"),
            new BossDefinition("comfort", "舒适区", "走出熟悉的一小步", "尝试一条陌生路线", "熟悉的奖励不断邀请你留下", "尝试探索牌，或装备路线准备", "照顾舒适，也给探索留空间"),
            new BossDefinition("gaze", "目光", "带着自己的理由出场", "公开展示一次想法", "一次评价让你想撤回作品", "建立支持，或练习承诺后的执行", "评价可以存在，选择仍属于你"),
            new BossDefinition("possibility", "另一个可能", "选择以后继续走", "走完已经选择的路线", "新路线不断出现，旧路线却停在起点", "选择一条人生路线，再锁定一次行动", "选择意味着保留价值，也放下一些可能"),
            new BossDefinition("fatigue", "疲劳", "带着余力抵达", "完成一次可持续挑战", "精力透支让每一步看起来更难", "至少安排三次恢复，留出足够精力", "恢复是策略的一部分"),
            new BossDefinition("waiting", "再等等", "接住当下的动机", "在提醒出现后开始", "准备越多，动机窗口越短", "装备截止提示，或及时接住机会牌", "条件成熟时，开始本身有价值"),
            new BossDefinition("preparation", "永远准备", "让计划进入执行", "亲自执行一次锁定决定", "准备清单增加，真正动手的时间变少", "完成一次锁定、准备与实际执行", "准备可以结束，行动可以开始")
        };
        public static BossDefinition Find(string id) { return All.FirstOrDefault(x => x.id == id); }
    }

    public sealed class FutureSelfVersion
    {
        public string id, name, question, memory;
        public int appearance;
    }
    public static class FutureSelfGallery
    {
        public static FutureSelfVersion[] From(RunRecord run)
        {
            if (run == null) return new[] { new FutureSelfVersion { id = "forming", name = "正在形成的自己", question = "今天，你准备留下哪一种来路？" } };
            string memory = run.actions.LastOrDefault()?.cardName ?? "一个小动作";
            var versions = new List<FutureSelfVersion> {
                new FutureSelfVersion { id = "elder", name = "老年的自己", question = "哪些选择，十年后你仍愿意记得？", memory = memory, appearance = 6 },
                new FutureSelfVersion { id = "parallel", name = "另一条人生的自己", question = "如果在同一个节点换一张牌，我们会在哪里相遇？", memory = memory, appearance = 5 }
            };
            versions.Insert(0, run.master?.chapter?.outcome == ChapterOutcome.Arrived || run.boss?.passed == 3 ?
                new FutureSelfVersion { id = "peaceful", name = "平静的自己", question = "这一路，你愿意保留什么，又愿意改变什么？", memory = memory, appearance = 3 } :
                new FutureSelfVersion { id = "again", name = "失败但继续的自己", question = "如果再来到这里，你准备怎样恢复并重新开始？", memory = memory, appearance = 4 });
            if (run.finalEnergy <= 3) versions.Add(new FutureSelfVersion { id = "tired", name = "疲惫的自己", question = "下一条路，哪里可以给恢复留一个位置？", memory = memory, appearance = 0 });
            if (run.finalMoney >= 7) versions.Add(new FutureSelfVersion { id = "wealthy", name = "富有的自己", question = "获得的时间与资源，你想用来靠近什么？", memory = memory, appearance = 1 });
            bool relationshipsDeclined = run.master?.resources != null && run.master.resources.Count > 1 &&
                run.master.resources[0].values != null && run.finalRelation < run.master.resources[0].values.relation;
            if (run.finalRelation <= 3 || relationshipsDeclined) versions.Add(new FutureSelfVersion { id = "lonely", name = "孤独的自己", question = "关系的来路正在变化。如果重新走一次，你想把哪一个人带进来？", memory = memory, appearance = 2 });
            return versions.ToArray();
        }
    }
}
