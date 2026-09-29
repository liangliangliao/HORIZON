using System;

namespace Horizon.Game
{
    public enum CardKind { Temptation, Growth, Recovery }

    [Serializable]
    public sealed class ResourceDelta
    {
        public int energy;
        public int mood;
        public int insight;
        public int relation;
        public int money;
        public int ability;

        public ResourceDelta(int energy = 0, int mood = 0, int insight = 0,
            int relation = 0, int money = 0, int ability = 0)
        {
            this.energy = energy;
            this.mood = mood;
            this.insight = insight;
            this.relation = relation;
            this.money = money;
            this.ability = ability;
        }

        public string ShortLabel()
        {
            string label = "";
            Append(ref label, "精", energy);
            Append(ref label, "心", mood);
            Append(ref label, "识", insight);
            Append(ref label, "♥", relation);
            Append(ref label, "¥", money);
            Append(ref label, "▲", ability);
            return label;
        }

        private static void Append(ref string label, string icon, int value)
        {
            if (value == 0) return;
            if (label.Length > 0) label += " · ";
            label += icon + " " + (value > 0 ? "+" : "") + value;
        }
    }

    public sealed class CardSpec
    {
        public readonly string Id;
        public readonly string Name;
        public readonly CardKind Kind;
        public readonly ResourceDelta Now;
        public readonly ResourceDelta Later;
        public readonly int Delay;
        public readonly string FutureHint;
        public readonly string EchoName;
        public readonly bool GivesSupport;

        public CardSpec(string id, string name, CardKind kind, ResourceDelta now,
            ResourceDelta later, int delay, string futureHint, string echoName, bool givesSupport = false)
        {
            Id = id;
            Name = name;
            Kind = kind;
            Now = now;
            Later = later;
            Delay = delay;
            FutureHint = futureHint;
            EchoName = echoName;
            GivesSupport = givesSupport;
        }
    }

    // One of each intent every day. All recovery choices restore energy, so no run can deadlock.
    public static class CardCatalog
    {
        // A missed invitation changes the choice, not merely its resource reward.
        public static readonly CardSpec SoloRecovery = new CardSpec("solo", "独处休息", CardKind.Recovery,
            new ResourceDelta(2), new ResourceDelta(), 0, "先照顾好此刻的自己", "");

        // Consequences can open a different action rather than only changing a number.
        public static readonly CardSpec Opportunity = new CardSpec("opportunity", "接住机会", CardKind.Growth,
            new ResourceDelta(-2, 0, 1), new ResourceDelta(0, 0, 0, 0, 2, 2), 2,
            "2日后  ·  机会", "机会变成了作品");
        public static readonly CardSpec Together = new CardSpec("together", "并肩准备", CardKind.Growth,
            new ResourceDelta(-1, 0, 0, 1), new ResourceDelta(0, 1, 0, 0, 1, 1), 2,
            "2日后  ·  同行", "有人一起走到了这里", true);

        private static readonly CardSpec[] Temptations =
        {
            new CardSpec("scroll", "刷到凌晨", CardKind.Temptation,
                new ResourceDelta(-2, 4), new ResourceDelta(-2), 2, "2日后  ·  火种", "疲惫回来了"),
            new CardSpec("impulse", "冲动消费", CardKind.Temptation,
                new ResourceDelta(-1, 3, 0, 0, -2), new ResourceDelta(0, -2), 3, "3日后  ·  火种", "快乐褪色"),
            new CardSpec("episode", "再看一集", CardKind.Temptation,
                new ResourceDelta(-1, 3), new ResourceDelta(-2), 2, "2日后  ·  火种", "睡意追上了你"),
            new CardSpec("avoid", "先放一放", CardKind.Temptation,
                new ResourceDelta(-1, 2), new ResourceDelta(-1, -1, 0, -1), 2, "2日后  ·  火种", "事情还在等你"),
            new CardSpec("comfort", "买点安慰", CardKind.Temptation,
                new ResourceDelta(-1, 3, 0, 0, -2), new ResourceDelta(0, -2), 3, "3日后  ·  火种", "快乐慢慢退去"),
            new CardSpec("overcommit", "全部答应", CardKind.Temptation,
                new ResourceDelta(-2, 2, 0, 1), new ResourceDelta(-2, 0, 0, -2), 2, "2日后  ·  火种", "疲惫叠了起来")
        };

        private static readonly CardSpec[] Growth =
        {
            new CardSpec("practice", "刻意练习", CardKind.Growth,
                new ResourceDelta(-2), new ResourceDelta(0, 0, 3, 0, 0, 2), 3, "3日后  ·  芽", "练习有了形状"),
            new CardSpec("study", "深度学习", CardKind.Growth,
                new ResourceDelta(-2), new ResourceDelta(0, 0, 2, 0, 0, 1), 2, "2日后  ·  芽", "知识连成了线"),
            new CardSpec("portfolio", "整理作品", CardKind.Growth,
                new ResourceDelta(-2, 0, 1), new ResourceDelta(0, 0, 2, 0, 2, 2), 3, "3日后  ·  芽", "机会看见了作品"),
            new CardSpec("review", "认真复盘", CardKind.Growth,
                new ResourceDelta(-2, 0, 1), new ResourceDelta(0, 0, 2, 0, 0, 1), 2, "2日后  ·  芽", "线索连了起来"),
            new CardSpec("plan", "写下计划", CardKind.Growth,
                new ResourceDelta(-1), new ResourceDelta(0, 0, 2, 0, 0, 1), 3, "3日后  ·  芽", "方向变得清晰"),
            new CardSpec("ask", "试着求助", CardKind.Growth,
                new ResourceDelta(-1, 0, 1, 1), new ResourceDelta(0, 1, 1, 1, 1), 2,
                "2日后  ·  回应", "有人愿意同行", true)
        };

        private static readonly CardSpec[] Recovery =
        {
            new CardSpec("rest", "早点休息", CardKind.Recovery,
                new ResourceDelta(3, 1), new ResourceDelta(), 0, "让明天轻一点", ""),
            new CardSpec("friend", "给朋友发消息", CardKind.Recovery,
                new ResourceDelta(2, 1, 0, 2), new ResourceDelta(0, 1, 0, 1), 2, "2日后  ·  回信", "有人回应了你", true),
            new CardSpec("walk", "出去走走", CardKind.Recovery,
                new ResourceDelta(3, 1), new ResourceDelta(), 0, "回到自己的节奏", ""),
            new CardSpec("tidy", "整理房间", CardKind.Recovery,
                new ResourceDelta(2, 2), new ResourceDelta(), 0, "眼前宽敞了一点", ""),
            new CardSpec("dinner", "一起吃饭", CardKind.Recovery,
                new ResourceDelta(2, 1, 0, 2), new ResourceDelta(0, 1, 0, 1), 2,
                "2日后  ·  回应", "一顿饭留下了温度", true),
            new CardSpec("jog", "慢跑十分钟", CardKind.Recovery,
                new ResourceDelta(3), new ResourceDelta(0, 1), 1,
                "明天  ·  回声", "身体记住了呼吸")
        };

        public static CardSpec[] ForDay(int day, int runNumber)
        {
            if (day < 1 || day > 12) throw new ArgumentOutOfRangeException("day");
            if (runNumber < 1) throw new ArgumentOutOfRangeException("runNumber");
            if (day == 1 && runNumber == 1)
                return new[] { Temptations[0], Growth[0], Recovery[0] };

            int index = (day - 1 + runNumber - 1) % Temptations.Length;
            return new[] { Temptations[index], Growth[index], Recovery[index] };
        }

        public static CardSpec FindById(string id)
        {
            if (id == SoloRecovery.Id) return SoloRecovery;
            if (id == Opportunity.Id) return Opportunity;
            if (id == Together.Id) return Together;
            CardSpec card = Array.Find(Temptations, candidate => candidate.Id == id);
            if (card != null) return card;
            card = Array.Find(Growth, candidate => candidate.Id == id);
            return card ?? Array.Find(Recovery, candidate => candidate.Id == id);
        }
    }
}
