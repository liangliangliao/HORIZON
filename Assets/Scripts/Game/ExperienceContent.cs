using System;
using System.Collections.Generic;
using System.Linq;

namespace Horizon.Game
{
    public static class CausalGraph
    {
        public static List<string> Parents(CausalNode node)
        {
            var result = new List<string>();
            if (node == null) return result;
            if (!string.IsNullOrEmpty(node.parentId)) result.Add(node.parentId);
            if (node.parentIds != null)
                foreach (string id in node.parentIds)
                    if (!string.IsNullOrEmpty(id) && id != node.id && !result.Contains(id)) result.Add(id);
            return result;
        }

        public static void Link(CausalNode node, string parent)
        {
            if (node == null || string.IsNullOrEmpty(parent) || node.id == parent) return;
            if (string.IsNullOrEmpty(node.parentId)) node.parentId = parent;
            if (node.parentIds == null) node.parentIds = new List<string>();
            if (!node.parentIds.Contains(parent)) node.parentIds.Add(parent);
        }

        public static List<CausalNode> Ancestors(List<CausalNode> graph, string id)
        {
            var visited = new HashSet<string>();
            var pending = new Stack<string>();
            pending.Push(id ?? "");
            while (pending.Count > 0 && visited.Count <= (graph?.Count ?? 0))
            {
                string next = pending.Pop();
                if (!visited.Add(next)) continue;
                CausalNode node = graph?.Find(n => n != null && n.id == next);
                if (node == null) continue;
                foreach (string parent in Parents(node)) pending.Push(parent);
            }
            return Ordered(graph?.FindAll(n => n != null && visited.Contains(n.id)));
        }

        public static List<CausalNode> Descendants(List<CausalNode> graph, string id)
        {
            var visited = new HashSet<string> { id ?? "" };
            bool changed;
            do
            {
                changed = false;
                if (graph == null) break;
                foreach (CausalNode node in graph)
                    if (node != null && !visited.Contains(node.id) && Parents(node).Exists(visited.Contains))
                    { visited.Add(node.id); changed = true; }
            } while (changed);
            return Ordered(graph?.FindAll(n => n != null && visited.Contains(n.id)));
        }

        private static List<CausalNode> Ordered(List<CausalNode> nodes)
        {
            return nodes == null ? new List<CausalNode>() : nodes.OrderBy(n => n.day).ToList();
        }
    }

    [Serializable]
    public sealed class JourneyProgress
    {
        public List<string> activeDates = new List<string>();
        public int Chapter { get { return Math.Min(7, activeDates?.Count ?? 0); } }
        public bool Visit(string localDate)
        {
            if (activeDates == null) activeDates = new List<string>();
            if (string.IsNullOrEmpty(localDate) || activeDates.Contains(localDate)) return false;
            activeDates.Add(localDate);
            return true;
        }
        public static string Name(int chapter)
        {
            string[] names = { "现在", "一天后的回声", "三天后的自己", "二阶影响", "未来的范围", "平行的道路", "三十天后的自己" };
            return names[Math.Max(0, Math.Min(6, chapter - 1))];
        }
    }

    [Serializable]
    public sealed class RareMoment
    {
        public int runNumber;
        public int day;
        public int type;
        public string memory;
        public string title;
        public string description;
    }

    public static class ExperienceContent
    {
        public static string NextStep(GameSession session)
        {
            if (session.Actions.Count == 0) return "今天先选一张，看看它会去哪一天";
            if (!session.Actions.Exists(a => a.echoed))
            {
                PendingEcho next = session.Pending.OrderBy(e => e.dueDay).FirstOrDefault();
                return next == null ? "今天照顾了自己，也可以试试种下一个回声" : "第 " + next.dueDay + " 天，你留下的选择会回来";
            }
            if (!session.StationVisited && session.Day <= 4) return "第 4 天，走进未来站见一个人";
            if (session.Day >= 10) return "截止日快到了，看看三道门还需要什么";
            return "成长、恢复、关系：今天想照顾哪一条路？";
        }

        public static string CardPurpose(CardSpec card, int day)
        {
            if (card.Delay > 0 && day + card.Delay > GameSession.LastDay)
                return "这次回声在截止日之后回来，今天的变化仍会发生。";
            if (card.GivesSupport) return "这条路会留下支援；朋友与成长可以产生新的连接。";
            if (card.Kind == CardKind.Growth) return "今天投入精力，等回声回来，为能力门留下成长。";
            if (card.Kind == CardKind.Recovery) return "先恢复状态，给下一次选择留出空间。";
            return "先得到快乐，也给之后的自己留下一点负担。";
        }

        public static List<RunRecord> Search(List<RunRecord> runs, string query, int filter)
        {
            query = (query ?? "").Trim();
            return (runs ?? new List<RunRecord>()).Where(r => r != null &&
                (filter == 0 || filter == 1 && r.boss?.passed == 3 || filter == 2 && r.boss?.passed < 3) &&
                (query.Length == 0 || (r.title ?? "").IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    r.number.ToString().Contains(query) || r.actions != null && r.actions.Exists(a =>
                        a != null && (a.cardName ?? "").IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)))
                .OrderByDescending(r => r.number).ToList();
        }

        public static List<ActionRecord> StationMemories(GameSession session)
        {
            var result = new List<ActionRecord>();
            foreach (CardKind kind in new[] { CardKind.Growth, CardKind.Recovery, CardKind.Temptation })
            {
                ActionRecord best = session.Actions.Where(a => a.kind == kind).OrderByDescending(a =>
                    CausalGraph.Descendants(session.CausalNodes, a.nodeId).Count).ThenByDescending(a => a.day).FirstOrDefault();
                if (best != null) result.Add(best);
            }
            return result;
        }

        public static RareMoment Moment(int run, int day, List<RunRecord> runs)
        {
            int type = (int)(MomentHash(run) % 4);
            string memory = "一个还没有发生的可能";
            if (runs != null && runs.Count > 0)
            {
                RunRecord past = runs[(run + day) % runs.Count];
                ActionRecord action = past.actions?.Find(a => a.day == 3) ?? past.actions?.FirstOrDefault();
                if (action != null) memory = "RUN " + past.number.ToString("000") + " · D" + action.day + " · " + action.cardName;
            }
            string[] names = { "时间裂缝", "另一个我", "记忆错位", "尚未找到的因" };
            string[] lines = {
                "地平线裂开了一瞬。你看见一扇还没有打开的门。\n未来只是短暂泄漏，并没有替你决定。",
                "另一条时间线里的你，刚刚走过这里。\n他走向了不同的路，你也还有自己的选择。",
                "这不是今天的记忆，却留下了熟悉的温度。\n" + memory,
                "一道光先抵达了。你还不知道，它来自哪次选择。\n接下来的路，仍然可以改变。"
            };
            return new RareMoment { runNumber = run, day = day, type = type, memory = memory,
                title = names[type], description = lines[type] };
        }

        public static int RareGap(int run) { return 3 + (int)((MomentHash(run) >> 8) % 3); }
        private static uint MomentHash(int run)
        {
            unchecked
            {
                uint state = (uint)run * 747796405u + 2891336453u;
                uint word = ((state >> ((int)(state >> 28) + 4)) ^ state) * 277803737u;
                return (word >> 22) ^ word;
            }
        }

        public static string ShareLine(RunRecord run)
        {
            if (run?.boss != null && run.actions?.Count == 12)
                foreach (ActionRecord source in run.actions)
                    foreach (CardSpec card in GameSession.AlternativesForDay(run, source.day))
                    {
                        RunRecord other = GameSession.ReplayAlternative(run, source.day, card.Id);
                        if (other != null && (other.boss.ability != run.boss.ability || other.boss.state != run.boss.state ||
                            other.boss.support != run.boss.support))
                            return "Day " + source.day + " 的一个选择，改变了 Day 12。";
                    }
            return "这一次，我走出了自己的时间线。";
        }
    }
}
