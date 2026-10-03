using System;
using System.Linq;
using System.Collections;
using Horizon.Game;
using Horizon.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Horizon
{
    public sealed partial class HorizonApp
    {
        private RunMode requestedMasterMode;
        private string imagineGoal = "参加一次面试";
        private int imagineDifficulty = 1;
        private static readonly string[] ModeNames = { "12 DAYS", "30 DAYS", "LONG RUN", "PARALLEL LIVES", "IMAGINATION RUN", "EXPERIMENT RUN", "MIRROR RUN", "CHAOS RUN" };
        private static readonly string[] ModeDescriptions = { "十二天，看看今天的行为怎样回来。", "三十天，给更长的因果链留出空间。", "三十天长期投资与回望。", "从相同起点重演并比较两条人生。", "先预演失败与恢复，再进入行动。", "尝试 Trigger、知识与不同路线。", "带着最近的模式，走过相似的节点。", "扩展行为家族，面对更多不确定性。" };

        private void BuildMasterHome()
        {
            View.Button(root, "Master hub", "HORIZON ME · 想象与现实", ShowMasterHub,
                0.12f, 0.66f, 0.88f, 0.72f, Palette.Panel, Palette.Mint, 28);
            View.Label(root, "Master version", "v" + MasterSpecification.Version + "  ·  SEE FARTHER. IMAGINE DEEPER.", 20,
                Palette.Muted, TextAnchor.MiddleCenter, 0.05f, 0.018f, 0.95f, 0.06f);
        }
        private void BuildMasterBoard()
        {
            if (!session.UsesMasterRules) return;
            if (session.InExecutionMode)
            {
                if (boardHint != null) boardHint.text = "EXECUTION MODE · 决定已锁定，完成下一步执行";
                View.Button(root, "Resume execution", "继续执行", ShowExecution, 0.065f, 0.51f, 0.35f, 0.555f, Palette.Mint, Palette.Ink, 24);
            }
            if (session.RunNumber >= 2 || session.Day >= 5)
                View.Button(root, "Master hub", "ME · 想象", ShowMasterHub, 0.52f, 0.877f, 0.725f, 0.911f, Palette.Deep, Palette.Mint, 20);
            int energy = session.Master.overdriveEnergy;
            if (session.RunNumber >= 2 && energy > 0)
            {
                View.Fill(root, "Overdrive rail", Palette.Deep, 0.065f, 0.49f, 0.935f, 0.495f);
                View.Fill(root, "Overdrive energy", Palette.Mint, 0.065f, 0.49f, 0.065f + 0.87f * energy / 100f, 0.495f);
            }
        }
        private void CloseMasterPage()
        { if (overlay != null) { overlay.gameObject.SetActive(false); Destroy(overlay.gameObject); overlay = null; } }
        private RectTransform MasterPage(string name, string title, Action back = null)
        {
            CloseMasterPage(); overlay = View.Rect(root, name, 0, 0, 1, 1);
            View.Fill(overlay, "Master shade", new Color(0.009f, 0.023f, 0.04f, 0.985f), 0, 0, 1, 1, true);
            View.Label(overlay, "Master title", title, 43, Palette.Mint, TextAnchor.MiddleLeft, 0.07f, 0.87f, 0.93f, 0.95f);
            View.Button(overlay, "Master back", "返回", back ?? CloseMasterPage, 0.18f, 0.035f, 0.82f, 0.095f, Palette.Panel, Palette.Text, 28);
            return overlay;
        }
        private void PersistMasterAction()
        { if (session != null && session.CompletedRun == null && archive.active?.runNumber == session.RunNumber) archive.active = session.Snapshot(); Save(); }
        private bool CanPrepareMaster { get { return session != null && session.UsesMasterRules && !session.HasChosen && !session.CanPredict && !session.HasPredictionReview && session.CompletedRun == null && archive.active?.runNumber == session.RunNumber; } }
        private void ShowMasterHub()
        {
            if (busy) return;
            MasterPage("Horizon me hub", "H O R I Z O N  M E");
            View.Label(overlay, "Behavior summary", archive.me.Summary, 27, Palette.Text, TextAnchor.UpperLeft, 0.075f, 0.745f, 0.925f, 0.855f);
            string[] names = { "想象演练 · IMAGINE", "行动发动机 · EXECUTE", "内在议会 · COUNCIL", "知识熔炉 · FORGE", "未来轨道 · ORBIT", "现实桥梁 · REALITY", "世界观卡组", "人生模式与平行比较" };
            Action[] callbacks = { ShowImagineSetup, ShowExecution, ShowCouncil, ShowForge, ShowOrbit, ShowReality, ShowWorldviews, ShowModes };
            for (int i = 0; i < names.Length; i++)
            { float y = 0.63f - (i / 2) * 0.13f, x = i % 2 == 0 ? 0.07f : 0.52f;
                View.Button(overlay, "Master feature " + i, names[i], callbacks[i], x, y, x + 0.41f, y + 0.1f, Palette.Panel, Palette.Text, 25); }
            View.Button(overlay, "Behavior trends", "近期资源与行为模式", ShowMe, 0.07f, 0.125f, 0.93f, 0.2f, Palette.Deep, Palette.Gold, 27);
        }
        private void AddDecisionLockButton(RectTransform panel, CardSpec card)
        {
            if (!CanPrepareMaster || session.InExecutionMode || !session.CanPlay(card) || session.RunNumber == 1 && session.Day < 5) return;
            View.Button(panel, "Lock decision", "LOCK · 决定完成，进入执行", () => {
                session.LockDecision(card.Id); PersistMasterAction(); CloseMasterPage(); BuildBoard(); ShowExecution();
            }, 0.075f, 0.2f, 0.925f, 0.264f, Palette.Deep, Palette.Gold, 24);
        }
        private void ShowExecution()
        {
            if (!CanPrepareMaster) { ShowMasterUnavailable("行动发动机", "回到正在进行的人生，先读完结果与预测，再准备下一步。"); return; }
            MasterPage("Action engine", session.InExecutionMode ? "E X E C U T I O N" : "行动发动机", ShowMasterHub);
            var e = session.Master.engine;
            string[] names = { "Motivation · 动机", "Ability · 能力", "Trigger · 提示", "Friction · 摩擦", "Emotion · 情绪", "Alternative Reward", "Fatigue · 疲劳", "Social Pressure" };
            int[] values = { e.motivation, e.ability, e.trigger, e.friction, e.emotion, e.alternativeReward, e.fatigue, e.socialPressure };
            for (int i = 0; i < names.Length; i++)
            { float x = i % 2 == 0 ? 0.075f : 0.525f, y = 0.755f - (i / 2) * 0.072f;
                View.Label(overlay, "Engine variable " + i, names[i] + "  " + values[i], 25, Palette.Text, TextAnchor.MiddleLeft, x, y, x + 0.4f, y + 0.065f); }
            View.Label(overlay, "Engine meaning", "改变开始的条件：降低摩擦、装备提示、缩小步骤。", 24, Palette.Muted, TextAnchor.MiddleLeft, 0.075f, 0.455f, 0.925f, 0.515f);
            View.Button(overlay, "Lower friction", "删掉一步准备", () => { session.LowerFriction(); PersistMasterAction(); ShowExecution(); }, 0.07f, 0.37f, 0.49f, 0.435f, Palette.Panel, Palette.Text, 25);
            View.Button(overlay, "Equip triggers", "装备 Trigger  " + session.Master.triggers.Count + "/3", ShowTriggers, 0.51f, 0.37f, 0.93f, 0.435f, Palette.Panel, Palette.Mint, 25);
            ThoughtMonster monster = ThoughtMonsters.Current(e, session.Master.decision?.reopens ?? 0);
            View.Label(overlay, "Thought monster", "念头 · " + monster.name + "\n" + monster.reasonablePart + "\n" + monster.tradeoff, 25, Palette.Muted, TextAnchor.UpperLeft, 0.075f, 0.21f, 0.925f, 0.355f);
            if (session.InExecutionMode)
            {
                DecisionRecord d = session.Master.decision;
                string step = d.status == DecisionStatus.Ready ? "完成「" + CardCatalog.FindById(d.cardId).Name + "」" : (d.step + 1) + "/4 · " + d.steps[d.step];
                View.Button(overlay, "Execute next step", step, () => {
                    if (!CanPrepareMaster) return;
                    if (session.Master.decision.status == DecisionStatus.Ready)
                    { string id = session.Master.decision.cardId; CloseMasterPage();
                        HorizonCardDrag drag = cards.FirstOrDefault(pair => pair.Value.Id == id).Key; if (drag != null) CardPlayed(drag); }
                    else { session.ExecuteDecisionStep(); PersistMasterAction(); ShowExecution(); }
                }, 0.07f, 0.132f, 0.72f, 0.205f, Palette.Mint, Palette.Ink, 27);
                View.Button(overlay, "Unlock decision", "解锁", () => { session.UnlockDecision(); PersistMasterAction(); CloseMasterPage(); BuildBoard(); }, 0.745f, 0.132f, 0.93f, 0.205f, Palette.Deep, Palette.Coral, 24);
            }
            else View.Button(overlay, "Choose to lock", "回到今天，点一张牌来 LOCK", () => { CloseMasterPage(); BuildBoard(); }, 0.07f, 0.132f, 0.93f, 0.205f, Palette.Mint, Palette.Ink, 28);
        }
        private void ShowTriggers()
        {
            if (!CanPrepareMaster) return;
            MasterPage("Trigger equipment", "T R I G G E R", ShowExecution);
            View.Label(overlay, "Equipment limit", "最多装备三个。环境结构可以支持行动。", 27, Palette.Muted, TextAnchor.MiddleLeft, 0.075f, 0.79f, 0.925f, 0.855f);
            for (int i = 0; i < TriggerEquipment.Ids.Length; i++)
            {
                int index = i; string id = TriggerEquipment.Ids[i]; bool equipped = session.Master.triggers.Contains(id);
                float x = i % 2 == 0 ? 0.075f : 0.525f, y = 0.675f - (i / 2) * 0.106f;
                View.Button(overlay, "Trigger " + id, TriggerEquipment.Names[index] + (equipped ? " · 已装备" : ""), () => {
                    session.EquipTrigger(id); PersistMasterAction(); ShowTriggers();
                }, x, y, x + 0.4f, y + 0.08f, Palette.Panel, equipped ? Palette.Mint : Palette.Text, 27);
            }
            View.Label(overlay, "Trigger observation", "有效性来自近期执行记录；提示并不保证结果。", 24, Palette.Muted, TextAnchor.MiddleCenter, 0.075f, 0.15f, 0.925f, 0.22f);
        }
        private void ShowCouncil()
        {
            if (!CanPrepareMaster) { ShowMasterUnavailable("内在议会", "在正在进行的人生里，观察重大决定背后的动机来源。"); return; }
            MasterPage("Inner council", "I N N E R  C O U N C I L", ShowMasterHub);
            View.Label(overlay, "Council explanation", "动机可以同时存在。点开一个席位，看看它从哪里来。", 27, Palette.Muted, TextAnchor.MiddleLeft, 0.075f, 0.77f, 0.925f, 0.855f);
            var voices = InnerCouncil.Explain(session.Master.engine);
            for (int i = 0; i < voices.Count; i++)
            {
                CouncilVoice voice = voices[i]; float y = 0.64f - i * 0.12f;
                View.Button(overlay, "Council voice " + voice.name, voice.name + "  " + voice.weight + "%", () => {
                    MasterPage("Council sources", voice.name + " · " + voice.weight + "%", ShowCouncil);
                    View.Label(overlay, "Voice sources", string.Join("\n\n", voice.sources), 32, Palette.Text, TextAnchor.UpperLeft, 0.09f, 0.35f, 0.91f, 0.78f);
                }, 0.075f, y, 0.925f, y + 0.095f, Palette.Panel, Palette.Text, 31);
            }
        }
        private void ShowMasterUnavailable(string title, string message)
        { MasterPage("Master availability", title, ShowMasterHub); View.Label(overlay, "Availability", message, 32, Palette.Text, TextAnchor.MiddleCenter, 0.1f, 0.35f, 0.9f, 0.73f); }
        private InputField MasterInput(string name, string initial, Action<string> changed, float y, int limit = 100)
        {
            RectTransform field = View.Rect(overlay, name, 0.075f, y, 0.925f, y + 0.105f);
            Image image = field.gameObject.AddComponent<Image>(); image.color = Palette.Panel;
            Text label = View.Label(field, name + " text", initial, 30, Palette.Text, TextAnchor.MiddleLeft, 0.04f, 0.06f, 0.96f, 0.94f);
            InputField input = field.gameObject.AddComponent<InputField>(); input.targetGraphic = image; input.textComponent = label;
            input.characterLimit = limit; input.text = initial; input.lineType = InputField.LineType.MultiLineNewline;
            input.onValueChanged.AddListener(value => changed(value)); return input;
        }
        private void ShowImagineSetup()
        {
            MasterPage("Imagine setup", "I M A G I N E", ShowMasterHub);
            if (archive.imagination != null)
            {
                View.Label(overlay, "Saved imagination", archive.imagination.goal + "\n\n你的预演停在这里：" + archive.imagination.phase, 33, Palette.Text, TextAnchor.MiddleCenter, 0.1f, 0.4f, 0.9f, 0.76f);
                View.Button(overlay, "Resume imagination", "继续这条想象时间线", ShowImagineRun, 0.075f, 0.22f, 0.925f, 0.3f, Palette.Mint, Palette.Ink, 29); return;
            }
            View.Label(overlay, "Imagine prompt", "选择一个重要目标。短暂看见胜利，然后提前经历过程、挫折与重新开始。", 30, Palette.Text, TextAnchor.UpperLeft, 0.075f, 0.7f, 0.925f, 0.84f);
            MasterInput("Imagine goal", imagineGoal, value => imagineGoal = value, 0.565f);
            string[] labels = { "普通 · 1 次挫折", "挑战 · 3 次挫折", "困难 · 4 次挫折" };
            for (int i = 0; i < 3; i++) { int difficulty = i + 1; float y = 0.425f - i * 0.09f;
                View.Button(overlay, "Imagine difficulty " + difficulty, labels[i] + (imagineDifficulty == difficulty ? " · 已选" : ""), () => { imagineDifficulty = difficulty; ShowImagineSetup(); },
                    0.075f, y, 0.925f, y + 0.07f, Palette.Panel, imagineDifficulty == difficulty ? Palette.Mint : Palette.Text, 28); }
            View.Button(overlay, "Start imagination", "VICTORY ANCHOR · 开始预演", () => {
                if (string.IsNullOrWhiteSpace(imagineGoal)) return;
                archive.imagination = ImaginationEngine.Begin(Guid.NewGuid().ToString("N"), imagineGoal.Trim().ToLowerInvariant(), imagineGoal, imagineDifficulty);
                Save(); ShowImagineRun();
            }, 0.075f, 0.125f, 0.925f, 0.2f, Palette.Mint, Palette.Ink, 29);
        }
        private void ShowImagineRun()
        {
            ImagineRun run = archive.imagination; if (run == null) { ShowImagineSetup(); return; }
            MasterPage("Imagination run", run.phase == ImaginePhase.VictoryAnchor ? "V I C T O R Y  A N C H O R" : run.phase == ImaginePhase.Recover ? "R E C O V E R" : run.phase == ImaginePhase.Retry ? "F A I L  &  A G A I N" : "B U I L D  T H E  P A T H", ShowMasterHub);
            var art = View.Rect(overlay, "Imagine spectacle", 0.075f, 0.42f, 0.925f, 0.8f).gameObject.AddComponent<MasterSpectacleGraphic>();
            art.Kind = run.phase == ImaginePhase.VictoryAnchor ? DomainEventKind.VictoryAnchor : DomainEventKind.Comeback; art.color = Palette.Mint; art.raycastTarget = false;
            View.Label(overlay, "Imagine goal title", run.goal, 33, Palette.Gold, TextAnchor.MiddleCenter, 0.075f, 0.77f, 0.925f, 0.86f);
            View.Label(overlay, "Imagine beat", run.timeline.Last().text, 36, Palette.Text, TextAnchor.MiddleCenter, 0.1f, 0.43f, 0.9f, 0.71f);
            View.Label(overlay, "Imagine resilience", "挫折 " + run.failures + "/" + run.RequiredFailures + "  ·  RECOVERY " + run.recovered + "  ·  ×" + run.multiplier, 26, Palette.Muted, TextAnchor.MiddleCenter, 0.075f, 0.345f, 0.925f, 0.415f);
            if (run.phase == ImaginePhase.Recover)
            {
                string[] names = { "休息并安排下一步", "请求帮助", "缩小目标", "改变方法" };
                for (int i = 0; i < 4; i++) { RecoveryAction action = (RecoveryAction)i; float x = i % 2 == 0 ? 0.075f : 0.525f, y = 0.235f - (i / 2) * 0.1f;
                    View.Button(overlay, "Recovery action " + i, names[i], () => { ImaginationEngine.Recover(run, action); Save(); ShowImagineRun(); }, x, y, x + 0.4f, y + 0.08f, Palette.Panel, Palette.Mint, 26); }
            }
            else if (run.phase == ImaginePhase.Complete)
                View.Button(overlay, "Keep future memory", "留下 FUTURE MEMORY", () => {
                    if (CanPrepareMaster) session.AttachImagination(run);
                    archive.KeepImagination(run); archive.imagination = null; PersistMasterAction(); ShowMasterHub();
                }, 0.075f, 0.15f, 0.925f, 0.235f, Palette.Mint, Palette.Ink, 28);
            else View.Button(overlay, "Continue imagination", run.phase == ImaginePhase.VictoryAnchor ? "时间倒退 · 回到今天" : run.phase == ImaginePhase.Failure ? "失败以后怎么办？" : "经历下一步", () => {
                ImaginationEngine.Continue(run); if (run.phase == ImaginePhase.Complete) archive.KeepImagination(run); Save(); ShowImagineRun();
            }, 0.075f, 0.15f, 0.925f, 0.235f, Palette.Mint, Palette.Ink, 29);
        }
    }
}
