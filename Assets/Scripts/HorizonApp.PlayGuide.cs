using System;
using System.Linq;
using Horizon.Game;
using Horizon.UI;
using UnityEngine;

namespace Horizon
{
    public sealed partial class HorizonApp
    {
        private int schedulePage;

        private void ShowPlayGuide() { ShowPlayGuidePage(0, false); }

        private void ShowPlayGuidePage(int page, bool onboarding)
        {
            if (onboarding)
            { archive.playGuide.run = session.RunNumber; archive.playGuide.page = page; Save(); }
            MasterPage("First life guide", page == 0 ? "用 " + session.Deadline + " 天，走向一个未来" : "一张牌，有两个时间", () => FinishPlayGuide(onboarding));
            View.Label(overlay, "Guide progress", (page + 1) + " / 2  ·  每天只选择一个行动", 27, Palette.Gold,
                TextAnchor.MiddleLeft, 0.075f, 0.79f, 0.925f, 0.85f);
            if (page == 0)
            {
                View.Label(overlay, "Guide mission", "第 " + session.Deadline + " 天，你会面对截止日。\n今天的准备，决定你能否从容抵达。", 35, Palette.Text,
                    TextAnchor.MiddleLeft, 0.075f, 0.665f, 0.925f, 0.785f);
                string[] titles = { "成长 · 让努力来得及回来", "状态 · 留出恢复的日子", "支持 · 与别人一起走一段" };
                string[] details = { "能力至少 6，成长回声至少 " + ProductExperience.EvidenceNeeded(session) + " 次。", "精力与心情至少 4，恢复行动至少 " + ProductExperience.EvidenceNeeded(session) + " 次。",
                    "支援行动至少 " + ProductExperience.EvidenceNeeded(session) + " 次，关系至少 6、金钱至少 2。" };
                for (int i = 0; i < 3; i++)
                {
                    float y = 0.49f - i * 0.135f;
                    var panel = View.Panel(overlay, "Guide goal " + i, Palette.Panel, 0.075f, y, 0.925f, y + 0.12f, 22).rectTransform;
                    View.Label(panel, "Guide goal title", titles[i], 29, Palette.Mint, TextAnchor.MiddleLeft, 0.04f, 0.48f, 0.96f, 0.96f);
                    View.Label(panel, "Guide goal detail", details[i], 25, Palette.Text, TextAnchor.MiddleLeft, 0.04f, 0.04f, 0.96f, 0.48f);
                }
                View.Button(overlay, "Guide next", "看看一次选择怎样进入未来", () => ShowPlayGuidePage(1, onboarding),
                    0.075f, 0.125f, 0.925f, 0.2f, Palette.Mint, Palette.Ink, 29);
            }
            else
            {
                CardSpec sample = session.Hand.FirstOrDefault(c => c.Kind == CardKind.Growth) ?? session.Hand[0];
                View.Label(overlay, "Guide sample card", "例如「" + sample.Name + "」", 39, Palette.Text,
                    TextAnchor.MiddleLeft, 0.075f, 0.68f, 0.925f, 0.78f);
                View.Label(overlay, "Guide now", "今天 · 付出与得到\n" + PlayExperience.NowLabel(session.ImmediateEffect(sample)), 33, Palette.Mint,
                    TextAnchor.MiddleLeft, 0.075f, 0.54f, 0.925f, 0.675f);
                View.Label(overlay, "Guide future", "第 " + (session.Day + sample.Delay) + " 天 · 行动回来\n" + PlayExperience.FutureMeaning(sample), 33, Palette.Gold,
                    TextAnchor.MiddleLeft, 0.075f, 0.39f, 0.925f, 0.53f);
                View.Label(overlay, "Guide control", "点牌：先看清，再选择。\n拖牌：向上送进金色圈，松手行动。\n做完一天后，过去的回声会自己抵达。", 28, Palette.Text,
                    TextAnchor.MiddleLeft, 0.075f, 0.225f, 0.925f, 0.38f);
                View.Button(overlay, "Guide start", "回到今天 · 亲自选一张", () => FinishPlayGuide(onboarding),
                    0.075f, 0.125f, 0.925f, 0.2f, Palette.Mint, Palette.Ink, 30);
            }
            View.RefreshText(overlay);
        }

        private void FinishPlayGuide(bool onboarding)
        {
            if (onboarding) { archive.playGuide.completed = true; archive.playGuide.run = 0; Save(); }
            CloseMasterPage(); BuildBoard();
        }

        private void BuildDailyGuide()
        {
            var panel = View.Panel(root, "Daily direction", new Color(0.014f, 0.046f, 0.068f, 0.94f),
                0.05f, 0.505f, 0.95f, 0.655f, 25).rectTransform;
            View.Label(panel, "Today's direction", PlayGuide.Today(session), 27, Palette.Text,
                TextAnchor.MiddleLeft, 0.035f, 0.785f, 0.85f, 0.98f);
            View.Button(panel, "How to play", "玩法", ShowPlayGuide, 0.85f, 0.785f, 0.98f, 0.98f, Palette.Panel, Palette.Muted, 22);
            View.Label(panel, "Next causal step", PlayGuide.Next(session), 23, Palette.Muted,
                TextAnchor.MiddleLeft, 0.035f, 0.405f, 0.965f, 0.78f);
            int need = ProductExperience.EvidenceNeeded(session);
            string[] progress = { "成长回声 " + ProductExperience.GrowthEvidence(session) + "/" + need + "\n能力 " + session.Ability + "/6",
                "恢复行动 " + ProductExperience.RecoveryEvidence(session) + "/" + need + "\n精力 " + session.Energy + " · 心情 " + session.Mood,
                "支援行动 " + ProductExperience.SupportEvidence(session) + "/" + need + "\n关系 " + session.Relation + "/6" };
            for (int i = 0; i < 3; i++)
            {
                int gate = i; float x = 0.035f + i * 0.316f;
                bool ready = ProductExperience.GateReady(session, i);
                View.Button(panel, "Daily goal " + gate, (ready ? "已准备 · " : "") + progress[i], ShowGoal,
                    x, 0.065f, x + 0.298f, 0.395f, Palette.Panel, ready ? Palette.Mint : Palette.Gold, 21);
            }
        }

        private void ShowFutureSchedule()
        {
            if (busy) return;
            var echoes = PlayGuide.Scheduled(session);
            int pages = Math.Max(1, (echoes.Count + 3) / 4);
            schedulePage = Mathf.Clamp(schedulePage, 0, pages - 1);
            MasterPage("Future schedule", "今天安排的未来", CloseMasterPage);
            View.Label(overlay, "Schedule explanation", "行动已经发生，回声还在路上。\n抵达日来自你选过的牌；更深的影响随视野逐渐显现。", 28, Palette.Text,
                TextAnchor.MiddleLeft, 0.075f, 0.725f, 0.925f, 0.855f);
            if (echoes.Count == 0)
                View.Label(overlay, "Empty schedule", "还没有在途的行动回声。\n点开一张牌，看看它准备去哪一天。", 34, Palette.Muted,
                    TextAnchor.MiddleCenter, 0.075f, 0.39f, 0.925f, 0.64f);
            for (int i = schedulePage * 4; i < Math.Min(echoes.Count, (schedulePage + 1) * 4); i++)
            {
                PendingEcho echo = echoes[i]; float y = 0.56f - (i % 4) * 0.115f;
                var panel = View.Panel(overlay, "Planned echo " + echo.nodeId, Palette.Panel, 0.075f, y, 0.925f, y + 0.105f, 20).rectTransform;
                View.Label(panel, "Echo route", "D" + echo.sourceDay + "「" + echo.cardName + "」 → D" + echo.dueDay, 29, Palette.Mint,
                    TextAnchor.MiddleLeft, 0.04f, 0.46f, 0.96f, 0.95f);
                View.Label(panel, "Echo visibility", (echo.dueDay - session.Day) + " 天后回来 · " + Vision.Clue(echo, i, session.Day), 24, Palette.Muted,
                    TextAnchor.MiddleLeft, 0.04f, 0.025f, 0.96f, 0.48f);
            }
            if (pages > 1)
            {
                View.Button(overlay, "Previous echoes", "上一页", () => { schedulePage--; ShowFutureSchedule(); }, 0.075f, 0.12f, 0.35f, 0.19f, Palette.Panel, Palette.Text, 25);
                View.Label(overlay, "Schedule page", (schedulePage + 1) + " / " + pages, 26, Palette.Gold, TextAnchor.MiddleCenter, 0.37f, 0.12f, 0.63f, 0.19f);
                View.Button(overlay, "Next echoes", "下一页", () => { schedulePage++; ShowFutureSchedule(); }, 0.65f, 0.12f, 0.925f, 0.19f, Palette.Panel, Palette.Text, 25);
            }
            View.RefreshText(overlay);
        }

        private void BeginBoardImagination()
        {
            if (!CanPrepareMaster) return;
            CardSpec card = session.InExecutionMode ? CardCatalog.FindById(session.Master.decision.cardId) : session.Hand[1];
            PrepareCardImagination(card);
        }

        private void PrepareCardImagination(CardSpec card)
        {
            if (!CanPrepareMaster) return;
            archive.playGuide.imagineFromBoard = true;
            if (archive.imagination == null)
            {
                archive.playGuide.preparedCardId = card.Id;
                archive.playGuide.preparedRun = session.RunNumber; archive.playGuide.preparedDay = session.Day;
                imagineGoal = card.Name; imagineDifficulty = 1;
            }
            Save(); ShowImagineSetup();
        }

        private void ReturnFromImagination()
        {
            if (!archive.playGuide.imagineFromBoard) { ShowMasterHub(); return; }
            string cardId = archive.playGuide.preparedRun == session.RunNumber && archive.playGuide.preparedDay == session.Day ? archive.playGuide.preparedCardId : null;
            if (archive.imagination == null)
            { archive.playGuide.imagineFromBoard = false; archive.playGuide.preparedCardId = null; Save(); }
            CloseMasterPage(); BuildBoard();
            if (overlay != null || busy || session.InExecutionMode) return;
            HorizonCardDrag drag = cards.FirstOrDefault(pair => pair.Value.Id == cardId).Key;
            if (drag != null) CardTapped(drag);
        }
    }
}
