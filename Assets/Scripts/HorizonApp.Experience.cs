using System;
using System.Collections;
using System.Collections.Generic;
using Horizon.Game;
using Horizon.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Horizon
{
    public sealed partial class HorizonApp
    {
        private void ShowSecondLife()
        {
            overlay = View.Rect(root, "A wider horizon", 0, 0, 1, 1);
            View.Fill(overlay, "New life veil", new Color(0.01f, 0.03f, 0.05f, 0.94f), 0, 0, 1, 1, true);
            View.Label(overlay, "New horizon", "HORIZON II", 54, Palette.Mint,
                TextAnchor.MiddleCenter, 0.06f, 0.73f, 0.94f, 0.84f);
            View.Label(overlay, "New sight", "这次，你能先看见一个未来节点的类型。", 34, Palette.Text,
                TextAnchor.MiddleCenter, 0.08f, 0.61f, 0.92f, 0.71f);
            View.Label(overlay, "New actions", "新的行动正在靠近\n\n请教前辈 · 玩一局 · 夜里散步", 33, Palette.Gold,
                TextAnchor.MiddleCenter, 0.08f, 0.38f, 0.92f, 0.57f);
            View.Label(overlay, "Keep understanding", "不用记住全部规则。\n先留意：今天的选择，会在哪一天回来？", 30, Palette.Muted,
                TextAnchor.MiddleCenter, 0.08f, 0.23f, 0.92f, 0.36f);
            View.Button(overlay, "Begin second life", "带着这点远见，继续", () =>
            {
                archive.seenSecondLife = true;
                Save();
                BuildBoard();
            }, 0.14f, 0.105f, 0.86f, 0.18f, Palette.Mint, Palette.Ink, 31);
            View.RefreshText(overlay);
        }

        private void RenderStationBeat(int stage)
        {
            stationStage = Mathf.Clamp(stage, 0, 3);
            Clear(true);
            archive.stationRun = session.RunNumber;
            archive.stationBeat = stationStage;
            archive.active = session.Snapshot();
            Save();
            bool reveal = session.RunNumber >= 3 && stage >= 2;
            bool question = session.RunNumber == 3 && stage == 3;
            world.ShowStation(stage, reveal);
            List<ActionRecord> memories = ExperienceContent.StationMemories(session);
            world.ShowMemories(memories);
            View.Fill(root, "Station atmosphere", new Color(0.005f, 0.02f, 0.035f, 0.18f), 0, 0, 1, 1);
            View.Label(root, "Station title", "F U T U R E   S T A T I O N", 33, Palette.Mint,
                TextAnchor.MiddleCenter, 0.05f, 0.91f, 0.95f, 0.965f);
            View.Label(root, "Station day", "DAY 04  /  在地平线的另一边", 25, Palette.Muted,
                TextAnchor.MiddleCenter, 0.06f, 0.852f, 0.94f, 0.9f);
            View.Panel(root, "Station dialogue plate", new Color(0.013f, 0.034f, 0.055f, 0.9f),
                0.045f, 0.365f, 0.955f, 0.51f, 35);
            string voice = question ? "「你还想继续这样走吗？」" : stage == 0 ? "「你终于来了。」" :
                stage == 1 ? "「你最近留下了很多东西。」" : reveal ? "「现在你终于看见我了。」" :
                session.RunNumber == 2 ? "「你已经知道，一些东西会回来。」" : "「你留下的东西，还会继续生长。」";
            Text dialogue = View.Label(root, "Future voice", voice, 39, Palette.Text,
                TextAnchor.MiddleCenter, 0.075f, 0.405f, 0.925f, 0.495f);
            Text detail = View.Label(root, "Future reflection", stage == 0 ? "沿着光，走到长椅前。" :
                stage == 1 ? "身后的路，来自你自己的选择。" : "你在此刻留下的路，正在成为未来的你。",
                25, Palette.Muted, TextAnchor.MiddleCenter, 0.075f, 0.37f, 0.925f, 0.416f);
            if (stage == 1)
            {
                for (int i = 0; i < memories.Count; i++)
                {
                    int selected = i;
                    ActionRecord action = memories[i];
                    float y = 0.278f - i * 0.066f;
                    View.Button(root, "Touch memory " + i, "D" + action.day + "  " + action.cardName +
                        (action.echoDay > 0 ? "  →  D" + action.echoDay : "  →  此刻恢复"), () =>
                    {
                        world.TouchMemory(selected);
                        detail.text = action.echoDay > 0 ? "「" + action.cardName + "」留下的回声：" + action.echoName :
                            "你给自己留了一次恢复，也给下一次选择留下空间。";
                    }, 0.11f, y, 0.89f, y + 0.055f, Palette.Panel,
                    action.kind == CardKind.Temptation ? Palette.Coral : Palette.Mint, 26);
                }
            }
            else if (question)
            {
                View.Label(root, "Choose concern", "现在，我更想保护这个。", 31, Palette.Muted,
                    TextAnchor.MiddleCenter, 0.1f, 0.298f, 0.9f, 0.352f);
                for (int i = 0; i < 3; i++)
                {
                    CardKind intent = (CardKind)i;
                    float y = 0.217f - i * 0.068f;
                    View.Button(root, "Protect " + intent, "保护 · " + RecentBehavior(intent), () =>
                    {
                        archive.preferredIntent = intent.ToString();
                        FinishStation();
                    }, 0.14f, y, 0.86f, y + 0.058f, intent == CardKind.Growth ? Palette.Mint : Palette.Panel,
                    intent == CardKind.Growth ? Palette.Ink : Palette.Text, 28);
                }
            }
            else if (stage == 2)
            {
                View.Panel(root, "Future keepsake", Palette.Panel, 0.075f, 0.18f, 0.925f, 0.322f, 25);
                View.Label(root, "Future keepsake text", reveal ? "那个人，就是未来的你。\nHORIZON III · 两条可能未来" :
                    memories.Count == 0 ? "路还没有写完。明天，你仍然可以选择。" :
                    "D" + memories[0].day + "「" + memories[0].cardName + "」\n" +
                    (memories[0].echoDay > 0 ? "在 D" + memories[0].echoDay + " 留下「" + memories[0].echoName + "」" : "留下了照顾自己的片刻"),
                    31, Palette.Mint, TextAnchor.MiddleCenter, 0.105f, 0.197f, 0.895f, 0.307f);
                if (memories.Count > 0) world.TouchMemory(0);
            }
            if (!question)
            {
                View.Button(root, "Next station beat", stage == 2 && session.RunNumber != 3 ? "回到第 5 天" :
                    stage == 0 ? "走向长椅" : "继续靠近", () => { if (stationStage == stage) NextStationStage(); },
                    0.17f, 0.055f, 0.83f, 0.119f, Palette.Mint, Palette.Ink, 30);
                View.Label(root, "Walk hint", "可以静静看完，也可以点击继续或向前滑动。", 22,
                    Palette.Muted, TextAnchor.MiddleCenter, 0.07f, 0.015f, 0.93f, 0.048f);
                Image touch = View.Fill(root, "Walk forward", new Color(0, 0, 0, 0), 0.03f, 0.52f, 0.97f, 0.845f, true);
                var swipe = touch.gameObject.AddComponent<StationSwipe>();
                swipe.ReadyAt = Time.unscaledTime + 1.1f;
                swipe.Advanced = () => { if (stationStage == stage) NextStationStage(); };
                StartCoroutine(StationPlayback(viewGeneration, stage, dialogue, detail));
            }
            View.RefreshText(root);
        }

        private IEnumerator StationPlayback(int generation, int stage, Text voice, Text detail)
        {
            // 13 + 19 + 14 = 46 seconds by default. Input can always advance it.
            float duration = stage == 0 ? 13 : stage == 1 ? 19 : 14;
            float age = 0;
            string first = voice.text;
            string second = stage == 0 ? "「走近一点。这里没有标准答案。」" :
                stage == 1 ? "「有些是种子，有些是负担。它们都来自你。」" :
                session.RunNumber >= 3 ? "「你不必成为别人。继续创造你想看见的自己。」" :
                "「下一次选择，仍然在你手里。」";
            while (age < duration && generation == viewGeneration && voice != null)
            {
                // A frame after returning from the background must not skip a whole beat.
                age += Mathf.Min(Time.unscaledDeltaTime, 0.25f);
                if (age < 1.6f) voice.text = first.Substring(0, Mathf.Clamp(Mathf.CeilToInt(age / 1.6f * first.Length), 0, first.Length));
                else voice.text = age > duration * 0.55f ? second : first;
                if (stage == 1 && age > 4 && age < 4 + Time.unscaledDeltaTime) world.TouchMemory(0);
                yield return null;
            }
            if (generation == viewGeneration) NextStationStage();
        }

        private bool TryRareMoment()
        {
            if (session == null || session.HasChosen) return false;
            if (archive.pendingMoment == null && session.RunNumber >= archive.nextRareRun && session.Day == 6)
            {
                archive.pendingMoment = ExperienceContent.Moment(session.RunNumber, session.Day, archive.runs);
                archive.nextRareRun = session.RunNumber + ExperienceContent.RareGap(session.RunNumber);
                archive.moments.Add(archive.pendingMoment);
                Save();
            }
            if (archive.pendingMoment == null) return false;
            RareMoment moment = archive.pendingMoment;
            Clear(true);
            world.ShowRare(moment.type);
            View.Fill(root, "Rare shade", new Color(0.01f, 0.025f, 0.045f, 0.45f), 0, 0, 1, 1, true);
            View.Label(root, "Rare title", moment.title, 54, Palette.Gold,
                TextAnchor.MiddleCenter, 0.06f, 0.76f, 0.94f, 0.87f);
            View.Panel(root, "Rare explanation plate", Palette.Panel, 0.06f, 0.135f, 0.94f, 0.37f, 35);
            View.Label(root, "Rare explanation", moment.description, 31, Palette.Text,
                TextAnchor.MiddleCenter, 0.095f, 0.155f, 0.905f, 0.35f);
            View.Button(root, "Continue rare moment", "记住这一瞬，回到今天", () =>
            {
                archive.pendingMoment = null;
                Save();
                BuildBoard();
            }, 0.12f, 0.045f, 0.88f, 0.115f, Palette.Mint, Palette.Ink, 30);
            View.RefreshText(root);
            return true;
        }

        private void ShowJourney()
        {
            if (overlay != null) return;
            overlay = View.Rect(root, "Journey chapters", 0, 0, 1, 1);
            View.Fill(overlay, "Journey background", Palette.Ink, 0, 0, 1, 1, true);
            View.Label(overlay, "Journey title", "让时间视野慢慢展开", 43, Palette.Text,
                TextAnchor.MiddleCenter, 0.06f, 0.855f, 0.94f, 0.945f);
            View.Label(overlay, "Journey promise", "每个回来过的日子，都留下理解。\n隔多久回来，这些理解都会保留。", 28, Palette.Muted,
                TextAnchor.MiddleCenter, 0.08f, 0.746f, 0.92f, 0.849f);
            for (int i = 1; i <= 7; i++)
            {
                float y = 0.669f - (i - 1) * 0.077f;
                bool reached = i <= archive.journey.Chapter;
                View.Panel(overlay, "Chapter " + i, Palette.Panel, 0.08f, y, 0.92f, y + 0.065f, 18);
                View.Label(overlay, "Chapter name", i + "  /  " + JourneyProgress.Name(i) + (reached ? " · 已留下" : " · 尚未到来"),
                    29, reached ? Palette.Mint : Palette.Muted, TextAnchor.MiddleLeft, 0.12f, y + 0.006f, 0.88f, y + 0.059f);
            }
            Button longView = View.Button(overlay, "Thirty day view", "看看三十天后的自己", ShowThirtyDays,
                0.13f, 0.105f, 0.87f, 0.169f, Palette.Panel, Palette.Gold, 29);
            longView.interactable = archive.journey.Chapter >= 7;
            View.Button(overlay, "Close journey", "回到地平线", () => { Destroy(overlay.gameObject); overlay = null; },
                0.19f, 0.031f, 0.81f, 0.094f, Palette.Mint, Palette.Ink, 29);
            View.RefreshText(overlay);
        }

        private void ShowThirtyDays()
        {
            GameSession beginning = new GameSession(Mathf.Max(4, archive.runs.Count + 1));
            ShowForecastRange(ForecastSimulator.Sample(beginning, null, 30), true);
        }

        private void ShowRangeForecast()
        {
            ShowForecastRange(ForecastSimulator.Sample(session, null, 12), false);
        }

        private void ShowForecastRange(ForecastRange range, bool longView)
        {
            if (overlay != null) Destroy(overlay.gameObject);
            overlay = View.Rect(root, "Possible future range", 0, 0, 1, 1);
            View.Fill(overlay, "Range background", Palette.Ink, 0, 0, 1, 1, true);
            View.Label(overlay, "Range title", longView ? "三十天后的自己" : "未来有一个范围", 47, Palette.Text,
                TextAnchor.MiddleCenter, 0.06f, 0.835f, 0.94f, 0.925f);
            View.Label(overlay, "Range assumption", "DAY " + range.targetDay + " · " + range.samples + " 条不同的后续选择\n" + range.assumption,
                28, Palette.Muted, TextAnchor.MiddleCenter, 0.07f, 0.716f, 0.93f, 0.829f);
            string[] rows = { "精力   " + range.energyMin + " 至 " + range.energyMax,
                "心情   " + range.moodMin + " 至 " + range.moodMax,
                "能力   " + range.abilityMin + " 至 " + range.abilityMax };
            for (int i = 0; i < rows.Length; i++)
            {
                float y = 0.577f - i * 0.115f;
                View.Panel(overlay, "Range state", Palette.Panel, 0.1f, y, 0.9f, y + 0.09f, 23);
                View.Label(overlay, "Range value", rows[i], 37, Palette.Mint,
                    TextAnchor.MiddleCenter, 0.13f, y + 0.01f, 0.87f, y + 0.08f);
            }
            View.Label(overlay, "Range doors", "这些模拟中，达到门的条件\n能力 " + range.abilityPass + "/" + range.samples +
                "   状态 " + range.statePass + "/" + range.samples + "   支援 " + range.supportPass + "/" + range.samples,
                29, Palette.Gold, TextAnchor.MiddleCenter, 0.08f, 0.223f, 0.92f, 0.334f);
            View.Label(overlay, "Range meaning", "它们取决于之后怎样选择。你还可以改变这条路。", 25, Palette.Muted,
                TextAnchor.MiddleCenter, 0.07f, 0.156f, 0.93f, 0.219f);
            View.Button(overlay, "Close future range", "回到此刻", () =>
            {
                Destroy(overlay.gameObject); overlay = null;
                if (longView) ShowJourney(); else RenderFocus(false);
            }, 0.15f, 0.06f, 0.85f, 0.13f, Palette.Mint, Palette.Ink, 30);
            View.RefreshText(overlay);
        }
    }
}
