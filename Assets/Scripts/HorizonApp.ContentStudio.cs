using System;
using System.Linq;
using System.Threading;
using Horizon.Game;
using Horizon.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Horizon
{
    public sealed partial class HorizonApp
    {
        private void ShowContentStudio(NarrativePurpose purpose, string goal, Action back)
        {
            string[] names = { "未来自己", "个人小动作", "近期模式解释", "想象中的困难", "知识变成动作", "与内在动机对话" };
            MasterPage("Personal content studio", names[(int)purpose], back);
            string input = AIText.Bound(goal, 300);
            MasterInput("Content studio goal", input, value => input = value, 0.68f, 300);
            View.Label(overlay, "Content studio privacy", archive.ai.provider == AIProvider.Local ? "本地内容 · 离线可用" : "点击生成后发送这个目标与最多六条近期因果。", 26, Palette.Muted,
                TextAnchor.MiddleLeft, 0.075f, 0.59f, 0.925f, 0.68f);
            RectTransform viewport = View.Rect(overlay, "Studio scroll viewport", 0.075f, 0.315f, 0.925f, 0.58f);
            viewport.gameObject.AddComponent<Image>().color = Palette.Panel; viewport.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            Text output = View.Label(viewport, "Content studio output", "保留现实条件，考虑过程、困难与恢复。最后的决定仍然属于你。", 29, Palette.Text,
                TextAnchor.UpperLeft, 0.03f, 1, 0.97f, 1); output.supportRichText = false;
            output.rectTransform.pivot = new Vector2(0.5f, 1); output.resizeTextForBestFit = false; output.verticalOverflow = VerticalWrapMode.Overflow;
            output.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = output.rectTransform;
            scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;
            Button generate = View.Button(overlay, "Generate studio content", "生成这段内容", () => { }, 0.075f, 0.20f, 0.925f, 0.275f, Palette.Mint, Palette.Ink, 28);
            generate.onClick.AddListener(async () =>
            {
                CancelAIRequest(); var request = new CancellationTokenSource(); aiRequest = request;
                generate.interactable = false; output.text = "正在根据近期证据生成…";
                try
                {
                    var context = new NarrativeContext(input, archive.me.Summary,
                        session == null ? Array.Empty<string>() : NarrativeContext.From(session).Evidence, purpose);
                    PersonalContent content = await CreateContentAdapter(archive.ai, true).Personalize(context, request.Token);
                    request.Token.ThrowIfCancellationRequested(); if (output == null) return;
                    output.text = purpose == NarrativePurpose.Pattern ? content.patternExplanation :
                        purpose == NarrativePurpose.PersonalQuest ? content.quest : content.futureSelfLine + "\n\n可尝试的小步骤\n" + content.quest;
                    View.Label(overlay, "Studio content source", content.source + " · " + content.status, 22, Palette.Muted,
                        TextAnchor.MiddleLeft, 0.075f, 0.275f, 0.925f, 0.315f).supportRichText = false;
                    if (purpose == NarrativePurpose.Imagination)
                        View.Button(overlay, "Use imagined situation", "带着这段情境继续预演", () => { imaginePersonalSituation = AIText.Bound(content.futureSelfLine, 180); back(); },
                            0.075f, 0.12f, 0.925f, 0.185f, Palette.Panel, Palette.Gold, 27);
                    else if (purpose == NarrativePurpose.Knowledge || purpose == NarrativePurpose.PersonalQuest)
                        View.Button(overlay, "Choose studio reality quest", "选择为今天的现实小动作", () => {
                            if (!archive.reality.quests.Any(q => q.localDate == DateTime.Now.ToString("yyyy-MM-dd")))
                                archive.reality.Offer(DateTime.Now, "personal:" + AIText.Bound(input, 100).ToLowerInvariant(), AIText.Bound(content.quest, 100), null);
                            Save(); ShowReality();
                        }, 0.075f, 0.12f, 0.925f, 0.185f, Palette.Panel, Palette.Gold, 26);
                }
                catch (OperationCanceledException) { }
                catch (Exception) { if (output != null) output.text = "暂时没有生成完成，可以再试一次。"; }
                finally { if (generate != null) generate.interactable = true; if (aiRequest == request) aiRequest = null; request.Dispose(); }
            });
        }

        private void ShowTimeVision()
        {
            int level = session == null ? archive.me.HorizonLevel : HorizonProgress.Resolve(session, archive.journey).MasterLevel;
            MasterPage("Eight time horizons", "你的视野正在成长", ShowMe);
            for (int i = 0; i < 8; i++)
            {
                float y = 0.75f - i * 0.065f;
                View.Label(overlay, "Horizon capability " + i, (level > i ? "● " : "○ ") + "HORIZON " + (i + 1) + " · " + MasterSpecification.HorizonNames[i],
                    29, level > i ? Palette.Mint : Palette.Muted, TextAnchor.MiddleLeft, 0.09f, y, 0.91f, y + 0.055f);
            }
            View.Label(overlay, "Horizon growth evidence", "预测、真实因果链和再次行动带来理解。\n新的视野会打开概率、平行分支与隐藏来路。", 26, Palette.Text, TextAnchor.MiddleLeft,
                0.075f, 0.20f, 0.925f, 0.28f);
            var mystery = session?.Mysteries.FirstOrDefault(m => !m.revealed);
            if (session?.UsesExpedition == true && mystery != null)
            {
                Button reveal = View.Button(overlay, "Reveal hidden variable", "看看这份余力从哪里来", () => {
                    if (session.RevealHiddenCause(mystery.consequenceNodeId)) PersistMasterAction(); ShowTimeVision(); },
                    0.075f, 0.12f, 0.925f, 0.185f, Palette.Panel, Palette.Gold, 27);
                reveal.interactable = session.MasterHorizon >= 8 || session.Day <= session.Master.overdriveUntilDay;
            }
        }
    }
}
