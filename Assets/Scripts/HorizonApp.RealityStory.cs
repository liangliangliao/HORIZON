using System;
using System.Linq;
using Horizon.Game;
using Horizon.UI;
using UnityEngine;

namespace Horizon
{
    public sealed partial class HorizonApp
    {
        private void ShowRealityNode(RealityNode node)
        {
            MasterPage("Reality node memory", "一次亲自选择的现实动作", ShowConstellation);
            FutureMemory memory = archive.futureMemories.Find(m => m.id == node.memoryId);
            string imagination = memory == null ? "这次行动没有关联预演，仍然是一颗真实报告的节点。" : memory.text;
            string simulation = memory == null || string.IsNullOrEmpty(memory.simulationNodeId) ? "尚未关联游戏中的行动" : "RUN " + memory.simulationRun + " · D" + memory.simulationDay + "\n游戏中你实际走过这一步";
            string reality = node.kind == CausalNodeKind.Reality ? node.label + "\n" + node.occurredAt : archive.reality.nodes.Find(n => n.memoryId == node.memoryId && n.kind == CausalNodeKind.Reality)?.label ?? "尚未在现实中报告完成";
            string[] labels = { "IMAGINATION\n" + imagination, "SIMULATION\n" + simulation, "REALITY\n" + reality };
            for (int i = 0; i < labels.Length; i++)
            { float y = 0.64f - i * 0.19f;
                View.Label(overlay, "Reality path evidence " + i, labels[i], 29, i == 2 ? Palette.Gold : Palette.Mint,
                    TextAnchor.MiddleLeft, 0.09f, y, 0.91f, y + 0.16f).supportRichText = false; }
            RunRecord source = archive.runs.Find(r => r.number == memory?.simulationRun);
            if (source != null)
                View.Button(overlay, "Revisit reality simulation", "回到这次游戏行动的因果路径", () => {
                    ActionRecord action = source.actions.Find(a => a.nodeId == memory.simulationNodeId);
                    if (action != null) ShowActionDetail(action, CausalGraph.ObservedGraph(GameSession.GraphForRun(source)), source.actions);
                    else ShowCausalNetwork(source, false);
                },
                    0.075f, 0.13f, 0.925f, 0.20f, Palette.Panel, Palette.Gold, 26);
        }
    }
}
