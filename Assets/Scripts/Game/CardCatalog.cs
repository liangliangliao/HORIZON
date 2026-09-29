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

        public ResourceDelta(int energy = 0, int mood = 0, int insight = 0)
        {
            this.energy = energy;
            this.mood = mood;
            this.insight = insight;
        }

        public string ShortLabel()
        {
            string label = "";
            Append(ref label, "⚡", energy);
            Append(ref label, "☀", mood);
            Append(ref label, "▲", insight);
            return label.Trim();
        }

        private static void Append(ref string label, string icon, int value)
        {
            if (value != 0) label += icon + " " + (value > 0 ? "+" : "") + value + "  ";
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
        private static readonly CardSpec[] Temptations =
        {
            new CardSpec("scroll", "刷到凌晨", CardKind.Temptation,
                new ResourceDelta(-2, 4), new ResourceDelta(-2), 2, "2日后  ·  火种", "疲惫回来了"),
            new CardSpec("impulse", "冲动消费", CardKind.Temptation,
                new ResourceDelta(-1, 3), new ResourceDelta(0, -2), 3, "3日后  ·  火种", "快乐褪色"),
            new CardSpec("episode", "再看一集", CardKind.Temptation,
                new ResourceDelta(-1, 3), new ResourceDelta(-2), 2, "2日后  ·  火种", "睡意追上了你")
        };

        private static readonly CardSpec[] Growth =
        {
            new CardSpec("practice", "刻意练习", CardKind.Growth,
                new ResourceDelta(-2), new ResourceDelta(0, 0, 3), 3, "3日后  ·  芽", "练习有了形状"),
            new CardSpec("study", "深度学习", CardKind.Growth,
                new ResourceDelta(-2), new ResourceDelta(0, 0, 2), 2, "2日后  ·  芽", "知识连成了线"),
            new CardSpec("portfolio", "整理作品", CardKind.Growth,
                new ResourceDelta(-2, 0, 1), new ResourceDelta(0, 0, 2), 3, "3日后  ·  芽", "机会看见了作品")
        };

        private static readonly CardSpec[] Recovery =
        {
            new CardSpec("rest", "早点休息", CardKind.Recovery,
                new ResourceDelta(3, 1), new ResourceDelta(), 0, "让明天轻一点", ""),
            new CardSpec("friend", "给朋友发消息", CardKind.Recovery,
                new ResourceDelta(2, 1), new ResourceDelta(0, 1), 2, "2日后  ·  回信", "有人回应了你", true),
            new CardSpec("walk", "出去走走", CardKind.Recovery,
                new ResourceDelta(3, 1), new ResourceDelta(), 0, "回到自己的节奏", "")
        };

        public static CardSpec[] ForDay(int day, int runNumber)
        {
            if (day < 1 || day > 12) throw new ArgumentOutOfRangeException("day");
            if (runNumber < 1) throw new ArgumentOutOfRangeException("runNumber");
            if (day == 1 && runNumber == 1)
                return new[] { Temptations[0], Growth[0], Recovery[0] };

            int index = (day - 1 + runNumber - 1) % 3;
            CardSpec recovery = day % 3 == 2 ? Recovery[1] : Recovery[day % 3 == 0 ? 2 : 0];
            return new[] { Temptations[index], Growth[index], recovery };
        }
    }
}

