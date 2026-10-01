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
        private void LessonSurface(string title, string introduction)
        {
            DismissStoryPage();
            overlay = View.Rect(root, "Observation exercise", 0, 0, 1, 1);
            View.Fill(overlay, "Exercise shade", Palette.Ink, 0, 0, 1, 1, true);
            View.Label(overlay, "Exercise title", title, 43, Palette.Text, TextAnchor.MiddleCenter,
                0.06f, 0.85f, 0.94f, 0.948f);
            View.Label(overlay, "Exercise invitation", introduction, 28, Palette.Muted, TextAnchor.MiddleCenter,
                0.07f, 0.714f, 0.93f, 0.84f);
            View.Button(overlay, "Close exercise", "回到时间视野", () =>
            { Destroy(overlay.gameObject); overlay = null; ShowJourney(); },
                0.15f, 0.047f, 0.85f, 0.12f, Palette.Mint, Palette.Ink, 30);
        }

        private void ShowChapterExercise(int chapter)
        {
            if (chapter < 1 || chapter > archive.journey.Chapter) return;
            if (chapter == 7) { ShowThirtyDays(); return; }
            if (chapter == 6)
            {
                var beginning = new GameSession(3, 15, 4);
                ForecastRange exercise = ForecastSimulator.Sample(beginning, null, 4);
                exercise.assumption = "独立练习：从同一个起点比较两种选择；不读取或改变正在发生的人生。";
                ShowForecastRange(exercise, false, true);
                return;
            }
            LessonSurface(JourneyProgress.Name(chapter), chapter == 5 ?
                "有些回声由你种下，有些事情由世界带来。" : "选一个行动，亲眼看看它怎样回来。\n这是独立的练习，不消耗今天的机会。");
            if (chapter == 5)
            {
                for (int i = 0; i < WorldEvents.All.Length; i++)
                {
                    WorldEventSpec e = WorldEvents.All[i]; float y = 0.557f - i * 0.122f;
                    View.Panel(overlay, "Probability example", Palette.Panel, 0.07f, y, 0.93f, y + 0.105f, 24);
                    View.Label(overlay, "Event chance", "D" + e.Day + " · " + e.Chance + "%\n" + e.Name,
                        29, Palette.Gold, TextAnchor.MiddleCenter, 0.10f, y + 0.008f, 0.90f, y + 0.096f);
                }
                View.Label(overlay, "Probability meaning", "概率描述天气和邀约。\n种下的回声按约定日期回来；观察不会改变事件。",
                    27, Palette.Mint, TextAnchor.MiddleCenter, 0.08f, 0.17f, 0.92f, 0.30f);
            }
            else
            {
                GameSession practice = new GameSession(chapter == 4 ? 3 : 1, 15, 4);
                if (chapter == 2)
                    while (practice.Day < 6)
                    { if (practice.CanPredict) practice.SkipPrediction(); practice.Choose(practice.Hand[2].Id);
                        if (practice.Day == 4) practice.VisitStation(); practice.Advance(); }
                for (int i = 0; i < practice.Hand.Length; i++)
                {
                    CardSpec card = practice.Hand[i]; float y = 0.559f - i * 0.129f;
                    View.Button(overlay, "Exercise choice " + card.Id, card.Name + "\n" +
                        PlayExperience.NowLabel(card.Now) + (card.Delay > 0 ? " · D" + (practice.Day + card.Delay) + " 回来" : " · 此刻恢复"),
                        () => ResolveObservationChapter(chapter, practice, card.Id),
                        0.08f, y, 0.92f, y + 0.108f, Palette.Panel,
                        card.Kind == CardKind.Growth ? Palette.Mint : card.Kind == CardKind.Recovery ? Palette.Gold : Palette.Coral, 28);
                }
                View.Label(overlay, "Exercise promise", "观察只增加理解，不会改写这一局或领取星尘。", 25,
                    Palette.Muted, TextAnchor.MiddleCenter, 0.07f, 0.174f, 0.93f, 0.257f);
            }
            View.RefreshText(overlay);
        }

        private void ResolveObservationChapter(int chapter, GameSession practice, string cardId)
        {
            int beginning = practice.Day;
            ActionRecord action = practice.Choose(cardId);
            int target = chapter == 1 ? beginning : chapter == 2 ? beginning + 1 : chapter == 3 ? beginning + 3 : 6;
            while (practice.Day < target)
            {
                if (practice.Day == 4 && !practice.StationVisited) practice.VisitStation();
                practice.Advance();
                if (practice.HasPredictionReview) practice.MarkPredictionReviewed();
                if (practice.CanPredict) practice.SkipPrediction();
                if (practice.Day < target) practice.Choose(practice.Hand[2].Id);
            }
            LessonSurface(JourneyProgress.Name(chapter), "D" + beginning + " 的「" + action.cardName + "」\n我们一起走到了 D" + target + "。");
            RectTransform panel = View.Panel(overlay, "Exercise result", Palette.Panel, 0.05f, 0.17f, 0.95f, 0.68f, 30).rectTransform;
            string result = "当时实际变化\n" + PlayExperience.NowLabel(action.actualNow);
            if (action.echoed) result += "\n\nD" + action.echoDay + " · " + action.echoName + "\n" + PlayExperience.NowLabel(action.actualLater);
            else if (action.echoDay > target) result += "\n\n它还在路上，会在 D" + action.echoDay + " 回来。";
            if (chapter == 4)
            {
                CausalNode choice = practice.CausalNodes.FindLast(n => n.type == CausalNodeKind.Choice && n.resolved);
                result += choice == null ? "\n\n这条路没有触发二阶影响。换一张，比较另一种走法。" :
                    "\n\n它改变了后来的选择\n" + NodeList(CausalGraph.Ancestors(practice.CausalNodes, choice.id)) +
                    "\n\nD" + choice.day + " 的选择空间因此发生了变化。";
            }
            result += "\n\n现在的精力 " + practice.Energy + " · 心情 " + practice.Mood + " · 能力 " + practice.Ability;
            ResultText(panel, result);
            View.Button(overlay, "Repeat exercise", "试试另一张", () => ShowChapterExercise(chapter),
                0.16f, 0.188f, 0.84f, 0.258f, Palette.Deep, Palette.Gold, 28);
            View.RefreshText(overlay);
        }

        private void ShowCrossLifeEchoes()
        {
            if (archive.runs.Count == 0) { ShowMap(false); return; }
            Clear(); ArchiveSurface("Echoes across lives");
            View.Label(overlay, "Echo archive title", "每一生，都留下了回声", 43, Palette.Text,
                TextAnchor.MiddleCenter, 0.06f, 0.86f, 0.94f, 0.955f);
            View.Label(overlay, "Echo archive guide", "上下滑动，点一个回声，看看它从哪里来。", 26, Palette.Muted,
                TextAnchor.MiddleCenter, 0.06f, 0.783f, 0.94f, 0.846f);
            var rows = new List<Tuple<RunRecord, CausalNode>>();
            foreach (RunRecord run in archive.runs.OrderByDescending(r => r.number))
                foreach (CausalNode node in GameSession.GraphForRun(run).Where(n => n.resolved &&
                    (n.type == CausalNodeKind.Echo || n.type == CausalNodeKind.Choice || n.type == CausalNodeKind.Mystery)).OrderByDescending(n => n.day))
                    rows.Add(Tuple.Create(run, node));
            RectTransform viewport = View.Rect(overlay, "Echo archive window", 0.045f, 0.16f, 0.955f, 0.762f);
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color = Color.clear;
            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            RectTransform content = View.Rect(viewport, "All life echoes", 0, 1, 1, 1); content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = new Vector2(0, Mathf.Max(1, rows.Count) * 150); scroll.content = content;
            for (int i = 0; i < rows.Count; i++)
            {
                RunRecord run = rows[i].Item1; CausalNode node = rows[i].Item2;
                float top = 1 - i / (float)rows.Count, bottom = 1 - (i + 1) / (float)rows.Count;
                View.Button(content, "Archived echo " + run.number + " " + node.id,
                    "RUN " + run.number.ToString("000") + " · D" + node.day + "\n" + node.label, () =>
                        ShowArchiveDetail(node.label, "它的来路\n" + NodeList(CausalGraph.Ancestors(GameSession.GraphForRun(run), node.id)) +
                            "\n\n当时实际变化\n" + (node.effectRecorded ? PlayExperience.NowLabel(node.effect) : "旧档案没有逐项记录") +
                            "\n\n它后来去了哪里\n" + NodeList(CausalGraph.Descendants(GameSession.GraphForRun(run), node.id))),
                    0.02f, bottom + 0.02f / rows.Count, 0.98f, top - 0.02f / rows.Count, Palette.Panel, Palette.Mint, 28);
            }
            if (rows.Count == 0) View.Label(viewport, "No archived echoes", "先走过一段日子，回声会留在这里。", 30,
                Palette.Muted, TextAnchor.MiddleCenter, 0.06f, 0.36f, 0.94f, 0.62f);
            View.Button(overlay, "Close echo archive", "回到地平线", ShowHome,
                0.16f, 0.05f, 0.84f, 0.12f, Palette.Mint, Palette.Ink, 30);
            View.RefreshText(overlay);
        }

        private void ShowThirtyDaySelf(ForecastRange range, bool other)
        {
            if (range.targetDay != 30 || archive.journey.Chapter < 7) return;
            if (overlay != null) Destroy(overlay.gameObject); overlay = null;
            Clear(true);
            List<ActionRecord> path = other ? range.otherExample : range.example;
            world.ShowOutlook(path);
            View.Label(root, "Outlook self title", "三十天后的一个我", 45, Palette.Text, TextAnchor.MiddleCenter,
                0.05f, 0.884f, 0.95f, 0.958f);
            View.Label(root, "Outlook condition", "可能 " + (other ? "B" : "A") + " · 取决于接下来怎样选择", 28, Palette.Muted,
                TextAnchor.MiddleCenter, 0.06f, 0.81f, 0.94f, 0.871f);
            RectTransform panel = View.Panel(root, "Thirty day reflection", Palette.Panel, 0.05f, 0.06f, 0.95f, 0.445f, 30).rectTransform;
            View.Label(panel, "Self reflection", "「这些日子，还没有发生。」", 35, Palette.Text,
                TextAnchor.MiddleCenter, 0.06f, 0.79f, 0.94f, 0.95f);
            string milestones = string.Join("\n", path.Where(a => a.day == 14 || a.day == 21 || a.day == 28 || a.day == 30)
                .Select(a => "D" + a.day + " · " + a.cardName));
            View.Label(panel, "Possible memories", milestones + "\n\n" + range.assumption, 27, Palette.Mint,
                TextAnchor.MiddleLeft, 0.07f, 0.29f, 0.93f, 0.78f);
            View.Button(panel, "Other thirty day self", "另一种走法", () => ShowThirtyDaySelf(range, !other),
                0.06f, 0.07f, 0.49f, 0.25f, Palette.Deep, Palette.Gold, 28);
            View.Button(panel, "Back to thirty day range", "回到观察", () => ShowForecastRange(range, true),
                0.53f, 0.07f, 0.94f, 0.25f, Palette.Mint, Palette.Ink, 28);
            View.RefreshText(root);
        }
    }
}
