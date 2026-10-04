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
        private ArchiveData practiceParent;
        private GameSession practiceParentSession;
        private bool IsPractice { get { return practiceParent != null; } }

        private void StartPractice()
        {
            if (busy || IsPractice) return;
            PersistLiveLife();
            practiceParent = archive; practiceParentSession = session;
            archive = new ArchiveData { nextRareRun = 99,
                me = JsonUtility.FromJson<PlayerBehavioralModel>(JsonUtility.ToJson(practiceParent.me)),
                preferences = JsonUtility.FromJson<PlayerPreferences>(JsonUtility.ToJson(practiceParent.preferences)),
                ai = JsonUtility.FromJson<AISettings>(JsonUtility.ToJson(practiceParent.ai)) };
            archive.Repair(); archive.playGuide.completed = true;
            requestedMasterMode = RunMode.Quick; requestedLifeLength = 12; StartNewRun();
        }
        private void ExitPractice()
        {
            if (!IsPractice) return;
            if (parallelPlaying) { SaveParallelSession(); parallelPlaying = false; }
            StopAllCoroutines(); CancelAIRequest();
            archive = practiceParent; session = practiceParentSession; practiceParent = null; practiceParentSession = null;
            Clear(); ApplyPreferences(); world.SetTheme(archive.wallet.theme);
            if (archive.active != null || archive.pendingFeedback != null) ContinueRun(); else ShowHome();
        }
        private void AddPracticeEntry(Transform parent, bool home)
        {
            if (IsPractice)
                View.Button(parent, "Exit practice", "练习中 · 返回原人生", ExitPractice,
                    0.075f, home ? 0.328f : 0.545f, 0.925f, home ? 0.395f : 0.625f, Palette.Panel, Palette.Gold, 30);
            else if (home)
                View.Button(parent, "Practice life", "自由练习 · 保留当前人生", StartPractice, 0.12f, 0.328f, 0.88f, 0.395f, Palette.Panel, Palette.Text, 31);
        }

        private void BuildReadableHand()
        {
            choiceVisibleSince = Time.unscaledTime;
            boardHint = View.Label(root, "Drag hint", IsPractice ? "练习采用正式规则，退出后原人生保持原样" : "点牌查看 · 向上拖进金色圈行动", 28,
                Palette.Muted, TextAnchor.MiddleCenter, 0.045f, 0.014f, 0.955f, 0.057f);
            trail = View.Fill(root, "Causal light", Palette.Mint, 0.5f, 0.5f, 0.5f, 0.5f).rectTransform;
            trail.gameObject.SetActive(false);
            for (int i = 0; i < session.Hand.Length; i++)
            {
                CardSpec card = session.Hand[i]; bool available = session.CanPlay(card), canImagine = session.CanPrepareImagination(card);
                float x = 0.055f + i * 0.302f, y = i == 1 ? 0.082f : 0.074f;
                Color accent = card.Kind == CardKind.Temptation ? Palette.Coral : card.Kind == CardKind.Recovery ? Palette.Gold : Palette.Mint;
                RectTransform rect = View.Rect(root, card.Id, x, y, x + 0.283f, y + 0.267f);
                var surface = rect.gameObject.AddComponent<HorizonCardSurface>();
                surface.Accent = accent; surface.Available = available || canImagine; surface.raycastTarget = true;
                var shadow = rect.gameObject.AddComponent<Shadow>(); shadow.effectColor = new Color(0, 0, 0, 0.55f); shadow.effectDistance = new Vector2(10, -16);
                rect.localRotation = Quaternion.Euler(5, (i - 1) * 7, (1 - i) * 3);
                View.Label(rect, "Type", PlayGuide.Family(card), 25, accent, TextAnchor.MiddleCenter, 0.06f, 0.88f, 0.94f, 0.98f);
                View.Label(rect, "Name", card.Name, 40, Palette.Text, TextAnchor.MiddleCenter, 0.07f, 0.63f, 0.93f, 0.87f);
                var emblem = View.Rect(rect, "Card causal emblem", 0.31f, 0.44f, 0.69f, 0.60f).gameObject.AddComponent<DropRingGraphic>();
                emblem.color = accent; emblem.raycastTarget = false; emblem.Thickness = 3;
                View.Label(rect, "Time marker", card.Delay == 0 ? "NOW" : "+" + card.Delay + "D", 26, accent, TextAnchor.MiddleCenter, 0.20f, 0.43f, 0.80f, 0.60f);
                View.Label(rect, "Now", canImagine ? "先预演失败与恢复" : available ? PlayExperience.NowLabel(session.ImmediateEffect(card)) : "暂不可用 · 点开查看",
                    28, available || canImagine ? Palette.Text : Palette.Coral, TextAnchor.MiddleCenter, 0.06f, 0.23f, 0.94f, 0.41f);
                string date = card.Delay == 0 ? "现在恢复" : "D" + (session.Day + card.Delay) + (session.Day + card.Delay > session.Deadline ? " · 局后" : " 回来");
                View.Label(rect, "Future", date + (card.Delay > 0 ? "\n" + PlayExperience.FutureMeaning(card) : ""), 26, accent, TextAnchor.MiddleCenter, 0.07f, 0.03f, 0.93f, 0.21f);
                HorizonCardDrag drag = rect.gameObject.AddComponent<HorizonCardDrag>();
                drag.Available = available || canImagine; drag.Dragged = CardDragged; drag.Played = CardPlayed; drag.Tapped = CardTapped;
                drag.IsOverTarget = IsInsideDropZone; drag.CanBegin = item => !busy && overlay == null && (activeDrag == null || activeDrag == item);
                drag.Began = item => activeDrag = item;
                drag.Rejected = item => { activeDrag = null; if (boardHint != null) boardHint.text = "卡牌已归位 · 拖进金色圈，圈变亮后松手"; };
                cards.Add(drag, card); StartCoroutine(RiseCard(rect, rect.gameObject.AddComponent<CanvasGroup>(), i * 0.04f));
            }
            AddPracticeEntry(root, false);
        }

        private void DrawPlayableFuture()
        {
            var planned = PlayGuide.Scheduled(session).Take(2).ToList();
            if (planned.Count == 0)
                View.Label(root, "Future empty", "你还没有安排回声\n从下方选择今天的行动", 32, Palette.Muted, TextAnchor.MiddleCenter, 0.075f, 0.797f, 0.925f, 0.867f);
            for (int i = 0; i < planned.Count; i++)
            {
                PendingEcho echo = planned[i]; float x = 0.075f + i * 0.445f;
                View.Label(root, "Known echo date", "D" + echo.dueDay + " ← D" + echo.sourceDay, 34, Palette.Mint, TextAnchor.MiddleLeft,
                    x, 0.828f, x + 0.4f, 0.868f);
                View.Label(root, "Known echo action", echo.cardName, 29, Palette.Text, TextAnchor.MiddleLeft, x, 0.792f, x + 0.4f, 0.83f);
            }
        }

        private void ShowPlayOptions()
        {
            MasterPage("Play help", "今天只需要选一个行动");
            View.Label(overlay, "Playable rules", "今天的结果立即发生。\n牌上的日期，是它下次回来的时候。\n成长需要时间；状态差时可以恢复。\n第 " + session.Deadline + " 天，把准备带到展示日。", 38,
                Palette.Text, TextAnchor.MiddleLeft, 0.075f, 0.45f, 0.925f, 0.84f);
            View.Button(overlay, "Read guide", "看看操作与目标说明", ShowPlayGuide, 0.075f, 0.32f, 0.925f, 0.405f, Palette.Panel, Palette.Text, 32);
            View.Button(overlay, "Practice life", IsPractice ? "返回原人生" : "自由练习 · 原人生会保留", IsPractice ? (Action)ExitPractice : StartPractice,
                0.075f, 0.2f, 0.925f, 0.285f, Palette.Panel, Palette.Gold, 32);
            View.RefreshText(overlay);
        }

        private void ShowImaginationComparison()
        {
            var comparison = archive.imaginationComparisons.LastOrDefault();
            MasterPage("Imagination comparison", "预演与实际 · 从哪里分开", ShowMasterHub);
            if (comparison == null)
            {
                View.Label(overlay, "No comparison", "先点开一张行动牌，完成一次预演。\n选择之后，这里会出现对应的实际路径。", 36, Palette.Text,
                    TextAnchor.MiddleCenter, 0.08f, 0.4f, 0.92f, 0.75f); return;
            }
            View.Label(overlay, "Comparison goal", comparison.goal + " · 第 " + comparison.run + " 条人生", 34, Palette.Gold,
                TextAnchor.MiddleLeft, 0.075f, 0.775f, 0.925f, 0.85f);
            string recovery = ImaginationCalibration.RecoveryName(comparison.expectedRecovery);
            string[] imagined = { "D" + comparison.plannedDay + " · " + CardCatalog.FindById(comparison.expectedCardId)?.Name, "遇到一次困难", recovery };
            string[] actual = { comparison.actionObserved ? "D" + comparison.actualDay + " · " + CardCatalog.FindById(comparison.actualCardId)?.Name : "行动尚未发生",
                comparison.failureDay > 0 ? "D" + comparison.failureDay + " · 确实受挫" : "尚未遇到对应困难",
                comparison.recoveryObserved ? "D" + comparison.recoveryDay + " · " + comparison.actualRecovery : "恢复路径仍待观察" };
            View.Label(overlay, "Imagined timeline heading", "想象中的路径", 28, Palette.Muted,
                TextAnchor.MiddleLeft, 0.105f, 0.71f, 0.48f, 0.775f);
            View.Label(overlay, "Actual timeline heading", "实际游戏行动", 28, Palette.Muted,
                TextAnchor.MiddleLeft, 0.535f, 0.71f, 0.895f, 0.775f);
            for (int i = 0; i < 3; i++)
            {
                float y = 0.57f - i * 0.115f;
                View.Panel(overlay, "Comparison row " + i, Palette.Panel, 0.075f, y, 0.925f, y + 0.102f, 21);
                View.Label(overlay, "Imagined step " + i, imagined[i], 29, Palette.Gold, TextAnchor.MiddleLeft, 0.105f, y, 0.48f, y + 0.102f);
                View.Label(overlay, "Actual step " + i, actual[i], 29, Palette.Mint, TextAnchor.MiddleLeft, 0.535f, y, 0.895f, y + 0.102f);
            }
            Text divergence = View.Label(overlay, "Divergence evidence", comparison.difference, 34, Palette.Text, TextAnchor.UpperLeft, 0.075f, 0.195f, 0.925f, 0.34f);
            divergence.resizeTextForBestFit = false;
            View.Label(overlay, "Calibration scope", comparison.realityObserved ? "现实路径 · " + comparison.realityAction + "\n" + comparison.realityAt : "对照保留为近期经验；现实行动需在现实桥梁亲自确认。", 25, Palette.Muted,
                TextAnchor.MiddleLeft, 0.075f, 0.115f, 0.925f, 0.185f);
            View.RefreshText(overlay);
        }
    }
}
