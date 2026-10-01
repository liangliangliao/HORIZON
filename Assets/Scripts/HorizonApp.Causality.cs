using System.Collections;
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
        private GhostStory ghostStory;

        private void ShowMemoryStory(int index, int beat = 0)
        {
            if (overlay != null || session == null || !session.NeedsStation || stationStage != 1) return;
            List<MemoryChain> chains = CausalPresentation.Station(session);
            if (index < 0 || index >= chains.Count || chains[index].Nodes.Count == 0)
            { archive.stationMemoryOpen = false; Save(); return; }
            archive.stationMemoryOpen = true; archive.stationMemoryIndex = index;
            world.TouchMemory(index);
            RenderMemoryStory(chains[index], Mathf.Clamp(beat, 0, chains[index].Nodes.Count - 1));
        }

        private void RenderMemoryStory(MemoryChain chain, int beat)
        {
            DismissStoryPage();
            archive.stationMemoryBeat = beat; Save();
            overlay = View.Rect(root, "Personal causal memory", 0, 0, 1, 1);
            RectTransform page = overlay;
            View.Fill(page, "Memory quiet veil", new Color(0.005f, 0.02f, 0.035f, 0.35f), 0, 0, 1, 1, true);
            View.Fill(page, "Memory readable sky", Palette.Ink, 0, 0.819f, 1, 1);
            View.Label(page, "Memory chain title", "你亲手留下的一条路", 37, Palette.Text,
                TextAnchor.MiddleCenter, 0.06f, 0.938f, 0.94f, 0.989f);
            View.Label(page, "Memory source", chain.Summary, 25, Palette.Mint,
                TextAnchor.MiddleCenter, 0.07f, 0.896f, 0.93f, 0.938f);
            DrawMemoryRoute(page, chain, beat);
            View.Panel(page, "Memory reading plate", Palette.Panel, 0.045f, 0.005f, 0.955f, 0.514f, 32);
            CausalNode node = chain.Nodes[beat];
            world.RevealMemoryNode(archive.stationMemoryIndex, node);
            View.Label(page, "Memory node title", "D" + node.day + " · " + node.label, 36, Palette.Text,
                TextAnchor.MiddleCenter, 0.075f, 0.405f, 0.925f, 0.496f);
            View.Label(page, "Memory actual consequence", CausalPresentation.NodeMeaning(node), 28,
                node.resolved ? Palette.Mint : Palette.Gold, TextAnchor.MiddleCenter,
                0.075f, 0.292f, 0.925f, 0.404f);
            View.Label(page, "Memory self response", chain.Reflection, 27, Palette.Text,
                TextAnchor.MiddleCenter, 0.085f, 0.179f, 0.915f, 0.289f);
            View.Label(page, "Memory reading progress", (beat + 1) + " / " + chain.Nodes.Count + " · 看清这一刻，再继续", 23,
                Palette.Muted, TextAnchor.MiddleCenter, 0.075f, 0.12f, 0.925f, 0.155f);
            bool last = beat == chain.Nodes.Count - 1;
            Button next = View.Button(page, "Next memory node", last ? "回到长椅，看看别的路" : "看看它后来去了哪里", () => {
                if (overlay != page || !page.gameObject.activeSelf) return;
                if (last) CloseMemoryStory(); else RenderMemoryStory(chain, beat + 1);
            }, 0.12f, 0.055f, 0.88f, 0.117f, Palette.Mint, Palette.Ink, 29);
            ArmStoryButton(next, page);
            View.Button(page, "Close memory story", "留到这里，回到长椅", () => { if (overlay == page) CloseMemoryStory(); },
                0.18f, 0.014f, 0.82f, 0.051f, Palette.Deep, Palette.Muted, 22);
            View.RefreshText(page);
        }

        private void CloseMemoryStory()
        {
            archive.stationMemoryOpen = false; Save();
            DismissStoryPage();
        }

        private void OpenGhostStory(RunRecord run)
        {
            if (overlay != null) return;
            ghostStory = CausalPresentation.Ghost(run);
            if (ghostStory == null || ghostStory.Beats.Count == 0)
            { ShowArchiveDetail("如果这里不同，会发生什么？", run.boss.ghost); return; }
            int beat = archive.ghostRun == run.number && archive.ghostOpen ? archive.ghostBeat : 0;
            Clear(true);
            archive.ghostRun = run.number; archive.ghostOpen = true;
            world.BeginGhostStory(ghostStory);
            RenderGhostStory(Mathf.Clamp(beat, 0, ghostStory.Beats.Count - 1));
        }

        private void RenderGhostStory(int index)
        {
            DismissStoryPage();
            int from = index == 0 ? ghostStory.Beats[0].Day : ghostStory.Beats[index - 1].Day;
            archive.ghostBeat = index; Save();
            GhostBeat beat = ghostStory.Beats[index];
            overlay = View.Rect(root, "Possible branch", 0, 0, 1, 1);
            RectTransform page = overlay;
            View.Fill(page, "Ghost quiet veil", new Color(0.008f, 0.02f, 0.045f, 0.38f), 0, 0, 1, 1, true);
            View.Fill(page, "Ghost readable sky", Palette.Ink, 0, 0.828f, 1, 1);
            View.Label(page, "Ghost title", "如果这里不同，会发生什么？", 37, Palette.Text,
                TextAnchor.MiddleCenter, 0.055f, 0.938f, 0.945f, 0.992f);
            View.Label(page, "Ghost hypothesis", "GHOST TIMELINE · 保留当时的天气，走一条可能的路", 23,
                Palette.Mint, TextAnchor.MiddleCenter, 0.055f, 0.899f, 0.945f, 0.938f);
            DrawStoryRoute(page, ghostStory.Beats.Select(b => b.Day).ToList(), index, "Ghost");
            View.Panel(page, "Ghost reading plate", Palette.Panel, 0.045f, 0.005f, 0.955f, 0.481f, 32);
            Text day = View.Label(page, "Ghost day cursor", "DAY " + from.ToString("00") + " → " + beat.Day.ToString("00"),
                24, Palette.Gold, TextAnchor.MiddleCenter, 0.07f, 0.453f, 0.93f, 0.482f);
            View.Label(page, "Ghost beat title", beat.Title, 34, Palette.Text,
                TextAnchor.MiddleCenter, 0.07f, 0.404f, 0.93f, 0.454f);
            StoryScrollText(page, beat.Meaning, "Ghost actual change", 0.239f, 0.399f);
            View.Label(page, "Ghost state comparison", "这一天结束时 · 原来的路 → 这条路\n" + StateComparison(beat.Before, beat.After),
                25, Palette.Mint, TextAnchor.MiddleCenter, 0.075f, 0.154f, 0.925f, 0.239f);
            View.Label(page, "Ghost reading progress", (index + 1) + " / " + ghostStory.Beats.Count + " · 这些变化会停在这里，等你看清", 22,
                Palette.Muted, TextAnchor.MiddleCenter, 0.07f, 0.124f, 0.93f, 0.154f);
            Button next = View.Button(page, beat.Deadline ? "Restart from ghost" : "Next ghost beat",
                beat.Deadline ? "再试一条时间线" : "沿着这条路，继续往后看", () => {
                    if (overlay != page || !page.gameObject.activeSelf) return;
                    if (beat.Deadline) { archive.ghostOpen = false; StartCoroutine(RestartSequence()); }
                    else RenderGhostStory(index + 1);
                }, 0.12f, 0.056f, 0.88f, 0.12f, Palette.Mint, Palette.Ink, 29);
            next.interactable = false;
            StartCoroutine(GhostDayPlayback(from, beat.Day, page, day, next));
            world.GhostTravels(from, beat.Day);
            if (beat.Deadline)
            { world.OpenGate(0, ghostStory.Alternative.boss.ability); world.OpenGate(1, ghostStory.Alternative.boss.state);
                world.OpenGate(2, ghostStory.Alternative.boss.support); Haptic(); }
            View.Button(page, "Close ghost", "回到这次人生", () => {
                if (overlay != page) return;
                archive.ghostOpen = false; Save(); DismissStoryPage();
                world.EndCausalPresentation();
                RunRecord run = ghostStory.Original;
                ShowDeadlineResult(run);
            }, 0.18f, 0.014f, 0.82f, 0.051f, Palette.Deep, Palette.Muted, 23);
            View.RefreshText(page);
        }

        private IEnumerator GhostDayPlayback(int from, int to, RectTransform page, Text cursor, Button next)
        {
            float duration = archive.preferences.reducedMotion ? 0.24f : Mathf.Min(1.2f, 0.35f + Mathf.Abs(to - from) * 0.09f);
            for (float age = 0; age < duration; age += Mathf.Min(Time.unscaledDeltaTime, 0.1f))
            {
                if (overlay != page || page == null) yield break;
                if (userPaused) { yield return null; continue; }
                cursor.text = "DAY " + Mathf.RoundToInt(Mathf.Lerp(from, to, age / duration)).ToString("00") + " · 另一条路正在展开";
                yield return null;
            }
            if (overlay != page || page == null) yield break;
            cursor.text = "DAY " + to.ToString("00") + " · 这一天留下了不同的东西";
            next.interactable = true;
        }

        private void ArmStoryButton(Button button, RectTransform page)
        { button.interactable = false; StartCoroutine(ArmStoryInput(button, page)); }
        private IEnumerator ArmStoryInput(Button button, RectTransform page)
        { yield return new WaitForSecondsRealtime(0.24f); if (overlay == page && button != null) button.interactable = true; }

        private void DrawStoryRoute(Transform parent, List<int> days, int selected, string name)
        {
            for (int i = 0; i < days.Count; i++)
            {
                float x = Mathf.Lerp(0.09f, 0.91f, i / (float)Mathf.Max(1, days.Count - 1));
                if (i > 0) View.Fill(parent, name + " route", i <= selected ? Palette.Mint : Palette.Deep,
                    Mathf.Lerp(0.09f, 0.91f, (i - 1) / (float)Mathf.Max(1, days.Count - 1)), 0.864f, x, 0.866f);
                View.Panel(parent, name + " node " + i, i <= selected ? Palette.Mint : Palette.Gold,
                    x - 0.008f, 0.858f, x + 0.008f, 0.873f, 12);
                if (i == selected || i == 0 || i == days.Count - 1)
                    View.Label(parent, name + " node date", "D" + days[i], 23, i == selected ? Palette.Text : Palette.Muted,
                        TextAnchor.MiddleCenter, x - 0.05f, 0.83f, x + 0.05f, 0.857f);
            }
        }

        private void DrawMemoryRoute(Transform parent, MemoryChain chain, int selected)
        {
            var points = new Dictionary<string, Vector2>();
            for (int i = 0; i < chain.Nodes.Count; i++)
                points[chain.Nodes[i].id] = new Vector2(Mathf.Lerp(0.09f, 0.91f,
                    i / (float)Mathf.Max(1, chain.Nodes.Count - 1)), 0.863f + i % 2 * 0.022f);
            foreach (CausalNode node in chain.Nodes)
                foreach (string source in CausalGraph.ObservedParents(node))
                    if (points.ContainsKey(source))
                    {
                        TimeThreadGraphic line = View.Rect(parent, "Actual memory edge " + source + " " + node.id, 0, 0, 1, 1)
                            .gameObject.AddComponent<TimeThreadGraphic>();
                        line.From = points[source]; line.To = points[node.id];
                        line.color = new Color(0.5f, 0.95f, 0.8f, 0.5f); line.raycastTarget = false;
                    }
            for (int i = 0; i < chain.Nodes.Count; i++)
            {
                CausalNode node = chain.Nodes[i]; Vector2 at = points[node.id];
                View.Panel(parent, "Memory node " + node.id, i == selected ? Palette.Text : node.resolved ? Palette.Mint : Palette.Gold,
                    at.x - 0.008f, at.y - 0.006f, at.x + 0.008f, at.y + 0.006f, 12);
                if (i == selected) View.Label(parent, "Selected memory date", "D" + node.day, 23, Palette.Text,
                    TextAnchor.MiddleCenter, at.x - 0.05f, 0.823f, at.x + 0.05f, 0.853f);
            }
        }

        private void StoryScrollText(Transform parent, string value, string name, float bottom, float top)
        {
            RectTransform viewport = View.Rect(parent, name + " window", 0.075f, bottom, 0.925f, top);
            viewport.gameObject.AddComponent<Image>().color = Color.clear; viewport.gameObject.AddComponent<RectMask2D>();
            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false;
            scroll.viewport = viewport; scroll.movementType = ScrollRect.MovementType.Clamped;
            RectTransform content = View.Rect(viewport, name + " content", 0, 1, 1, 1); content.pivot = new Vector2(0.5f, 1);
            Text text = View.Label(content, name, value, 27, Palette.Text, TextAnchor.UpperLeft, 0, 0, 1, 1);
            Canvas.ForceUpdateCanvases(); content.sizeDelta = new Vector2(0, Mathf.Max(viewport.rect.height, text.preferredHeight + 12));
            scroll.content = content;
        }

        private static string StateComparison(ResourceDelta before, ResourceDelta after)
        { return "精力 " + before.energy + "→" + after.energy + "   心情 " + before.mood + "→" + after.mood + "   能力 " + before.ability + "→" + after.ability +
            "\n关系 " + before.relation + "→" + after.relation + "   金钱 " + before.money + "→" + after.money + "   洞察 " + before.insight + "→" + after.insight; }
    }
}
