using System;
using System.Collections.Generic;
using System.Linq;
using Horizon.Game;
using Horizon.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Horizon
{
    public sealed partial class HorizonApp
    {
        private string archiveQuery = "";
        private int archiveFilter;
        private readonly Dictionary<int, string> branchChoices = new Dictionary<int, string>();

        private void ShowBranchPlanner(RunRecord original, int day, bool reset = false)
        {
            if (reset) branchChoices.Clear();
            ArchiveSurface("Explore a possible life");
            View.Label(overlay, "Branch title", "如果这几天不同", 43, Palette.Text,
                TextAnchor.MiddleCenter, 0.06f, 0.883f, 0.94f, 0.967f);
            View.Label(overlay, "Branch rules", "可以改变多天的选择。其余行动沿用原来那一局；\n若机会消失，会改用当天的恢复行动。", 25, Palette.Muted,
                TextAnchor.MiddleCenter, 0.07f, 0.8f, 0.93f, 0.884f);
            View.Button(overlay, "Previous branch day", "<", () => ShowBranchPlanner(original, day - 1),
                0.07f, 0.713f, 0.2f, 0.782f, Palette.Panel, Palette.Text, 38).interactable = day > 1;
            View.Label(overlay, "Branch day", "DAY " + day.ToString("00"), 34, Palette.Mint,
                TextAnchor.MiddleCenter, 0.23f, 0.713f, 0.77f, 0.782f);
            View.Button(overlay, "Next branch day", ">", () => ShowBranchPlanner(original, day + 1),
                0.8f, 0.713f, 0.93f, 0.782f, Palette.Panel, Palette.Text, 38).interactable = day < 12;
            CardSpec[] choices = GameSession.AlternativesForDay(original, day, branchChoices);
            string selected = branchChoices.TryGetValue(day, out string changed) ? changed : original.actions[day - 1].cardId;
            for (int i = 0; i < choices.Length; i++)
            {
                CardSpec card = choices[i];
                float y = 0.609f - i * 0.084f;
                View.Button(overlay, "Branch choice " + card.Id, card.Name + (selected == card.Id ? " · 当前选择" : ""), () =>
                {
                    // Later custom choices may no longer exist after changing an earlier day.
                    foreach (int later in branchChoices.Keys.Where(d => d >= day).ToArray()) branchChoices.Remove(later);
                    if (card.Id != original.actions[day - 1].cardId) branchChoices[day] = card.Id;
                    ShowBranchPlanner(original, day);
                }, 0.07f, y, 0.93f, y + 0.072f, Palette.Panel,
                    selected == card.Id ? Palette.Mint : Palette.Text, 28);
            }
            RunRecord alternate = GameSession.ReplayChoices(original, branchChoices);
            string outcome = alternate == null ? "这组选择还不能走通，请回到前面的一天调整。" :
                "原来 → 这条可能未来\n" + GateComparison("能力", original.boss.ability, alternate.boss.ability) + "    " +
                GateComparison("状态", original.boss.state, alternate.boss.state) + "\n" +
                GateComparison("支援", original.boss.support, alternate.boss.support) + "\n" +
                "能力 " + original.finalAbility + " → " + alternate.finalAbility +
                " · 精力 " + original.finalEnergy + " → " + alternate.finalEnergy +
                " · 心情 " + original.finalMood + " → " + alternate.finalMood;
            View.Label(overlay, "Branch result", outcome, 27, Palette.Text,
                TextAnchor.MiddleCenter, 0.075f, 0.254f, 0.925f, 0.433f);
            int automaticChanges = alternate == null ? 0 : alternate.actions.Count(a =>
                !branchChoices.ContainsKey(a.day) && a.cardId != original.actions[a.day - 1].cardId);
            View.Label(overlay, "Branch assumption", "你改变了 " + branchChoices.Count + " 天" +
                (automaticChanges > 0 ? " · " + automaticChanges + " 次后续行动因机会变化而调整" : "") +
                "\n这里只是探索，不会改写那段人生。", 23, Palette.Muted,
                TextAnchor.MiddleCenter, 0.08f, 0.173f, 0.92f, 0.249f);
            View.Button(overlay, "Reset branch choices", "重新探索", () => ShowBranchPlanner(original, day, true),
                0.07f, 0.096f, 0.46f, 0.157f, Palette.Panel, Palette.Gold, 26);
            View.Button(overlay, "Inspect branch timeline", "看这条支线", () => ShowBranchSummary(alternate),
                0.5f, 0.096f, 0.93f, 0.157f, Palette.Panel, Palette.Mint, 26).interactable = alternate != null;
            View.Button(overlay, "Close branch planner", "回到时间地图", () => RenderMap(false),
                0.15f, 0.022f, 0.85f, 0.081f, Palette.Mint, Palette.Ink, 28);
            View.RefreshText(overlay);
        }

        private static string GateComparison(string name, bool before, bool after)
        { return name + " " + (before ? "亮" : "暗") + " → " + (after ? "亮" : "暗"); }

        private void ShowBranchSummary(RunRecord run)
        {
            if (run == null) return;
            ShowArchiveDetail("一条可能的时间线", string.Join("\n", run.actions.Select(a =>
                "D" + a.day + " · " + a.cardName + (a.echoed ? " → " + a.echoName : ""))));
        }

        private RunRecord MapRecord(bool duringRun)
        {
            if (duringRun && session != null) return new RunRecord
            {
                number = session.RunNumber, title = "正在发生", actions = session.Actions,
                causalNodes = session.CausalNodes, catalogVersion = session.CatalogVersion,
                worldSeed = session.WorldSeed, deckSeed = session.DeckSeed, deckSeedRecorded = true,
                finalEnergy = session.Energy, finalMood = session.Mood, finalAbility = session.Ability,
                prediction = session.Prediction
            };
            return archive.runs.Count == 0 ? null : archive.runs[Mathf.Clamp(mapIndex, 0, archive.runs.Count - 1)];
        }

        private RectTransform ArchiveSurface(string name)
        {
            if (overlay != null) Destroy(overlay.gameObject);
            overlay = View.Rect(root, name, 0, 0, 1, 1);
            View.Fill(overlay, "Archive background", Palette.Ink, 0, 0, 1, 1, true);
            return overlay;
        }

        private void RenderDetailedMap(bool duringRun)
        {
            ArchiveSurface("Time map");
            RunRecord run = MapRecord(duringRun);
            List<ActionRecord> actions = run?.actions ?? new List<ActionRecord>();
            List<CausalNode> graph = CausalGraph.ObservedGraph(GameSession.GraphForRun(run));
            View.Label(overlay, "Map title", "时 间 地 图", 43, Palette.Text,
                TextAnchor.MiddleCenter, 0.06f, 0.915f, 0.94f, 0.975f);
            View.Label(overlay, "Run title", run == null ? "还没有走过的时间线" :
                "RUN " + run.number.ToString("000") + " · " + run.title, 29, Palette.Mint,
                TextAnchor.MiddleCenter, 0.075f, 0.856f, 0.925f, 0.915f);
            View.Label(overlay, "Tap a day", "点开一天，看看它怎样回到你身边。", 23, Palette.Muted,
                TextAnchor.MiddleCenter, 0.08f, 0.813f, 0.92f, 0.852f);
            foreach (CausalNode node in graph)
            {
                foreach (string id in CausalGraph.Parents(node))
                {
                    CausalNode parent = graph.Find(n => n.id == id);
                    if (parent == null || parent.day > 12 || node.day > 12) continue;
                    Vector2 from = new Vector2(0.775f + (graph.IndexOf(parent) % 3) * 0.055f,
                        0.801f - (parent.day - 1) * 0.047f);
                    Vector2 to = new Vector2(0.775f + (graph.IndexOf(node) % 3) * 0.055f,
                        0.801f - (node.day - 1) * 0.047f);
                    Color color = NodeColor(node); color.a = node.resolved ? 0.58f : 0.22f;
                    TimeThreadGraphic thread = View.Rect(overlay, "A causal connection", 0, 0, 1, 1).gameObject.AddComponent<TimeThreadGraphic>();
                    thread.From = from; thread.To = to; thread.color = color; thread.raycastTarget = false;
                }
            }
            for (int day = 1; day <= 12; day++)
            {
                ActionRecord action = actions.Find(a => a.day == day);
                float y = 0.779f - (day - 1) * 0.047f;
                Button row = View.Button(overlay, "Inspect day " + day, "", () => ShowActionDetail(action, graph, actions),
                    0.055f, y, 0.735f, y + 0.041f, Palette.Panel, Palette.Text);
                row.interactable = action != null;
                View.Label(row.transform, "Day", day.ToString("00"), 26, Palette.Muted,
                    TextAnchor.MiddleCenter, 0.015f, 0.09f, 0.105f, 0.9f);
                View.Label(row.transform, "Action", action?.cardName ?? "尚未到来", 26,
                    action == null ? Palette.Muted : Palette.Text, TextAnchor.MiddleLeft, 0.13f, 0.35f, 0.94f, 0.94f);
                View.Label(row.transform, "Echo date", action == null ? "" : action.echoDay == 0 ? "当下的恢复" :
                    "→ D" + action.echoDay + (action.echoed ? " · 已回来" : action.echoDay > 12 ? " · 截止日之后" : " · 等待回声"),
                    19, action?.echoed == true ? Palette.Mint : Palette.Muted,
                    TextAnchor.MiddleLeft, 0.13f, 0.04f, 0.94f, 0.42f);
            }
            if (run != null)
            {
                View.Button(overlay, "Causal network", "展开因果网络", () => ShowCausalNetwork(run, duringRun),
                    0.07f, 0.169f, 0.93f, 0.224f, Palette.Panel, Palette.Mint, 28);
                if (!duringRun)
                {
                    View.Button(overlay, "Browse lives", "找以前的人生", ShowArchiveBrowser,
                        0.07f, 0.102f, 0.48f, 0.158f, Palette.Panel, Palette.Text, 25);
                    View.Button(overlay, "Share life", "十秒时间动画", () => ShowShareStory(run, () => RenderMap(false)),
                        0.52f, 0.102f, 0.93f, 0.158f, Palette.Panel, Palette.Gold, 25);
                    View.Button(overlay, "Rename", "改标题", () => ShowRenameRun(run),
                        0.78f, 0.92f, 0.95f, 0.966f, Palette.Deep, Palette.Mint, 20);
                }
            }
            View.Button(overlay, "Close map", "返回", () => { Destroy(overlay.gameObject); overlay = null; },
                0.2f, 0.027f, 0.8f, 0.087f, Palette.Mint, Palette.Ink, 29);
            View.RefreshText(overlay);
        }

        private static Color NodeColor(CausalNode node)
        {
            if (node.type == CausalNodeKind.Gate) return node.gatePassed ? Palette.Mint : Palette.Coral;
            if (node.type == CausalNodeKind.Choice || node.type == CausalNodeKind.Situation) return Palette.Gold;
            CardSpec card = CardCatalog.FindById(node.cardId);
            return card?.Kind == CardKind.Temptation ? Palette.Coral : card?.Kind == CardKind.Recovery ? Palette.Gold : Palette.Mint;
        }

        private void ShowActionDetail(ActionRecord action, List<CausalNode> graph, List<ActionRecord> actions)
        {
            if (action == null) return;
            CausalNode node = graph.Find(n => n.id == action.nodeId);
            string description = action.actualNowRecorded ? "当时实际变化\n" + PlayExperience.NowLabel(action.actualNow) :
                "行动牌的效果\n" + PlayExperience.NowLabel(action.now);
            if (action.echoDay > 0)
                description += "\n\nD" + action.echoDay + (action.echoed ? " · 回声已经回来\n" + action.echoName +
                    "\n" + PlayExperience.NowLabel(action.actualLaterRecorded ? action.actualLater : action.later) :
                    action.echoDay > 12 ? " · 超过本局截止日，尚未兑现" : " · 回声还没有回来");
            if (node != null)
            {
                List<CausalNode> parents = CausalGraph.Ancestors(graph, node.id).FindAll(n => n.id != node.id);
                if (parents.Count > 0) description += "\n\n这次选择从哪里来\n" + NodeList(parents);
                List<CausalNode> consequences = CausalGraph.Descendants(graph, node.id).FindAll(n => n.id != node.id);
                if (consequences.Count > 0) description += "\n\n它后来去了哪里\n" + NodeList(consequences);
            }
            RunRecord completed = archive.runs.Find(r => r.actions == actions);
            ShowArchiveDetail("D" + action.day + " · " + action.cardName, description,
                completed == null ? null : (Action)(() => ShowBranchPlanner(completed, action.day, true)));
        }

        private static string NodeList(List<CausalNode> nodes)
        {
            return string.Join("\n", nodes.Select(n => "D" + n.day + " · " +
                (n.resolved ? n.label : "尚未回来的回声")));
        }

        private void ShowArchiveDetail(string title, string description, Action explore = null)
        {
            RectTransform modal = View.Rect(overlay, "Event details", 0, 0, 1, 1);
            View.Fill(modal, "Details shade", new Color(0.005f, 0.02f, 0.035f, 0.95f), 0, 0, 1, 1, true);
            RectTransform panel = View.Panel(modal, "Details sheet", Palette.Panel,
                0.04f, 0.075f, 0.96f, 0.91f, 35).rectTransform;
            panel.gameObject.AddComponent<PanelEntrance>();
            View.Label(panel, "Event title", title, 39, Palette.Text,
                TextAnchor.MiddleLeft, 0.065f, 0.79f, 0.935f, 0.955f);
            ResultText(panel, description);
            if (explore != null) View.Button(panel, "Explore another choice", "试试另一种选择", explore,
                0.075f, 0.19f, 0.925f, 0.29f, Palette.Deep, Palette.Gold, 28);
            else View.Label(panel, "Details note", "每条线，都连接着一次真实的选择。", 25, Palette.Muted,
                TextAnchor.MiddleCenter, 0.07f, 0.19f, 0.93f, 0.28f);
            View.Button(panel, "Close event details", "回到时间线", () => Destroy(modal.gameObject),
                0.075f, 0.045f, 0.925f, 0.142f, Palette.Mint, Palette.Ink, 29);
            View.RefreshText(modal);
        }

        private void ShowCausalNetwork(RunRecord run, bool duringRun)
        {
            ArchiveSurface("Causal network");
            List<CausalNode> graph = CausalGraph.ObservedGraph(GameSession.GraphForRun(run));
            View.Label(overlay, "Network title", "你留下的因果网络", 43, Palette.Text,
                TextAnchor.MiddleCenter, 0.06f, 0.865f, 0.94f, 0.955f);
            View.Label(overlay, "Network guide", "拖动查看，点一个节点追溯来路。", 25, Palette.Muted,
                TextAnchor.MiddleCenter, 0.07f, 0.806f, 0.93f, 0.862f);
            RectTransform viewport = View.Rect(overlay, "Network window", 0.025f, 0.14f, 0.975f, 0.794f);
            viewport.gameObject.AddComponent<RectMask2D>();
            Image hit = viewport.gameObject.AddComponent<Image>(); hit.color = Palette.Deep;
            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport; scroll.horizontal = scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            int columns = Mathf.Max(3, graph.GroupBy(n => n.day).Select(g => g.Count()).DefaultIfEmpty(3).Max());
            float width = columns * 260 + 100, height = 12 * 135 + 60;
            RectTransform content = View.Rect(viewport, "Network canvas", 0, 1, 0, 1);
            content.pivot = new Vector2(0, 1); content.sizeDelta = new Vector2(width, height);
            scroll.content = content;
            var positions = new Dictionary<string, Vector2>();
            for (int day = 1; day <= 12; day++)
            {
                int col = 0;
                foreach (CausalNode node in graph.FindAll(n => n.day == day))
                {
                    positions[node.id] = new Vector2((60 + col++ * 260 + 105) / width, (height - 45 - (day - 1) * 135 - 40) / height);
                }
            }
            foreach (CausalNode node in graph)
            {
                if (!positions.ContainsKey(node.id)) continue;
                foreach (string id in CausalGraph.Parents(node))
                {
                    if (!positions.ContainsKey(id)) continue;
                    TimeThreadGraphic line = View.Rect(content, "Network edge", 0, 0, 1, 1).gameObject.AddComponent<TimeThreadGraphic>();
                    Vector2 parent = positions[id], child = positions[node.id];
                    bool sameColumn = Mathf.Abs(parent.x - child.x) < 0.001f;
                    float direction = child.x >= parent.x ? 1 : -1;
                    line.RightLane = sameColumn;
                    line.From = parent + Vector2.right * (105 / width) * (sameColumn ? 1 : direction);
                    line.To = child + Vector2.right * (105 / width) * (sameColumn ? 1 : -direction);
                    line.color = NodeColor(node); line.Thickness = 3;
                    line.raycastTarget = false;
                }
            }
            foreach (CausalNode node in graph)
            {
                if (!positions.TryGetValue(node.id, out Vector2 pos)) continue;
                Button button = View.Button(content, "Inspect node " + node.id, "D" + node.day + " · " +
                    (node.resolved ? node.label : "回声尚未到来"), () =>
                {
                    ActionRecord action = run.actions.Find(a => a.nodeId == node.id);
                    if (action != null) ShowActionDetail(action, graph, run.actions);
                    else ShowArchiveDetail(node.label, "它的来路\n" + (node.originHidden ? "还没看清，D9 再回来看看。" :
                        NodeList(CausalGraph.Ancestors(graph, node.id))) +
                        (node.effectRecorded ? "\n\n抵达时的实际变化\n" + PlayExperience.NowLabel(node.effect) : "") +
                        "\n\n接下来的连接\n" + NodeList(CausalGraph.Descendants(graph, node.id)));
                }, pos.x - 105 / width, pos.y - 40 / height, pos.x + 105 / width, pos.y + 40 / height,
                    Palette.Panel, NodeColor(node), 23);
            }
            View.Button(overlay, "Close causal network", "回到时间地图", () => RenderMap(duringRun),
                0.14f, 0.045f, 0.86f, 0.112f, Palette.Mint, Palette.Ink, 29);
            View.RefreshText(overlay);
        }

        private void ShowArchiveBrowser()
        {
            ArchiveSurface("Find a past life");
            View.Label(overlay, "Search title", "找一段走过的人生", 43, Palette.Text,
                TextAnchor.MiddleCenter, 0.06f, 0.867f, 0.94f, 0.955f);
            RectTransform field = View.Rect(overlay, "Search field", 0.07f, 0.762f, 0.73f, 0.824f);
            Image fieldBackground = field.gameObject.AddComponent<Image>(); fieldBackground.color = Palette.Panel;
            InputField input = field.gameObject.AddComponent<InputField>(); input.targetGraphic = fieldBackground;
            input.textComponent = View.Label(field, "Search text", "", 30, Palette.Text,
                TextAnchor.MiddleLeft, 0.05f, 0.09f, 0.95f, 0.91f);
            input.placeholder = View.Label(field, "Search placeholder", "标题、行动或局数", 27, Palette.Muted,
                TextAnchor.MiddleLeft, 0.05f, 0.09f, 0.95f, 0.91f);
            input.text = archiveQuery; input.characterLimit = 28;
            View.Button(overlay, "Search lives", "查找", () => { archiveQuery = input.text; ShowArchiveBrowser(); },
                0.76f, 0.762f, 0.93f, 0.824f, Palette.Mint, Palette.Ink, 25);
            string[] filters = { "全部", "三门点亮", "另一种可能" };
            for (int i = 0; i < filters.Length; i++)
            {
                int filter = i; float x = 0.07f + i * 0.292f;
                View.Button(overlay, "Filter lives " + i, filters[i], () =>
                { archiveQuery = input.text; archiveFilter = filter; ShowArchiveBrowser(); },
                    x, 0.683f, x + 0.275f, 0.741f, Palette.Panel, filter == archiveFilter ? Palette.Mint : Palette.Muted, 24);
            }
            RectTransform list = View.Rect(overlay, "Lives window", 0.055f, 0.15f, 0.945f, 0.661f);
            list.gameObject.AddComponent<RectMask2D>();
            Image hit = list.gameObject.AddComponent<Image>(); hit.color = new Color(0, 0, 0, 0);
            ScrollRect scroll = list.gameObject.AddComponent<ScrollRect>(); scroll.viewport = list;
            scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
            List<RunRecord> matches = ExperienceContent.Search(archive.runs, archiveQuery, archiveFilter);
            RectTransform content = View.Rect(list, "Past lives", 0, 1, 1, 1); content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = new Vector2(0, Mathf.Max(1, matches.Count) * 155);
            scroll.content = content;
            for (int i = 0; i < matches.Count; i++)
            {
                RunRecord run = matches[i];
                float top = 1 - i / (float)matches.Count, bottom = 1 - (i + 1) / (float)matches.Count;
                View.Button(content, "Open life " + run.number, "RUN " + run.number.ToString("000") + " · " + run.title +
                    "\n点亮 " + (run.boss?.passed ?? 0) + "/3 道门", () =>
                { mapIndex = archive.runs.IndexOf(run); RenderMap(false); },
                    0.025f, bottom + 0.02f / matches.Count, 0.975f, top - 0.02f / matches.Count, Palette.Panel, Palette.Text, 27);
            }
            if (matches.Count == 0) View.Label(list, "No matches", "这次没有找到。试试另一段记忆。", 29,
                Palette.Muted, TextAnchor.MiddleCenter, 0.05f, 0.35f, 0.95f, 0.6f);
            View.Button(overlay, "Close archive search", "回到时间地图", () => RenderMap(false),
                0.15f, 0.05f, 0.85f, 0.119f, Palette.Mint, Palette.Ink, 29);
            View.RefreshText(overlay);
        }
    }
}
