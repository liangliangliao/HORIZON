using System;
using System.Linq;

namespace Horizon.Game
{
    public static class CampaignContent
    {
        public static readonly CardSpec[] Actions = {
            new CardSpec("create", "把灵感做出来", CardKind.Growth, new ResourceDelta(-1, -1),
                new ResourceDelta(0, 1, 1, 0, 0, 2), 2, "2日后 · 创作", "快乐变成了一个作品"),
            new CardSpec("budget", "留一笔余钱", CardKind.Recovery, new ResourceDelta(2, 1, 0, 0, 1),
                new ResourceDelta(0, 0, 1), 2, "2日后 · 从容", "留出的空间让选择变多了"),
            new CardSpec("resilience", "重新试一次", CardKind.Growth, new ResourceDelta(-2),
                new ResourceDelta(0, 1, 1, 0, 0, 2), 3, "3日后 · 突破", "休息后的尝试走得更远"),
            new CardSpec("teach", "分享我的经验", CardKind.Growth, new ResourceDelta(-2, 0, 0, 1),
                new ResourceDelta(0, 1, 1, 1, 1, 1), 3, "3日后 · 回应", "你的经验成为了别人的支持", true),
            new CardSpec("commission", "接一个小项目", CardKind.Growth, new ResourceDelta(-2, 0, 0, 0, -1),
                new ResourceDelta(0, 0, 1, 1, 3, 2), 4, "4日后 · 交付", "一段稳定投入换来了作品与收入", true),
            new CardSpec("visit", "去看看老朋友", CardKind.Recovery, new ResourceDelta(2, 1, 0, 2),
                new ResourceDelta(0, 1, 0, 1), 2, "2日后 · 陪伴", "那次见面给后来留了支援", true),
            new CardSpec("garden", "照顾窗边的花", CardKind.Recovery, new ResourceDelta(3, 1),
                new ResourceDelta(0, 1, 1), 3, "3日后 · 生长", "一件小事也能留下安定"),
            new CardSpec("concert", "去听一场演出", CardKind.Temptation, new ResourceDelta(-1, 3, 0, 0, -1),
                new ResourceDelta(0, 1, 0, 1), 2, "2日后 · 共鸣", "快乐让你和别人靠近了一点")
        };
        public static readonly WorldEventSpec[] World = WorldEvents.Season.Concat(new[] {
            new WorldEventSpec(7, 45, "街角的作品让你想动手试试", "create", CardKind.Growth),
            new WorldEventSpec(9, 50, "老朋友发来问候", "visit", CardKind.Recovery),
            new WorldEventSpec(15, 55, "有人想和你一起完成一个项目", "collaborate", CardKind.Growth),
            new WorldEventSpec(17, 45, "一个需要耐心的小项目出现了", "commission", CardKind.Growth),
            new WorldEventSpec(20, 50, "今天可以给自己留一个安静周末", "weekend", CardKind.Recovery),
            new WorldEventSpec(23, 60, "朋友想听听你积累的经验", "teach", CardKind.Growth),
            new WorldEventSpec(25, 40, "附近有一场你喜欢的演出", "concert", CardKind.Temptation),
            new WorldEventSpec(29, 55, "有人想看看你的成果", "publish", CardKind.Growth)
        }).OrderBy(e => e.Day).ToArray();

        public static CardSpec[] LongHand(int day, int seed)
        {
            string[] joy = { "celebrate", "music", "concert", "play" };
            string[] growth = { "project", "collaborate", "publish", "teach", "commission", "resilience" };
            string[] recovery = { "weekend", "reconnect", "visit", "garden", "breathing", "listen" };
            int Pick(int stream, int count) { unchecked {
                uint x = (uint)seed ^ (uint)day * 0x9e3779b9u ^ (uint)stream * 0x85ebca6bu;
                x ^= x >> 16; x *= 0x7feb352du; x ^= x >> 15; return (int)(x % (uint)count);
            } }
            return new[] { CardCatalog.FindById(joy[Pick(1, joy.Length)]),
                CardCatalog.FindById(growth[Pick(2, growth.Length)]), CardCatalog.FindById(recovery[Pick(3, recovery.Length)]) };
        }
        public static string ActName(int day)
        { return day <= 12 ? "种下选择" : day <= 21 ? "让生活展开" : "带着过去抵达"; }
        public static string StationVoice(GameSession life, int stage, bool question)
        {
            if (question) return "「这一次，你最想留住什么？」";
            if (life.Day == 4 && life.RunNumber == 3 && stage == 2) return "「现在你终于看见我了。」";
            if (life.Deadline == 30 && life.Day >= 12) {
                string[] lines = life.Day == 12 ? new[] { "「十二天以后，路还没有结束。」", "「你留下的种子，可以走得更远。」", "「我们还有十八天，创造一个不同的自己。」" } :
                    life.Day == 14 ? new[] { "「原来长大，是继续照顾开始的东西。」", "「成果、朋友和休息，正在互相连接。」", "「别急着抵达。先把今天活好。」" } :
                    life.Day == 21 ? new[] { "「我已经记得，你是怎样走来的。」", "「有些小事，比当时想的留得更久。」", "「最后九天，仍然有新的选择。」" } :
                    new[] { "「就快到我们约好的那一天了。」", "「有些回声还在路上，有些已经成为了你。」", "「无论几道门亮起，这段人生都值得留下。」" };
                return lines[Math.Min(2, stage)];
            }
            if (stage == 0) return life.RunNumber == 1 ? "「你终于来了。」" : life.RunNumber == 2 ? "「这次，你带着一点远见来了。」" : "「我记得，你又选择了一条路。」";
            if (stage == 2) return life.RunNumber >= 3 ? "「现在你终于看见我了。」" : "「有些改变，现在才刚刚开始。」";
            int growth = life.Actions.Count(a => a.kind == CardKind.Growth);
            int rest = life.Actions.Count(a => a.kind == CardKind.Recovery);
            return life.Energy <= 2 ? "「你走得很用力。我也想让你歇一会儿。」" :
                growth > rest ? "「你留下的努力，正在给我一个形状。」" :
                life.SupportActions >= 2 ? "「你靠近过的人，也在靠近未来的我。」" :
                rest >= 2 ? "「那些留给自己的时间，我都记得。」" : "「快乐也会留下东西。看看它去了哪里。」";
        }
        public static string StationSecondVoice(GameSession life, int stage)
        {
            ActionRecord last = life.Actions.LastOrDefault();
            if (stage == 0) return "「走近一点。这里没有标准答案。」";
            if (stage == 1 && last != null) return "「第" + last.day + "天的「" + last.cardName + "」，也是这条路的一部分。」";
            return life.Deadline == 30 ? "「我们会在第30天，再看一看整条路。」" : "「下一次选择，仍然在你手里。」";
        }
    }
}
