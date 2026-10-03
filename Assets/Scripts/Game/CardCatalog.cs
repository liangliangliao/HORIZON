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
        public readonly CardTraits Traits;

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
            Traits = CardTraits.For(id, kind, givesSupport);
        }
    }

    // One of each intent every day. All recovery choices restore energy, so no run can deadlock.
    public static class CardCatalog
    {
        public const int CurrentVersion = 7;
        private static readonly CardSpec[] SeasonActions = {
            new CardSpec("library", "借一本好书", CardKind.Growth, new ResourceDelta(-1),
                new ResourceDelta(0, 0, 2, 0, 0, 1), 2, "2日后 · 灵感", "书里的一个想法成为了你的能力"),
            new CardSpec("teamproject", "一起做点东西", CardKind.Growth, new ResourceDelta(-2, 0, 0, 1),
                new ResourceDelta(0, 1, 1, 1, 0, 2), 3, "3日后 · 作品", "共同完成的作品带来了成长", true),
            new CardSpec("music", "听完一张专辑", CardKind.Temptation, new ResourceDelta(-1, 2),
                new ResourceDelta(0, 1), 1, "明天 · 余韵", "音乐留在了第二天的心情里"),
            new CardSpec("rushjob", "接一份急活", CardKind.Temptation, new ResourceDelta(-2, 1, 0, 0, 3),
                new ResourceDelta(-2, -1), 2, "2日后 · 倦意", "赶工留下的疲惫追上了你"),
            new CardSpec("breathing", "给自己十分钟", CardKind.Recovery, new ResourceDelta(2, 2),
                new ResourceDelta(1), 1, "明天 · 余力", "留给自己的时间慢慢回来了"),
            new CardSpec("listen", "听朋友说说话", CardKind.Recovery, new ResourceDelta(2, 1, 0, 2),
                new ResourceDelta(0, 1, 0, 1), 2, "2日后 · 信任", "有人记得你认真听过", true)
        };
        public static readonly CardSpec BalancedPlay = new CardSpec("play", "玩一局", CardKind.Temptation,
            new ResourceDelta(-1, 3), new ResourceDelta(0, 1), 1, "明天  ·  余兴", "快乐留下了一点余温");
        private static readonly CardSpec[] OutlookActions = {
            new CardSpec("project", "推进长期项目", CardKind.Growth, new ResourceDelta(-2),
                new ResourceDelta(0, 0, 1, 0, 1, 2), 4, "4日后 · 成果", "项目向前走了一步"),
            new CardSpec("collaborate", "一起完成项目", CardKind.Growth, new ResourceDelta(-2, 0, 0, 1),
                new ResourceDelta(0, 1, 0, 1, 1, 1), 3, "3日后 · 合作", "一起完成的东西留下了信任", true),
            new CardSpec("publish", "分享我的成果", CardKind.Growth, new ResourceDelta(-2, 0, 1),
                new ResourceDelta(0, 1, 0, 1, 2, 1), 2, "2日后 · 回应", "成果找到了回应", true),
            new CardSpec("weekend", "留一个安静周末", CardKind.Recovery, new ResourceDelta(3, 2),
                new ResourceDelta(1), 2, "2日后 · 余力", "休息为后来留出了空间"),
            new CardSpec("reconnect", "与老朋友重聚", CardKind.Recovery, new ResourceDelta(2, 2, 0, 2),
                new ResourceDelta(0, 1, 0, 1), 3, "3日后 · 惦念", "你们又靠近了一点", true),
            new CardSpec("celebrate", "庆祝一个小进展", CardKind.Temptation, new ResourceDelta(-1, 3),
                new ResourceDelta(0, 1, 0, 1), 2, "2日后 · 余温", "快乐也留下了连接")
        };
        public static CardSpec[] ForOutlookDay(int day, int seed = 0, int catalogVersion = 3)
        {
            if (day < 13 || day > 30) throw new ArgumentOutOfRangeException("day");
            if (catalogVersion >= 6) return CampaignContent.LongHand(day, seed);
            return new[] { OutlookActions[5], OutlookActions[day < 21 ? 0 : day < 28 ? 1 : 2],
                OutlookActions[day % 2 == 0 ? 3 : 4] };
        }
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

        private static readonly CardSpec[] NewActions =
        {
            new CardSpec("play", "玩一局", CardKind.Temptation,
                new ResourceDelta(-1, 3), new ResourceDelta(0, -1), 1,
                "明天  ·  余兴", "快乐留下了一点余温"),
            new CardSpec("mentor", "请教前辈", CardKind.Growth,
                new ResourceDelta(-2, 0, 1, 0, -1), new ResourceDelta(0, 0, 1, 1, 0, 2), 2,
                "2日后  ·  指引", "一个建议打开了方向", true),
            new CardSpec("nightwalk", "夜里散步", CardKind.Recovery,
                new ResourceDelta(2, 2), new ResourceDelta(1, 0, 1), 1,
                "明天  ·  呼吸", "夜风让思绪清楚了一点"),
            new CardSpec("shortstudy", "利用半小时", CardKind.Growth,
                new ResourceDelta(-1), new ResourceDelta(0, 0, 1, 0, 0, 1), 1,
                "明天  ·  小芽", "短短的投入也留下了成长"),
            new CardSpec("smalljob", "短时帮忙", CardKind.Growth,
                new ResourceDelta(-2, 0, 0, 1, 2), new ResourceDelta(0, 0, 1, 0, 0, 1), 1,
                "明天  ·  合作", "一次合作留下了经验", true)
        };

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

        public static CardSpec[] ForDay(int day, int runNumber, int catalogVersion = 2, int worldSeed = 0)
        {
            if (day < 1 || day > 12) throw new ArgumentOutOfRangeException("day");
            if (runNumber < 1) throw new ArgumentOutOfRangeException("runNumber");
            if (day == 1 && runNumber == 1)
                return new[] { Temptations[0], Growth[0], Recovery[0] };

            int index = (day - 1 + runNumber - 1) % Temptations.Length;
            CardSpec[] hand = new[] { Temptations[index], Growth[index], Recovery[index] };
            // Each six-day deck contains all six actions once per intent. Different
            // streams prevent the three columns from forming a repeatable recipe.
            // The first life and catalogs 1–4 retain their exact recorded rules.
            if (catalogVersion >= 5 && runNumber >= 2)
            {
                hand[0] = Temptations[DeckIndex(day, worldSeed, 0)];
                hand[1] = Growth[DeckIndex(day, worldSeed, 1)];
                hand[2] = Recovery[DeckIndex(day, worldSeed, 2)];
                if (runNumber >= 3)
                {
                    if (day == 2 || day == 5) hand[0] = SeasonActions[2 + DeckIndex(day, worldSeed, 3) % 2];
                    if (day == 3 || day == 8) hand[1] = SeasonActions[DeckIndex(day, worldSeed, 4) % 2];
                    // Keep the deck's relationship opportunities when adding rest.
                    if (day == 5 || day == 11) hand[2] = SeasonActions[hand[2].GivesSupport ? 5 : 4];
                }
            }
            if (catalogVersion >= 2 && runNumber >= 2)
            {
                if (day == 8) hand[1] = NewActions[1];
                if (day == 10) hand[0] = NewActions[0];
                if (day == 11) hand[2] = NewActions[2];
            }
            if (catalogVersion >= 3 && hand[0].Id == "play") hand[0] = BalancedPlay;
            return hand;
        }

        private static int DeckIndex(int day, int seed, int stream)
        {
            int[] deck = { 0, 1, 2, 3, 4, 5 };
            unchecked
            {
                uint x = (uint)seed ^ (uint)(stream + 1) * 0x9e3779b9u ^ (uint)((day - 1) / 6 + 1) * 0x85ebca6bu;
                for (int i = 5; i > 0; i--)
                {
                    x += 0x9e3779b9u; uint z = x;
                    z ^= z >> 16; z *= 0x7feb352du; z ^= z >> 15; z *= 0x846ca68bu; z ^= z >> 16;
                    int j = (int)(z % (uint)(i + 1));
                    int value = deck[i]; deck[i] = deck[j]; deck[j] = value;
                }
            }
            return deck[(day - 1) % 6];
        }

        public static CardSpec FindById(string id)
        {
            CardSpec master = Array.Find(MasterContent.Actions, candidate => candidate.Id == id);
            if (master != null) return master;
            CardSpec story = Array.Find(CampaignContent.Actions, candidate => candidate.Id == id);
            if (story != null) return story;
            CardSpec seasonal = Array.Find(SeasonActions, candidate => candidate.Id == id);
            if (seasonal != null) return seasonal;
            CardSpec outlook = Array.Find(OutlookActions, candidate => candidate.Id == id);
            if (outlook != null) return outlook;
            if (id == SoloRecovery.Id) return SoloRecovery;
            if (id == Opportunity.Id) return Opportunity;
            if (id == Together.Id) return Together;
            CardSpec extra = Array.Find(NewActions, candidate => candidate.Id == id);
            if (extra != null) return extra;
            CardSpec card = Array.Find(Temptations, candidate => candidate.Id == id);
            if (card != null) return card;
            card = Array.Find(Growth, candidate => candidate.Id == id);
            return card ?? Array.Find(Recovery, candidate => candidate.Id == id);
        }
    }
}
