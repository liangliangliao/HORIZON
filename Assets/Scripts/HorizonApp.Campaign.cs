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
        private int predictionHorizon = 3;
        private RectTransform dragJourney;
        private Text journeyCursor;

        private void ShowExpandedPrediction(bool reset)
        {
            if (reset) { Array.Clear(forecastOffsets, 0, forecastOffsets.Length); predictionHorizon = 3; }
            Clear();
            int generation = viewGeneration;
            View.Fill(root, "Prediction hush", new Color(0.01f, 0.03f, 0.05f, 0.87f), 0, 0, 1, 1);
            View.Label(root, "Prediction day", "DAY " + session.Day.ToString("00") + " / " + session.Deadline, 29, Palette.Muted,
                TextAnchor.MiddleCenter, 0.07f, 0.92f, 0.93f, 0.97f);
            View.Label(root, "Prediction title", "画下未来的自己", 43, Palette.Text,
                TextAnchor.MiddleCenter, 0.07f, 0.848f, 0.93f, 0.915f);
            View.Label(root, "Prediction note", "把状态向上或向下推。封存后，未来会来对照。", 25, Palette.Muted,
                TextAnchor.MiddleCenter, 0.07f, 0.798f, 0.93f, 0.848f);
            int[] spans = { 1, 3, 7 };
            for (int i = 0; i < spans.Length; i++)
            {
                int span = spans[i]; float x = 0.08f + i * 0.285f;
                View.Button(root, "Prediction horizon " + span, span + "天后 · D" + (session.Day + span), () => {
                    if (generation != viewGeneration || !session.CanPredict) return;
                    predictionHorizon = span; ShowExpandedPrediction(false);
                }, x, 0.729f, x + 0.27f, 0.782f, predictionHorizon == span ? Palette.Mint : Palette.Panel,
                    predictionHorizon == span ? Palette.Ink : Palette.Text, 25).interactable = session.Day + span <= session.Deadline;
            }
            string[] names = { "精力", "心情", "洞察", "关系", "金钱", "能力" };
            for (int i = 0; i < 6; i++)
            {
                int index = i; float x = 0.145f + i % 3 * 0.29f; float y = i < 3 ? 0.437f : 0.177f;
                View.Label(root, "Axis name", names[i], 27, Palette.Text, TextAnchor.MiddleCenter,
                    x - 0.025f, y + 0.214f, x + 0.165f, y + 0.26f);
                Image track = View.Fill(root, "Draw future " + i, Palette.Deep, x, y + 0.045f, x + 0.15f, y + 0.215f, true);
                View.Fill(track.transform, "Zero", Palette.Muted, 0.12f, 0.495f, 0.88f, 0.505f);
                View.Label(track.transform, "Up", "+", 24, Palette.Muted, TextAnchor.MiddleCenter, 0.3f, 0.83f, 0.7f, 0.98f);
                View.Label(track.transform, "Down", "-", 24, Palette.Muted, TextAnchor.MiddleCenter, 0.3f, 0.02f, 0.7f, 0.17f);
                float center = 0.1f + (forecastOffsets[i] + 3) / 6f * 0.8f;
                RectTransform marker = View.Panel(track.transform, "Forecast mark", Palette.Mint,
                    0.2f, center - 0.04f, 0.8f, center + 0.04f, 18).rectTransform;
                Text value = View.Label(root, "Forecast value", Direction(forecastOffsets[i]), 25, Palette.Mint,
                    TextAnchor.MiddleCenter, x - 0.045f, y, x + 0.195f, y + 0.045f);
                PredictionAxisDrag axis = track.gameObject.AddComponent<PredictionAxisDrag>();
                axis.Initialize(forecastOffsets[i]);
                axis.Changed = next => {
                    if (generation != viewGeneration) return;
                    forecastOffsets[index] = next; float at = 0.1f + (next + 3) / 6f * 0.8f;
                    marker.anchorMin = new Vector2(0.2f, at - 0.04f); marker.anchorMax = new Vector2(0.8f, at + 0.04f);
                    value.text = Direction(next);
                };
            }
            View.Button(root, "Lock prediction", "封存到第" + (session.Day + predictionHorizon) + "天", () => {
                if (generation != viewGeneration || !session.CanPredict) return;
                session.LockPrediction(new ResourceDelta(forecastOffsets[0], forecastOffsets[1], forecastOffsets[2],
                    forecastOffsets[3], forecastOffsets[4], forecastOffsets[5]), predictionHorizon);
                archive.active = session.Snapshot(); Save(); BuildBoard();
            }, 0.10f, 0.09f, 0.90f, 0.153f, Palette.Mint, Palette.Ink, 29);
            View.Button(root, "Skip prediction", "这次先继续生活", () => {
                if (generation != viewGeneration || !session.CanPredict) return;
                session.SkipPrediction(); archive.active = session.Snapshot(); Save(); BuildBoard();
            }, 0.18f, 0.028f, 0.82f, 0.076f, Palette.Panel, Palette.Muted, 24);
            View.RefreshText(root);
        }

        private void BeginLongLife()
        {
            if (archive.journey.Chapter < 7 || busy) return;
            if (overlay != null) { Destroy(overlay.gameObject); overlay = null; }
            if (archive.active != null) { ContinueRun(); return; }
            requestedLifeLength = 30; StartNewRun();
        }

        private void RenderLongMap(RunRecord run, bool duringRun)
        {
            View.Label(overlay, "Map title", "三 十 天 的 时 间 地 图", 39, Palette.Text,
                TextAnchor.MiddleCenter, 0.04f, 0.915f, 0.96f, 0.975f);
            View.Label(overlay, "Run title", "RUN " + run.number.ToString("000") + " · " + run.title, 27, Palette.Mint,
                TextAnchor.MiddleCenter, 0.06f, 0.841f, 0.94f, 0.913f);
            View.Label(overlay, "Map hint", "上下滑动，点开一天。每个选择都有来路。", 24, Palette.Muted,
                TextAnchor.MiddleCenter, 0.06f, 0.802f, 0.94f, 0.842f);
            RectTransform viewport = View.Rect(overlay, "Long map scroll", 0.05f, 0.236f, 0.95f, 0.798f);
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color = Palette.Deep;
            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport; scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
            RectTransform content = View.Rect(viewport, "Long map content", 0, 1, 1, 1);
            content.pivot = new Vector2(0.5f, 1); content.sizeDelta = new Vector2(0, 30 * 122);
            scroll.content = content;
            var graph = CausalGraph.ObservedGraph(GameSession.GraphForRun(run));
            for (int day = 1; day <= 30; day++)
            {
                ActionRecord action = run.actions.Find(a => a.day == day);
                float top = 1 - (day - 1) / 30f, bottom = top - 1 / 30f;
                Button row = View.Button(content, "Inspect day " + day, "", () => ShowActionDetail(action, graph, run.actions),
                    0.015f, bottom + 0.002f, 0.985f, top - 0.002f, Palette.Panel, Palette.Text);
                row.interactable = action != null;
                View.Label(row.transform, "Day", "D" + day, 29, Palette.Muted, TextAnchor.MiddleCenter, 0.02f, 0.05f, 0.15f, 0.95f);
                View.Label(row.transform, "Action", action?.cardName ?? "尚未到来", 29, Palette.Text,
                    TextAnchor.MiddleLeft, 0.18f, 0.41f, 0.97f, 0.95f);
                View.Label(row.transform, "Echo date", action == null ? "" : action.echoDay == 0 ? "当下的恢复" :
                    "→ D" + action.echoDay + (action.echoed ? " · 已回来" : action.echoDay > 30 ? " · 截止日之后" : " · 等待回声"),
                    24, action?.echoed == true ? Palette.Mint : Palette.Gold, TextAnchor.MiddleLeft, 0.18f, 0.05f, 0.97f, 0.45f);
            }
            View.Button(overlay, "Causal network", "展开完整因果网络", () => ShowCausalNetwork(run, duringRun),
                0.09f, 0.17f, 0.91f, 0.223f, Palette.Panel, Palette.Mint, 27);
            if (!duringRun) View.Button(overlay, "Share life", "十秒时间动画", () => ShowShareStory(run, () => RenderMap(false)),
                0.09f, 0.105f, 0.91f, 0.157f, Palette.Panel, Palette.Gold, 26);
            View.Button(overlay, "Close map", "返回", () => { Destroy(overlay.gameObject); overlay = null; },
                0.18f, 0.032f, 0.82f, 0.091f, Palette.Mint, Palette.Ink, 29);
            View.RefreshText(overlay);
        }

        private static Vector2 ConstellationPoint(int day, int length)
        { return length == 30 ? new Vector2(0.096f + (day - 1) % 6 * 0.162f, 0.718f - (day - 1) / 6 * 0.073f) :
            new Vector2(0.15f + (day - 1) % 4 * 0.233f, 0.709f - (day - 1) / 4 * 0.135f); }

        private static string LongGateEvidence(RunRecord run, int gate)
        {
            List<int> days = run.actions.Where(a => a.day > 12 && (gate == 0 ? a.echoed && a.later?.ability > 0 :
                gate == 1 ? a.kind == CardKind.Recovery : a.givesSupport)).Select(a => a.day).ToList();
            return "后半程 · " + EvidenceSummary(days);
        }

        private void UpdateDragJourney(CardSpec card, Vector2 pointer, bool visible)
        {
            if (!visible) { if (dragJourney != null) Destroy(dragJourney.gameObject); dragJourney = null; return; }
            if (dragJourney == null)
            {
                dragJourney = View.Rect(root, "Days crossed by the card", 0.025f, 0.44f, 0.27f, 0.798f);
                View.Panel(dragJourney, "Route glass", new Color(0.008f, 0.025f, 0.04f, 0.94f), 0, 0, 1, 1, 25);
                int[] days = new[] { session.Day, session.Day + 1, session.Day + 3, session.Day + 7, session.Day + card.Delay }
                    .Where(d => d <= session.Deadline || d == session.Day + card.Delay).Distinct().OrderBy(d => d).ToArray();
                for (int i = 0; i < days.Length; i++)
                {
                    float y = 0.11f + i / (float)Mathf.Max(1, days.Length - 1) * 0.69f;
                    View.Label(dragJourney, "Crossed date", days[i] == session.Day ? "今天" : "D" + days[i], 27,
                        days[i] == session.Day + card.Delay ? Palette.Gold : Palette.Muted,
                        TextAnchor.MiddleLeft, 0.13f, y, 0.88f, y + 0.1f);
                }
                journeyCursor = View.Label(dragJourney, "Route progress", "", 25, Palette.Mint,
                    TextAnchor.MiddleCenter, 0.06f, 0.83f, 0.94f, 0.97f);
            }
            dragJourney.gameObject.SetActive(true);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, pointer, null, out Vector2 local);
            float height = (local.y - root.rect.yMin) / root.rect.height;
            float progress = Mathf.InverseLerp(0.31f, 0.70f, height);
            journeyCursor.text = card.Delay == 0 ? "回到此刻" : "经过 D" + (session.Day + Mathf.RoundToInt(card.Delay * progress));
        }
    }
}
