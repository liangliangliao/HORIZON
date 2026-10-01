using System;
using System.Collections.Generic;
using System.Linq;

namespace Horizon.Game
{
    public sealed class MemoryChain
    {
        public readonly ActionRecord Origin;
        public readonly List<CausalNode> Nodes;
        private readonly List<CausalNode> consequences;
        public MemoryChain(ActionRecord origin, List<CausalNode> graph)
        {
            Origin = origin;
            // Hidden provenance stays hidden in every presentation, including the station.
            List<CausalNode> observed = CausalGraph.ObservedGraph(graph);
            var descendants = CausalGraph.Descendants(observed, origin.nodeId);
            consequences = descendants;
            var connected = new HashSet<string>();
            foreach (CausalNode node in descendants)
                foreach (CausalNode ancestor in CausalGraph.Ancestors(observed, node.id)) connected.Add(ancestor.id);
            Nodes = observed.Where(n => connected.Contains(n.id)).OrderBy(n => n.day).ThenBy(n => n.depth).ToList();
        }
        public string Summary { get { return "D" + Origin.day + "「" + Origin.cardName + "」 · " + Nodes.Count + " 个相连节点"; } }
        public string Reflection
        {
            get {
                CausalNode changed = consequences.FindLast(n => n.resolved && n.type == CausalNodeKind.Choice);
                CausalNode arrived = consequences.FindLast(n => n.resolved && n.type == CausalNodeKind.Echo);
                CausalNode waiting = consequences.Find(n => !n.resolved);
                return changed != null ? "「你留下的不只是一次变化。D" + changed.day + "，后来能选的路也变了。」" :
                    arrived != null ? "「从 D" + Origin.day + " 开始的这条路，已经走到了 D" + arrived.day + "。」" :
                    waiting != null ? "「这道光还在走向 D" + waiting.day + "。你不用停在原地等它。」" :
                    "「那天，你给自己留出了空间。这也属于你走过的路。」";
            }
        }
    }

    public sealed class GhostBeat
    {
        public int Day;
        public string Title, Meaning;
        public ResourceDelta Before, After;
        public bool Deadline;
    }

    public sealed class GhostStory
    {
        public readonly RunRecord Original, Alternative;
        public readonly List<GhostBeat> Beats;
        public GhostStory(RunRecord original, RunRecord alternative, List<GhostBeat> beats)
        { Original = original; Alternative = alternative; Beats = beats; }
    }

    public static class CausalPresentation
    {
        public static List<MemoryChain> Station(GameSession life)
        { return ExperienceContent.StationMemories(life).Select(a => new MemoryChain(a, life.CausalNodes)).ToList(); }

        public static string NodeMeaning(CausalNode node)
        {
            if (!node.resolved) return "还在路上 · 当天尚未到来\n" +
                (node.type == CausalNodeKind.Echo ? "这是已经种下的回声；之后的选择仍由你决定。" : "它可能改变之后可选择的行动。");
            if (node.type == CausalNodeKind.Choice) return "后来的选择空间发生了变化\n" +
                (node.effectRecorded ? PlayExperience.NowLabel(node.effect) : "这次变化已留在时间线上。");
            return "已经发生\n" + (node.effectRecorded ? PlayExperience.NowLabel(node.effect) : "这里留下了一个选择或机会，没有单独记录状态变化。");
        }

        // Replay once, then derive every frame from actual clamped changes. No
        // invented chain or resource payout is used to make a branch look better.
        public static GhostStory Ghost(RunRecord original)
        {
            GhostTimeline ghost = original?.boss?.ghostTimeline;
            if (ghost == null) return null;
            RunRecord alternate = GameSession.ReplayAlternative(original, ghost.sourceDay, ghost.alternativeId);
            if (alternate == null) return null;
            RunRecord baseline = GameSession.ReplayChoices(original, new Dictionary<int, string>());
            if (baseline == null) return null;
            var beats = new List<GhostBeat>();
            ResourceDelta previousGap = new ResourceDelta();
            int length = GameSession.RunLength(original);
            for (int day = 1; day <= length; day++)
            {
                ResourceDelta before = StateAt(baseline, day), after = StateAt(alternate, day);
                ResourceDelta gap = Subtract(after, before);
                ActionRecord oldAction = baseline.actions[day - 1], newAction = alternate.actions[day - 1];
                bool actionChanged = oldAction.cardId != newAction.cardId;
                bool consequenceChanged = !Same(gap, previousGap);
                previousGap = gap;
                if (day < ghost.sourceDay || day != ghost.sourceDay && day != length && !actionChanged && !consequenceChanged) continue;
                var changedEvents = alternate.causalNodes.Where(n => n.day == day && n.resolved &&
                    n.type != CausalNodeKind.Action && n.type != CausalNodeKind.Gate && n.effectRecorded &&
                    !baseline.causalNodes.Any(b => b.day == n.day && b.type == n.type && b.cardId == n.cardId &&
                        b.label == n.label && Same(b.effect, n.effect))).ToList();
                string meaning = actionChanged ? "原来：「" + oldAction.cardName + "」\n这条路：「" + newAction.cardName + "」" :
                    "当天仍选「" + newAction.cardName + "」，但之前的变化已经带到了这里。";
                foreach (CausalNode node in changedEvents) meaning += "\n" + node.label + " · " + PlayExperience.NowLabel(node.effect);
                ActionRecord removed = baseline.actions.Find(a => a.echoed && a.echoDay == day &&
                    !alternate.actions.Exists(b => b.echoed && b.echoDay == day && b.cardId == a.cardId && b.day == a.day));
                if (removed != null) meaning += "\n原来的「" + removed.echoName + "」在这条路上没有发生。";
                bool final = day == length;
                if (final) meaning += "\n\n" + GateChange(baseline.boss, alternate.boss);
                beats.Add(new GhostBeat { Day = day, Title = final ? "另一条路抵达了截止日" :
                    day == ghost.sourceDay ? "如果这一天，换一个选择" : actionChanged ? "后来能走的路变了" : "先前的变化，在这里继续",
                    Meaning = meaning, Before = before, After = after, Deadline = final });
            }
            return new GhostStory(original, alternate, beats);
        }

        public static string GateChange(BossResult before, BossResult after)
        {
            string[] names = { "能力", "状态", "支援" };
            bool[] old = { before.ability, before.state, before.support }, current = { after.ability, after.state, after.support };
            var lines = new List<string>();
            for (int i = 0; i < 3; i++) if (old[i] != current[i])
                lines.Add(names[i] + "门：" + (current[i] ? "这次打开了" : "这次尚未打开"));
            return string.Join("\n", lines) + (lines.Count > 0 ? "\n" : "") +
                before.passed + " 道门 → " + after.passed + " 道门\n" +
                (after.passed > before.passed ? "一个选择，改变了后来的抵达。" : "这条路也需要继续准备，没有一个选择能保证整个未来。");
        }

        private static ResourceDelta StateAt(RunRecord run, int day)
        {
            var value = new ResourceDelta(6, 5, 2, 4, 5, 2);
            foreach (CausalNode node in run.causalNodes.Where(n => n.day <= day && n.resolved && n.effectRecorded && n.effect != null))
            { value.energy += node.effect.energy; value.mood += node.effect.mood; value.insight += node.effect.insight;
                value.relation += node.effect.relation; value.money += node.effect.money; value.ability += node.effect.ability; }
            return value;
        }
        private static ResourceDelta Subtract(ResourceDelta a, ResourceDelta b)
        { return new ResourceDelta(a.energy - b.energy, a.mood - b.mood, a.insight - b.insight,
            a.relation - b.relation, a.money - b.money, a.ability - b.ability); }
        private static bool Same(ResourceDelta a, ResourceDelta b)
        { return a != null && b != null && a.energy == b.energy && a.mood == b.mood && a.insight == b.insight &&
            a.relation == b.relation && a.money == b.money && a.ability == b.ability; }
    }
}
