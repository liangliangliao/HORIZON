using System.Linq;
using Horizon.Game;
using Horizon.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Horizon
{
    public sealed partial class HorizonApp
    {
        private bool RenderEchoSummary()
        {
            FeedbackRecord receipt = archive.pendingFeedback;
            if (receipt?.kind != FeedbackKind.Echoes || receipt.detailed || receipt.page > 0 || receipt.beats.Count < 2) return false;
            Clear(); world.ShowBoard(); world.SetTimeline(session.Actions, session.Deadline);
            View.Label(root, "Result day", "DAY " + receipt.day.ToString("00") + " / " + session.Deadline, 37, Palette.Text,
                TextAnchor.MiddleLeft, 0.06f, 0.95f, 0.94f, 0.985f);
            View.Label(root, "Echo bundle title", receipt.beats.Count + " 条回声 · 一起抵达", 48, Palette.Mint,
                TextAnchor.MiddleLeft, 0.075f, 0.78f, 0.925f, 0.88f);
            View.Label(root, "Echo bundle context", "下面每一条，都来自已经发生的行动。", 32, Palette.Text,
                TextAnchor.MiddleLeft, 0.075f, 0.713f, 0.925f, 0.78f);
            RectTransform viewport = View.Rect(root, "Echo summary scroll", 0.075f, 0.275f, 0.925f, 0.705f);
            viewport.gameObject.AddComponent<RectMask2D>();
            Image hit = viewport.gameObject.AddComponent<Image>(); hit.color = Color.clear;
            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            RectTransform content = View.Rect(viewport, "All echo origins", 0, 1, 1, 1); content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = new Vector2(0, Mathf.Max(800, receipt.beats.Count * 184)); scroll.content = content;
            for (int i = 0; i < receipt.beats.Count; i++)
            {
                FeedbackBeat beat = receipt.beats[i];
                RectTransform row = View.Panel(content, "Resolved echo " + i, Palette.Panel, 0, 1, 1, 1, 23).rectTransform;
                row.pivot = new Vector2(0.5f, 1); row.sizeDelta = new Vector2(0, 166); row.anchoredPosition = new Vector2(0, -i * 184);
                View.Label(row, "Echo source", "D" + beat.sourceDay + "「" + beat.source + "」 → 今天", 32, Palette.Gold,
                    TextAnchor.MiddleLeft, 0.035f, 0.51f, 0.965f, 0.96f);
                View.Label(row, "Echo actual changes", PlayExperience.NowLabel(beat.delta), 34, Palette.Text,
                    TextAnchor.MiddleLeft, 0.035f, 0.07f, 0.965f, 0.52f);
            }
            View.Label(root, "Echo scroll hint", "可上下滑动查看全部来源 · 资源已经结算并保存", 27, Palette.Muted,
                TextAnchor.MiddleCenter, 0.075f, 0.226f, 0.925f, 0.27f);
            View.Button(root, "Review echo details", "逐条细看", () => { receipt.detailed = true; Save(); ShowFeedback(); },
                0.075f, 0.148f, 0.36f, 0.213f, Palette.Panel, Palette.Text, 29);
            View.Label(root, "Bundle reward", "+" + receipt.stardust + " 星尘 · 已收集", 28, Palette.Gold,
                TextAnchor.MiddleRight, 0.39f, 0.148f, 0.925f, 0.213f);
            Button next = View.Button(root, "Continue result", session.HasPredictionReview ? "看看预测与实际" : "回到今天 · 选择下一步", () => {
                if (busy) return;
                receipt.page = receipt.beats.Count - 1; ContinueFeedback();
            }, 0.075f, 0.055f, 0.925f, 0.129f, Palette.Mint, Palette.Ink, 34);
            busy = true; next.interactable = false; StartCoroutine(EnableReceipt(next, viewGeneration));
            receipt.presented = true; PresentLocalMasterFeedback(); Save(); View.RefreshText(root); return true;
        }

        private void PresentLocalMasterFeedback()
        {
            MasterRunState state = CurrentMaster;
            if (state == null) return;
            var pending = state.events.Where(e => !e.acknowledged && e.tier < RewardTier.Mythic).ToList();
            DomainEvent strongest = pending.OrderByDescending(e => e.tier).ThenByDescending(e => e.day).FirstOrDefault();
            if (strongest != null)
            {
                world.PresentMasterEvent(strongest);
                if (strongest.tier >= RewardTier.Combo)
                    View.Label(root, "Joined reward event", strongest.title.Replace("\n", " "), 36, Palette.Mint,
                        TextAnchor.MiddleCenter, 0.055f, 0.55f, 0.945f, 0.615f);
            }
            foreach (DomainEvent e in pending) e.acknowledged = true;
            if (session != null && session.CompletedRun == null && archive.active?.runNumber == session.RunNumber)
                archive.active = session.Snapshot();
        }
    }
}
