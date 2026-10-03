using System.Collections.Generic;
using Horizon.Game;
using Horizon.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Horizon
{
    public sealed partial class HorizonApp
    {
        private void ShowCausalNetwork(RunRecord run, bool duringRun) { ShowCausalStory(run, duringRun, 0); }

        private void ShowCausalStory(RunRecord run, bool duringRun, int selection)
        {
            ArchiveSurface("Causal network");
            List<CausalStoryPath> stories = CausalStoryPaths.For(run);
            View.Label(overlay, "Network title", "这条未来怎样形成", 43, Palette.Text, TextAnchor.MiddleCenter, 0.06f, 0.865f, 0.94f, 0.955f);
            View.Label(overlay, "Network guide", "沿着一条实际来路，看看行为怎样回来。", 25, Palette.Muted,
                TextAnchor.MiddleCenter, 0.07f, 0.805f, 0.93f, 0.86f);
            if (stories.Count > 0)
            {
                selection = Mathf.Clamp(selection, 0, stories.Count - 1); int current = selection;
                View.Button(overlay, "Previous cause story", "‹", () => ShowCausalStory(run, duringRun, (current + stories.Count - 1) % stories.Count),
                    0.05f, 0.74f, 0.15f, 0.798f, Palette.Panel, Palette.Mint, 35);
                View.Label(overlay, "Selected cause story", (selection + 1) + " / " + stories.Count + " · " + stories[selection].Title, 28, Palette.Mint,
                    TextAnchor.MiddleCenter, 0.165f, 0.74f, 0.835f, 0.798f);
                View.Button(overlay, "Next cause story", "›", () => ShowCausalStory(run, duringRun, (current + 1) % stories.Count),
                    0.85f, 0.74f, 0.95f, 0.798f, Palette.Panel, Palette.Mint, 35);
                RectTransform viewport = View.Rect(overlay, "Story path window", 0.035f, 0.205f, 0.965f, 0.725f);
                viewport.gameObject.AddComponent<RectMask2D>(); viewport.gameObject.AddComponent<Image>().color = Palette.Deep;
                ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.horizontal = false;
                scroll.movementType = ScrollRect.MovementType.Clamped;
                List<CausalNode> nodes = stories[selection].nodes;
                RectTransform content = View.Rect(viewport, "Story path", 0, 1, 1, 1); content.pivot = new Vector2(0.5f, 1);
                float height = nodes.Count * 178 + 20; content.sizeDelta = new Vector2(0, height); scroll.content = content;
                var graph = CausalGraph.ObservedGraph(GameSession.GraphForRun(run));
                for (int i = 0; i < nodes.Count; i++)
                {
                    CausalNode node = nodes[i]; float top = 1 - (i * 178 + 15) / height, bottom = top - 138 / height;
                    if (i > 0) View.Fill(content, "Actual cause edge", Palette.Mint, 0.496f, top, 0.504f, top + 40 / height);
                    string state = !node.resolved ? "尚未发生" : node.effectRecorded ? PlayExperience.NowLabel(node.effect) : "已发生";
                    int other = CausalGraph.Parents(node).Count - (i > 0 ? 1 : 0);
                    View.Button(content, "Inspect node " + node.id, "D" + node.day + " · " + node.label + "\n" + state + (other > 0 ? " · 另有 " + other + " 条来路" : ""), () => {
                        ActionRecord action = run.actions.Find(a => a.nodeId == node.id);
                        if (action != null) ShowActionDetail(action, graph, run.actions);
                        else ShowArchiveDetail(node.label, "实际来路\n" + (node.originHidden ? "来路尚未看清" : NodeList(CausalGraph.Ancestors(graph, node.id))) +
                            (node.effectRecorded ? "\n\n实际变化\n" + PlayExperience.NowLabel(node.effect) : ""));
                    }, 0.035f, bottom, 0.965f, top, Palette.Panel, NodeColor(node), 32);
                }
            }
            else View.Label(overlay, "No cause story", "还没有连起来的因果。\n再行动一次，等待回声回来。", 33, Palette.Muted,
                TextAnchor.MiddleCenter, 0.08f, 0.36f, 0.92f, 0.65f);
            View.Button(overlay, "All causal nodes", "查看全部节点与连接", () => ShowCausalNetworkOverview(run, duringRun),
                0.14f, 0.125f, 0.86f, 0.185f, Palette.Panel, Palette.Text, 27);
            View.Button(overlay, "Close causal network", "回到时间地图", () => RenderMap(duringRun), 0.14f, 0.045f, 0.86f, 0.112f, Palette.Mint, Palette.Ink, 29);
            View.RefreshText(overlay);
        }
    }
}
