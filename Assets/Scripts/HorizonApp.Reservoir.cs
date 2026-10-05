using System;
using System.Linq;
using Horizon.Game;
using Horizon.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Horizon
{
    public sealed partial class HorizonApp
    {
        private void ShowReservoir()
        {
            MasterRunState state = CurrentMaster; if (state == null) return;
            MasterPage("Causal investment reservoirs", "那些还没有立刻回报的行动", ShowOrbit);
            overlay.Find("Master shade").GetComponent<Image>().color=new Color(.009f,.023f,.04f,.1f);
            world.ShowReservoirScene(state.expedition?.pools);
            string[] ids = { "growth", "skill", "connection", "recovery" };
            string[] names = { "成长投资", "技能训练", "关系支持", "休息恢复" };
            string[] conditions = { "能力≥6，心情≥4", "能力≥6，心情≥4", "关系≥6，精力≥3", "精力≥7，能力≥4" };
            for (int i = 0; i < ids.Length; i++)
            {
                InvestmentPool pool = state.expedition?.pools.Find(p => p.id == ids[i]);
                int accumulated = pool == null ? 0 : pool.sources.Count;
                string title = names[i] + " · " + accumulated + "/4\n" + conditions[i] + (pool?.paid > 0 ? " · 已兑现" + pool.paid + "次" : "");
                float y = 0.65f - i * 0.125f;
                View.Label(overlay, "Reservoir condition " + ids[i], title, 29, accumulated >= 4 ? Palette.Gold : Palette.Mint,
                    TextAnchor.MiddleLeft, 0.09f, y, 0.91f, y + 0.105f);
            }
            View.Label(overlay, "Reservoir explanation", "每个储备记录你亲自留下的行为。条件同时成熟时，过去的积累才会兑现，并进入因果图。", 28, Palette.Text,
                TextAnchor.MiddleLeft, 0.075f, 0.15f, 0.925f, 0.26f);
        }
    }
}
