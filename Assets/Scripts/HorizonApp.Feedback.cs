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
        private HorizonCardDrag activeDrag;
        private bool dropReady;

        private void RenderFeedbackReceipt()
        {
            FeedbackRecord receipt = archive.pendingFeedback;
            if (receipt == null) { BuildBoard(); return; }
            if (receipt.kind == FeedbackKind.Deadline)
            { ShowDeadlineResult(archive.runs[archive.runs.Count - 1]); return; }
            Clear();
            world.ShowBoard();
            world.SetTimeline(session.Actions, session.Deadline);
            bool firstPresentation = !receipt.presented;
            receipt.presented = true;
            List<FeedbackBeat> beats = receipt.beats;
            bool hasBeats = beats != null && beats.Count > 0;
            receipt.page = hasBeats ? Mathf.Clamp(receipt.page, 0, beats.Count - 1) : 0;
            bool newPage = firstPresentation || receipt.page > 0 && (receipt.presentedPages & (1 << receipt.page)) == 0;
            receipt.presentedPages |= 1 << receipt.page;
            FeedbackBeat beat = hasBeats ? beats[receipt.page] : null;
            Save();
            bool action = receipt.kind == FeedbackKind.Choice;
            bool difficult = !action && beat != null && PlayExperience.IsDifficult(beat.delta);
            bool quiet = action && beat != null && beat.intent == CardKind.Growth;
            bool chain = !action && beat != null && beat.chainSize >= 3;
            Color accent = difficult ? Palette.Coral : chain ? Palette.Gold : action ? Palette.Gold : Palette.Mint;
            if (beat != null) world.Preview(beat.intent, beat.support);

            View.Label(root, "Result day", "DAY " + receipt.day.ToString("00") + " / " + session.Deadline, 32,
                Palette.Text, TextAnchor.MiddleLeft, 0.06f, 0.951f, 0.55f, 0.986f);
            Text wallet = View.Label(root, "Wallet", "星尘 " + archive.wallet.stardust, 28, Palette.Gold,
                TextAnchor.MiddleRight, 0.6f, 0.951f, 0.94f, 0.986f);
            View.Panel(root, "Outcome ribbon", new Color(0.015f, 0.045f, 0.065f, 0.92f),
                0.05f, 0.791f, 0.95f, 0.917f, 28);
            View.Label(root, "Outcome type", action ? "行动完成 · 今天已记录" : chain ? "因果连锁 · CASCADE ×" + beat.chainSize : difficult ?
                "回声抵达 · 现在仍可以调整" : "回声抵达 · 过去正在回应你", 29, accent,
                TextAnchor.MiddleCenter, 0.085f, 0.861f, 0.915f, 0.903f);
            string origin = beat == null ? "" : "D" + beat.sourceDay.ToString("00") + " · " + beat.source;
            string destination = beat == null ? "" : action ? beat.destinationDay == 0 ? "今天 · 恢复" :
                "D" + beat.destinationDay.ToString("00") + " · 回声" : "D" + receipt.day.ToString("00") + " · 现在";
            View.Label(root, "Receipt origin", origin, 27, Palette.Text,
                TextAnchor.MiddleCenter, 0.085f, 0.804f, 0.445f, 0.853f);
            View.Label(root, "Receipt arrow", "→", 38, accent,
                TextAnchor.MiddleCenter, 0.453f, 0.801f, 0.547f, 0.857f);
            View.Label(root, "Receipt destination", destination, 27, accent,
                TextAnchor.MiddleCenter, 0.555f, 0.804f, 0.915f, 0.853f);

            RectTransform panel = View.Panel(root, "Readable result", Palette.Panel,
                0.05f, 0.048f, 0.95f, 0.537f, 38).rectTransform;
            panel.gameObject.AddComponent<PanelEntrance>();
            View.Label(panel, "Result title", beat == null ? receipt.title :
                action ? "「" + beat.title + "」已开始" : beat.title, 42, Palette.Text,
                TextAnchor.MiddleLeft, 0.065f, 0.797f, 0.935f, 0.948f);
            View.Label(panel, "Result timing", action ? "今天已经发生的变化" : "这次回声带来的实际变化", 24,
                Palette.Muted, TextAnchor.MiddleLeft, 0.065f, 0.735f, 0.935f, 0.798f);
            if (beat == null) ResultText(panel, receipt.description);
            else
            {
                DrawReceiptChanges(panel, beat.delta, newPage);
                View.Label(panel, "Result explanation", beat.meaning, 27, Palette.Text,
                    TextAnchor.UpperLeft, 0.065f, 0.287f, 0.935f, 0.441f);
            }
            int pageStars = beat != null && beat.stardust > 0 ? beat.stardust : receipt.stardust;
            View.Label(panel, "Reward", "+" + pageStars + " 星尘 · 已收集", 29, Palette.Gold,
                TextAnchor.MiddleLeft, 0.065f, 0.207f, 0.935f, 0.283f);
            bool more = hasBeats && receipt.page < beats.Count - 1;
            string next = more ? "下一条回声  " + (receipt.page + 2) + " / " + beats.Count : action ?
                session.NeedsStation ? "走进未来站" : "前往第 " + (receipt.day + 1) + " 天" :
                session.CanPredict ? "试着预测三天后" : "回到今天，选一张牌";
            View.Label(panel, "Hold result", receipt.preparedGates != 0 ? PreparedGateText(receipt.preparedGates) : hasBeats && beats.Count > 1 ?
                "回声 " + (receipt.page + 1) + " / " + beats.Count + " · 看完这一条再继续" :
                difficult ? "未来还没写完。下一张牌，仍然由你决定。" : "这份变化会留下。看完，再继续。", 23, Palette.Muted,
                TextAnchor.MiddleCenter, 0.065f, 0.145f, 0.935f, 0.205f);
            Button nextButton = View.Button(panel, "Continue result", next, ContinueFeedback,
                0.065f, 0.032f, 0.935f, 0.142f, Palette.Mint, Palette.Ink, 31);
            busy = true;
            nextButton.interactable = false;
            StartCoroutine(EnableReceipt(nextButton, viewGeneration));
            if (newPage)
            {
                if (!quiet && !difficult && pageStars > 0)
                {
                    StarBurst(pageStars, new Vector2(0.5f, 0.173f));
                    if (!action) EchoCrown(new Vector2(0.5f, 0.665f), accent, chain ? 3 : 2);
                }
                if (firstPresentation && !quiet && receipt.stardust > 0)
                {
                    var counter = wallet.gameObject.AddComponent<RewardCounter>();
                    counter.From = archive.wallet.stardust - receipt.stardust;
                    counter.To = archive.wallet.stardust;
                }
                ReceiptPulse(root, new Vector2(0.5f, 0.665f), accent);
            }
            View.RefreshText(root);
        }

        private int PreparedGateChanges(RunSnapshot before)
        {
            GameSession previous = GameSession.Restore(before);
            int changes = 0;
            for (int i = 0; i < 3; i++) if (!ProductExperience.GateReady(previous, i) && ProductExperience.GateReady(session, i)) changes |= 1 << i;
            return changes;
        }

        private static string PreparedGateText(int mask)
        {
            string text = "";
            string[] names = { "能力", "状态", "支援" };
            for (int i = 0; i < 3; i++) if ((mask & (1 << i)) != 0) text += (text.Length == 0 ? "" : " · ") + names[i];
            return text + "门 · 目前已准备好，继续照顾抵达前的自己。";
        }

        private void EchoCrown(Vector2 center, Color color, int intensity)
        {
            if (archive.preferences.reducedMotion) return;
            RectTransform rect = View.Rect(root, "Echo bloom crown", center.x - 0.45f,
                center.y - 0.25f, center.x + 0.45f, center.y + 0.25f);
            var crown = rect.gameObject.AddComponent<EchoCrownGraphic>();
            crown.color = new Color(color.r, color.g, color.b, 0.66f); crown.Intensity = intensity; crown.raycastTarget = false;
            rect.SetSiblingIndex(2);
        }

        private IEnumerator EnableReceipt(Button button, int generation)
        {
            // Also prevents a double tap on one receipt from skipping the next receipt.
            yield return new WaitForSecondsRealtime(0.24f);
            if (generation != viewGeneration || button == null) yield break;
            busy = false;
            button.interactable = true;
        }

        private void DrawReceiptChanges(Transform parent, ResourceDelta delta, bool animate)
        {
            int[] values = delta == null ? new int[6] : new[] {
                delta.energy, delta.mood, delta.insight, delta.relation, delta.money, delta.ability };
            string[] names = { "精力", "心情", "洞察", "关系", "金钱", "能力" };
            int count = 0;
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] == 0) continue;
                int column = count % 3, row = count / 3;
                float x = 0.065f + column * 0.297f, y = 0.589f - row * 0.129f;
                Color color = values[i] > 0 ? Palette.Mint : Palette.Coral;
                RectTransform chip = View.Panel(parent, "Actual resource change " + names[i],
                    new Color(color.r * 0.14f, color.g * 0.14f, color.b * 0.14f, 1),
                    x, y, x + 0.276f, y + 0.115f, 17).rectTransform;
                View.Label(chip, "Change name", names[i], 23, Palette.Muted,
                    TextAnchor.MiddleLeft, 0.1f, 0.58f, 0.9f, 0.94f);
                View.Label(chip, "Change amount", (values[i] > 0 ? "+" : "") + values[i], 40, color,
                    TextAnchor.MiddleLeft, 0.1f, 0.06f, 0.9f, 0.62f);
                if (animate) chip.gameObject.AddComponent<ReceiptPop>().Delay = count * 0.08f;
                count++;
            }
            if (count == 0) View.Label(parent, "Unchanged state", "状态保持不变 · 已按实际上限记录", 29,
                Palette.Muted, TextAnchor.MiddleLeft, 0.065f, 0.49f, 0.935f, 0.69f);
        }

        private static void ReceiptPulse(Transform parent, Vector2 center, Color color)
        {
            if (VisualPreferences.ReducedMotion) return;
            RectTransform rect = View.Rect(parent, "Feedback ripple",
                center.x - 0.1f, center.y - 0.056f, center.x + 0.1f, center.y + 0.056f);
            var ring = rect.gameObject.AddComponent<DropRingGraphic>();
            ring.color = new Color(color.r, color.g, color.b, 0.72f);
            ring.Thickness = 4; ring.raycastTarget = false;
            rect.gameObject.AddComponent<FeedbackRipple>();
        }
    }
}
