using System;
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
        private readonly List<Text> resourceNumbers = new List<Text>();
        private readonly List<int> resourceValues = new List<int>();
        private int focusPage;

        private float FutureX(int day)
        {
            if (session.Deadline != 30) return Mathf.Lerp(0.12f, 0.88f,
                Mathf.Clamp01((day - session.Day) / (float)Mathf.Max(1, session.Deadline - session.Day)));
            List<int> days = ObservedFutureDays();
            if (days.Count < 2 || day <= days[0]) return 0.12f;
            for (int i = 1; i < days.Count; i++)
                if (day <= days[i]) return Mathf.Lerp(0.12f, 0.88f,
                    (i - 1 + Mathf.InverseLerp(days[i - 1], days[i], day)) / (days.Count - 1));
            return 0.88f;
        }

        private List<int> ObservedFutureDays()
        {
            var days = new List<int> { session.Day };
            IEnumerable<int> candidates = session.Pending.Select(e => e.dueDay);
            if (Vision.Probability && session.CatalogVersion >= 3 && (session.RunNumber >= 3 || session.Deadline == 30))
                candidates = candidates.Concat(WorldEvents.ForCatalog(session.CatalogVersion).Select(e => e.Day));
            foreach (int day in candidates.Where(d => d > session.Day && d < session.Deadline).Distinct().OrderBy(d => d).Take(2))
                days.Add(day);
            for (int day = session.Day + 2; days.Count < 3 && day < session.Deadline; day += 2)
                if (!days.Contains(day)) days.Add(day);
            if (!days.Contains(session.Deadline)) days.Add(session.Deadline);
            days.Sort();
            return days;
        }

        private void DrawObservedFuture()
        {
            List<int> days = ObservedFutureDays();
            int seen = 0;
            HorizonProgress vision = Vision;
            foreach (int day in days)
            {
                float x = FutureX(day);
                PendingEcho echo = session.Pending.Find(e => e.dueDay == day);
                bool known = echo != null && vision.Sees(echo, seen++, session.Day);
                bool strain = echo != null && ObservationDesign.EchoType(echo) == "火种";
                string mark = day == session.Day ? "今天" : day == session.Deadline ? "截止日" : "D" + day;
                string type = known || strain ? ObservationDesign.EchoType(echo) : day == session.Day ? "" : "?";
                if (echo == null && vision.Probability && day > session.Day && day <= session.Day + vision.Days &&
                    session.CatalogVersion >= 3 && (session.RunNumber >= 3 || session.Deadline == 30))
                {
                    WorldEventSpec potential = WorldEvents.ForCatalog(session.CatalogVersion).FirstOrDefault(e => e.Day == day);
                    if (potential != null) type = "变动 " + potential.Chance + "%";
                }
                Color color = strain ? Palette.Coral : day == session.Day || known ? Palette.Mint : Palette.Muted;
                View.Panel(root, "Future halo", new Color(color.r, color.g, color.b, 0.16f),
                    x - 0.024f, 0.831f, x + 0.024f, 0.857f, 24);
                View.Panel(root, "Future core", day == session.Day || known || strain ? color : Palette.Deep,
                    x - 0.009f, 0.839f, x + 0.009f, 0.85f, 12);
                View.Label(root, "Future type", type, 22, color, TextAnchor.MiddleCenter,
                    x - 0.06f, 0.848f, x + 0.06f, 0.875f);
                View.Label(root, "Day mark", mark, 20, Palette.Muted, TextAnchor.MiddleCenter,
                    x - 0.06f, 0.811f, x + 0.06f, 0.834f);
            }
        }

        private void DrawResourceOrbits()
        {
            resourceNumbers.Clear(); resourceValues.Clear();
            int[] values = session.RunNumber == 1 && session.Deadline == 12 ? new[] { session.Energy, session.Mood, session.Ability } :
                new[] { session.Energy, session.Mood, session.Insight, session.Relation, session.Money, session.Ability };
            string[] names = session.RunNumber == 1 && session.Deadline == 12 ? new[] { "精力", "心情", "能力" } :
                new[] { "精力", "心情", "专注", "关系", "金钱", "能力" };
            View.Label(root, "State heading", "此刻的你", 22, Palette.Muted, TextAnchor.MiddleLeft,
                0.06f, 0.416f, 0.5f, 0.443f);
            View.Button(root, "All resources", "全部状态", ShowGoal, 0.73f, 0.417f, 0.94f, 0.442f,
                Palette.Panel, Palette.Muted, 20);
            Image stateHit = View.Fill(root, "Hold current state", Color.clear, 0.05f, 0.354f, 0.95f, 0.413f, true);
            FutureHold hold = stateHit.gameObject.AddComponent<FutureHold>();
            hold.Activated = () => RevealResourceNumbers(true);
            hold.Released = () => RevealResourceNumbers(false);
            for (int i = 0; i < values.Length; i++)
            {
                float width = 0.88f / values.Length, x = 0.06f + i * width;
                View.Panel(root, "State glass", new Color(0.025f, 0.07f, 0.105f, 0.85f), x, 0.354f, x + width - 0.01f, 0.411f, 16);
                ResourceOrbitGraphic orbit = View.Rect(root, "Resource orbit " + names[i], x + 0.012f, 0.367f,
                    x + 0.064f, 0.398f).gameObject.AddComponent<ResourceOrbitGraphic>();
                orbit.Value = values[i]; orbit.color = values[i] <= 2 ? Palette.Coral : Palette.Mint; orbit.raycastTarget = false;
                View.Label(root, "Resource name", names[i], values.Length == 3 ? 24 : 20,
                    Palette.Text, TextAnchor.MiddleCenter, x + 0.064f, 0.382f, x + width - 0.018f, 0.407f);
                resourceNumbers.Add(View.Label(root, "Resource number", values[i] <= 2 ? values[i].ToString() : "", 21,
                    values[i] <= 2 ? Palette.Coral : Palette.Muted, TextAnchor.MiddleCenter,
                    x + 0.064f, 0.356f, x + width - 0.018f, 0.383f));
                resourceValues.Add(values[i]);
            }
        }

        private void RevealResourceNumbers(bool reveal)
        {
            for (int i = 0; i < resourceNumbers.Count; i++)
                if (resourceNumbers[i] != null) resourceNumbers[i].text = reveal || resourceValues[i] <= 2 ? resourceValues[i].ToString() : "";
        }

        private void ShowPredictionWhy()
        {
            if (overlay != null) return;
            overlay = View.Rect(root, "Prediction explanation", 0, 0, 1, 1);
            View.Fill(overlay, "Why veil", new Color(0.006f, 0.02f, 0.04f, 0.95f), 0, 0, 1, 1, true);
            RectTransform panel = View.Panel(overlay, "Why sheet", Palette.Panel, 0.05f, 0.08f, 0.95f, 0.91f, 35).rectTransform;
            View.Label(panel, "Why title", "这段时间，发生了什么？", 38, Palette.Text, TextAnchor.MiddleLeft,
                0.065f, 0.8f, 0.935f, 0.95f);
            List<CausalNode> causes = ObservationDesign.PredictionCauses(session);
            PredictionRecord p = session.Prediction;
            string explanation = "从 D" + p.sourceDay + " 锁定预测，到 D" + p.dueDay + " 早上。以下是实际发生的变化（包含状态上限）：\n";
            int e = 0, m = 0, i = 0, r = 0, money = 0, ability = 0;
            foreach (CausalNode n in causes)
            { explanation += "\nD" + n.day + " · " + n.label + "\n" + PlayExperience.NowLabel(n.effect) + "\n";
                e += n.effect.energy; m += n.effect.mood; i += n.effect.insight;
                r += n.effect.relation; money += n.effect.money; ability += n.effect.ability; }
            if (e != p.actualEnergy || m != p.actualMood || i != p.actualInsight ||
                p.sixAxes && (r != p.actualRelation || money != p.actualMoney || ability != p.actualAbility))
                explanation += "\n旧档案还有未逐项记录的变化：\n" + PlayExperience.NowLabel(new ResourceDelta(
                    p.actualEnergy - e, p.actualMood - m, p.actualInsight - i, p.sixAxes ? p.actualRelation - r : 0,
                    p.sixAxes ? p.actualMoney - money : 0, p.sixAxes ? p.actualAbility - ability : 0)) + "\n";
            explanation += "\n合计实际变化\n" + PlayExperience.NowLabel(new ResourceDelta(p.actualEnergy, p.actualMood, p.actualInsight, p.sixAxes ? p.actualRelation : 0, p.sixAxes ? p.actualMoney : 0, p.sixAxes ? p.actualAbility : 0)) +
                "\n\n预测是校准理解。它不会改变资源，也没有额外星尘奖励。";
            ResultText(panel, explanation);
            View.Button(panel, "Close prediction why", "回到预测对照", () => { Destroy(overlay.gameObject); overlay = null; },
                0.065f, 0.04f, 0.935f, 0.15f, Palette.Mint, Palette.Ink, 30);
            View.RefreshText(overlay);
        }

        private IEnumerator MovePredictionPoint(RectTransform point, float from, float to, float y, int generation)
        {
            for (float age = 0; age < 0.9f; age += Time.unscaledDeltaTime)
            {
                if (generation != viewGeneration || point == null) yield break;
                float x = Mathf.Lerp(from, to, Mathf.SmoothStep(0, 1, age / 0.9f));
                point.anchorMin = new Vector2(x - 0.012f, y + 0.018f);
                point.anchorMax = new Vector2(x + 0.012f, y + 0.031f);
                yield return null;
            }
            if (point != null) { point.anchorMin = new Vector2(to - 0.012f, y + 0.018f);
                point.anchorMax = new Vector2(to + 0.012f, y + 0.031f); }
        }

        private void DrawFocusEchoes()
        {
            List<PendingEcho> echoes = session.Pending.OrderBy(e => e.dueDay)
                .ThenBy(e => e.cardId == archive.preferredCardId ? 0 :
                    string.IsNullOrEmpty(archive.preferredCardId) && e.kind.ToString() == archive.preferredIntent ? 0 : 1).ToList();
            int pages = Mathf.Max(1, Mathf.CeilToInt(echoes.Count / 3f)); focusPage = Mathf.Clamp(focusPage, 0, pages - 1);
            HorizonProgress vision = Vision;
            for (int row = 0; row < 3 && focusPage * 3 + row < echoes.Count; row++)
            {
                int index = focusPage * 3 + row; PendingEcho echo = echoes[index]; float y = 0.605f - row * 0.10f;
                View.Panel(overlay, "Future event", Palette.Panel, 0.08f, y, 0.92f, y + 0.083f, 22);
                string detail = vision.Clue(echo, index, session.Day);
                View.Label(overlay, "Forecast", "D" + echo.dueDay + " · " + detail, 28,
                    ObservationDesign.EchoType(echo) == "火种" ? Palette.Coral : Palette.Mint,
                    TextAnchor.MiddleLeft, 0.12f, y + 0.006f, 0.88f, y + 0.077f);
            }
            if (echoes.Count == 0) View.Label(overlay, "Empty", "还没有埋下回声。今天可以种下一颗。", 30, Palette.Muted,
                TextAnchor.MiddleCenter, 0.09f, 0.49f, 0.91f, 0.6f);
            if (pages > 1)
            {
                View.Button(overlay, "Previous focus page", "<", () => { focusPage--; RenderFocus(false); },
                    0.08f, 0.342f, 0.27f, 0.398f, Palette.Panel, Palette.Text, 27).interactable = focusPage > 0;
                View.Label(overlay, "Focus page", (focusPage + 1) + " / " + pages, 24, Palette.Muted, TextAnchor.MiddleCenter,
                    0.28f, 0.342f, 0.72f, 0.398f);
                View.Button(overlay, "Next focus page", ">", () => { focusPage++; RenderFocus(false); },
                    0.73f, 0.342f, 0.92f, 0.398f, Palette.Panel, Palette.Text, 27).interactable = focusPage < pages - 1;
            }
            if (session.CatalogVersion >= 3 && (session.RunNumber >= 3 || session.Deadline == 30))
            {
                WorldEventSpec next = Array.Find(WorldEvents.ForCatalog(session.CatalogVersion), spec => spec.Day > session.Day);
                if (next != null) View.Label(overlay, "World chance", vision.Probability ?
                    "D" + next.Day + " · " + next.Chance + "% " + next.Name + "\n这是环境事件的概率，已种回声仍会按时回来。" :
                    "D" + next.Day + " · 环境可能改变当天的选择", 23, Palette.Gold,
                    TextAnchor.MiddleCenter, 0.08f, 0.30f, 0.92f, 0.347f);
            }
            if (vision.Compare)
                View.Button(overlay, "Two futures", "查看两条可能未来", () => RenderFocus(true),
                    0.16f, 0.145f, 0.84f, 0.205f, Palette.Panel, Palette.Mint, 27);
            if (vision.Probability && session.Day < session.Deadline)
                View.Button(overlay, "Forecast range", "看看未来的范围", ShowRangeForecast,
                    0.16f, 0.218f, 0.84f, 0.278f, Palette.Panel, Palette.Gold, 27);
        }

        private void DrawDeadlineConstellation(RunRecord run)
        {
            View.Panel(root, "Constellation glass", new Color(0.012f, 0.035f, 0.055f, 0.89f),
                0.035f, 0.35f, 0.965f, 0.766f, 28);
            var positions = new Dictionary<int, Vector2>();
            for (int day = 1; day <= GameSession.RunLength(run); day++) positions[day] = ConstellationPoint(day, GameSession.RunLength(run));
            foreach (CausalNode node in GameSession.GraphForRun(run))
                foreach (string parent in CausalGraph.Parents(node))
                {
                    CausalNode origin = run.causalNodes.Find(n => n.id == parent);
                    if (origin == null || origin.day == node.day || !positions.ContainsKey(node.day) || !positions.ContainsKey(origin.day) ||
                        node.type == CausalNodeKind.Gate) continue;
                    TimeThreadGraphic line = View.Rect(root, "Constellation cause", 0, 0, 1, 1).gameObject.AddComponent<TimeThreadGraphic>();
                    line.From = positions[origin.day]; line.To = positions[node.day]; line.color = new Color(0.52f, 0.95f, 0.8f, 0.23f);
                    line.raycastTarget = false;
                }
            foreach (ActionRecord action in run.actions)
            {
                Vector2 p = positions[action.day]; Color c = action.kind == CardKind.Growth ? Palette.Mint : action.kind == CardKind.Recovery ? Palette.Gold : Palette.Coral;
                View.Panel(root, "Boss day " + action.day, c, p.x - 0.012f, p.y - 0.007f, p.x + 0.012f, p.y + 0.007f, 14);
                View.Label(root, "Boss action", "D" + action.day + "\n" + action.cardName, GameSession.RunLength(run) == 30 ? 15 : 24, Palette.Text,
                    TextAnchor.MiddleCenter, p.x - 0.101f, p.y - 0.084f, p.x + 0.101f, p.y - 0.01f);
            }
        }

        private IEnumerator IlluminateHistory(List<int> days, int gate, int length = 12)
        {
            world.PowerGate(days, gate);
            foreach (int day in days)
            {
                Vector2 p = ConstellationPoint(day, length);
                TimeThreadGraphic line = View.Rect(root, "History reaches gate", 0, 0, 1, 1).gameObject.AddComponent<TimeThreadGraphic>();
                line.From = p; line.To = new Vector2(0.5f, 0.32f - gate * 0.08f); line.color = new Color(1, 0.76f, 0.49f, 0.38f); line.raycastTarget = false;
                yield return new WaitForSeconds(0.022f);
            }
        }

        private IEnumerator EnterStation()
        {
            Clear(); busy = true; int generation = viewGeneration;
            world.ShowStation(0, false);
            View.Fill(root, "Station threshold shade", new Color(0.005f, 0.02f, 0.035f, 0.5f), 0, 0, 1, 1);
            RectTransform horizon = View.Fill(root, "Rising horizon", Palette.Mint, 0.05f, 0.47f, 0.95f, 0.472f).rectTransform;
            for (float age = 0; age < 0.9f; age += Time.unscaledDeltaTime)
            {
                if (generation != viewGeneration || horizon == null) yield break;
                float y = Mathf.Lerp(0.47f, 1.02f, Mathf.SmoothStep(0, 1, age / 0.9f));
                horizon.anchorMin = new Vector2(0.05f, y); horizon.anchorMax = new Vector2(0.95f, y + 0.003f);
                yield return null;
            }
            if (generation == viewGeneration) ShowInRunStation(0);
        }
    }
}
