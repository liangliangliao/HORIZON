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
        private int imagineFailures;
        private string imagineTargetCardId;
        private string imaginePersonalSituation = "";
        private bool executionFromBoard;
        private static readonly string[] ModeNames = { "12 DAYS", "30 DAYS", "LONG RUN", "PARALLEL LIVES", "IMAGINATION RUN", "EXPERIMENT RUN", "MIRROR RUN", "CHAOS RUN" };
        private static readonly string[] ModeDescriptions = { "十二天，看看今天的行为怎样回来。", "三十天，给更长的因果链留出空间。", "六十天，持续投资与阶段回望。", "从相同起点重演并比较两条人生。", "先预演失败与恢复，再进入行动。", "尝试 Trigger、知识与不同路线。", "主动重现最近停下来的个人节点。", "扩展行为家族，面对更多不确定性。" };

        private void BuildMasterHome()
        {
            View.Button(root, "Master hub", "HORIZON ME · 想象与现实", ShowMasterHub,
                0.12f, 0.532f, 0.88f, 0.58f, Palette.Panel, Palette.Mint, 28);
            View.Label(root, "Master version", "v" + MasterSpecification.Version + "  ·  SEE FARTHER. IMAGINE DEEPER.", 20,
                Palette.Muted, TextAnchor.MiddleCenter, 0.05f, 0.018f, 0.95f, 0.06f);
        }
        private void BuildMasterBoard()
        {
            if (!session.UsesMasterRules) return;
            if (session.InExecutionMode)
            {
                if (boardHint != null) boardHint.text = "EXECUTION MODE · 决定已锁定，完成下一步执行";
                View.Button(root, "Resume execution", "决定好了 · 继续执行", () => { executionFromBoard = true; ShowExecution(); },
                    0.065f, 0.754f, 0.49f, 0.788f, Palette.Mint, Palette.Ink, 22);
            }
            if (session.RunNumber >= 2 || session.Day >= 3)
                View.Button(root, "Master hub", "ME · 想象", ShowMasterHub, 0.52f, 0.877f, 0.725f, 0.911f, Palette.Deep, Palette.Mint, 20);
            if (CanPrepareMaster && (session.Day >= 3 || session.RunNumber >= 2))
                View.Button(root, "Prepare with imagination", archive.imagination == null ? "先练一次失败与恢复" : "继续上次失败预演", BeginBoardImagination,
                    0.52f, 0.754f, 0.945f, 0.788f, Palette.Panel, Palette.Mint, 22);
            int energy = session.Master.overdriveEnergy;
            bool overdrive = session.Day <= session.Master.overdriveUntilDay;
            world.SetInsightState(energy, overdrive);
            if (overdrive)
                View.Label(root, "Overdrive insight", "OVERDRIVE · 隐藏来路正在变得清晰", 23, Palette.Gold,
                    TextAnchor.MiddleCenter, 0.065f, 0.485f, 0.935f, 0.515f);
            if (session.RunNumber >= 2 && energy > 0)
            {
                View.Fill(root, "Overdrive rail", Palette.Deep, 0.065f, 0.49f, 0.935f, 0.495f);
                View.Fill(root, "Overdrive energy", Palette.Mint, 0.065f, 0.49f, 0.065f + 0.87f * energy / 100f, 0.495f);
            }
        }
        private void CloseMasterPage()
        { CancelAIRequest(); if (world != null) { world.Cinematics?.CancelAll(); world.EndImaginationScene(); } if (overlay != null) { overlay.gameObject.SetActive(false); Destroy(overlay.gameObject); overlay = null; } RestoreMasterBackground(); }
        private RectTransform MasterPage(string name, string title, Action back = null)
        {
            CloseMasterPage(); overlay = View.Rect(root, name, 0, 0, 1, 1);
            HideMasterBackground();
            View.Fill(overlay, "Master shade", new Color(0.009f, 0.023f, 0.04f, 0.985f), 0, 0, 1, 1, true);
            View.Label(overlay, "Master title", title, 43, Palette.Mint, TextAnchor.MiddleLeft, 0.07f, 0.87f, 0.73f, 0.95f);
            View.Button(overlay, "Back master", "返回", back ?? CloseMasterPage, 0.765f, 0.882f, 0.93f, 0.939f, Palette.Panel, Palette.Text, 26);
            View.Button(overlay, "Master back", "返回", back ?? CloseMasterPage, 0.18f, 0.035f, 0.82f, 0.095f, Palette.Panel, Palette.Text, 28);
            return overlay;
        }
        private void PersistMasterAction()
        { if (session != null && session.CompletedRun == null && archive.active?.runNumber == session.RunNumber) archive.active = session.Snapshot(); Save(); }
        private bool CanPrepareMaster { get { return session != null && session.UsesMasterRules && !session.HasChosen && !session.CanPredict && !session.HasPredictionReview && session.CompletedRun == null && archive.active?.runNumber == session.RunNumber; } }
        private void ShowMasterHub()
        {
            if (busy) return;
            executionFromBoard = false;
            MasterPage("Horizon me hub", "H O R I Z O N  M E");
            View.Label(overlay, "Behavior summary", archive.me.Summary, 27, Palette.Text, TextAnchor.UpperLeft, 0.075f, 0.745f, 0.925f, 0.855f);
            string[] names = { "想象演练 · IMAGINE", "行动发动机 · EXECUTE", "内在议会 · COUNCIL", "知识熔炉 · FORGE", "未来轨道 · ORBIT", "现实桥梁 · REALITY", "世界观卡组", "人生模式与平行比较" };
            Action[] callbacks = { () => { archive.playGuide.imagineFromBoard = false; ShowImagineSetup(); }, ShowExecution, ShowCouncil, ShowForge, ShowOrbit, ShowReality, ShowWorldviews, ShowModes };
            for (int i = 0; i < names.Length; i++)
            { float y = 0.64f - (i / 2) * 0.10f, x = i % 2 == 0 ? 0.07f : 0.52f;
                View.Button(overlay, "Master feature " + i, names[i], callbacks[i], x, y, x + 0.41f, y + 0.1f, Palette.Panel, Palette.Text, 25); }
            View.Button(overlay, "Imagination comparisons", "预演与实际对照", ShowImaginationComparison, 0.07f, 0.205f, 0.93f, 0.26f, Palette.Deep, Palette.Gold, 28);
            View.Button(overlay, "Behavior trends", "近期行为模式", ShowMe, 0.07f, 0.125f, 0.49f, 0.2f, Palette.Deep, Palette.Gold, 26);
            View.Button(overlay, "AI future self dialogue", "AI · 未来自己", ShowFutureSelfDialogue, 0.51f, 0.125f, 0.93f, 0.2f, Palette.Deep, Palette.Mint, 26);
            View.Button(overlay, "Reward collection", "因果纪念物 · " + archive.rewardCollection.items.Count, ShowRewardCollection, 0.075f,0.096f,0.925f,0.124f,Palette.Deep,Palette.Gold,23);
            if (CanPrepareMaster && session.UsesExpedition)
                View.Button(overlay, "Choose life route", "人生路线 · 五种不同的选择空间", ShowLifeRoutes, 0.07f, 0.275f, 0.93f, 0.32f, Palette.Deep, Palette.Mint, 24);
        }
        private void AddDecisionLockButton(RectTransform panel, CardSpec card)
        {
            if (!CanPrepareMaster || session.InExecutionMode || !session.CanPlay(card) || session.RunNumber == 1 && session.Day < 3) return;
            View.Button(panel, "Lock decision", "决定好了 · 锁定并一步步执行", () => {
                session.LockDecision(card.Id); executionFromBoard = true; PersistMasterAction(); CloseMasterPage(); BuildBoard(); ShowExecution();
            }, 0.075f, 0.22f, 0.925f, 0.3f, Palette.Deep, Palette.Gold, 26);
        }
        private void ShowExecution()
        {
            if (!CanPrepareMaster) { ShowMasterUnavailable("行动发动机", "回到正在进行的人生，先读完结果与预测，再准备下一步。"); return; }
            MasterPage("Action engine", session.InExecutionMode ? "决定已完成 · 开始执行" : "让下一步更容易开始", ReturnFromExecution);
            var e = session.Master.engine;
            if (session.InExecutionMode)
            {
                DecisionRecord decision = session.Master.decision;
                View.Label(overlay, "Chosen execution", "今天已选择「" + CardCatalog.FindById(decision.cardId).Name + "」", 34, Palette.Gold,
                    TextAnchor.MiddleLeft, 0.075f, 0.78f, 0.925f, 0.85f);
                for (int i = 0; i < decision.steps.Count; i++)
                {
                    float y = 0.685f - i * 0.055f;
                    string status = i < decision.step ? "完成" : i == decision.step ? "现在" : "稍后";
                    View.Label(overlay, "Execution step " + i, status + " · " + (i + 1) + ". " + decision.steps[i], 32,
                        i == decision.step ? Palette.Mint : i < decision.step ? Palette.Muted : Palette.Text,
                        TextAnchor.MiddleLeft, 0.09f, y, 0.91f, y + 0.052f);
                }
            }
            else View.Label(overlay, "Engine invitation", "已经想做，仍然没有开始？\n先选一张行动牌，再把决定锁定。\n之后只处理执行步骤。", 33, Palette.Text,
                TextAnchor.MiddleLeft, 0.075f, 0.58f, 0.925f, 0.84f);
            CardSpec locked = session.InExecutionMode ? CardCatalog.FindById(session.Master.decision.cardId) : null;
            string cost = locked == null ? "查看行动条件与来源" : "执行成本 · " + PlayExperience.NowLabel(session.ImmediateEffect(locked)) +
                (session.PreparationSaving(locked) > 0 ? " · 准备已省1精力" : session.PreparationWillSave(locked) ? " · 准备可省1精力" : " · 查看准备条件");
            View.Button(overlay, "Action factors", cost, ShowActionFactors,
                0.075f, 0.455f, 0.925f, 0.515f, Palette.Deep, Palette.Muted, 24);
            string preparationLabel = e.friction <= 1 ? "准备已经最小" : session.CatalogVersion >= 9 && session.Insight < 1 ? "专注不足 · 需要1" : session.CatalogVersion >= 9 ? "缩小准备 · 专注-1" : "删掉一步准备";
            Button preparation = View.Button(overlay, "Lower friction", preparationLabel, () => { session.LowerFriction(); PersistMasterAction(); ShowExecution(); }, 0.07f, 0.37f, 0.49f, 0.435f, Palette.Panel, Palette.Text, 25);
            preparation.interactable = e.friction > 1 && (session.CatalogVersion < 9 || session.Insight >= 1);
            View.Button(overlay, "Equip triggers", "设置行动提示  " + session.Master.triggers.Count + "/3", ShowTriggers, 0.51f, 0.37f, 0.93f, 0.435f, Palette.Panel, Palette.Mint, 25);
            ThoughtMonster monster = ThoughtMonsters.Current(e, session.Master.decision?.reopens ?? 0);
            View.Label(overlay, "Thought monster", "念头 · " + monster.name + "\n" + monster.reasonablePart + "\n" + monster.tradeoff, 23, Palette.Muted, TextAnchor.UpperLeft, 0.075f, session.UsesExpedition ? 0.268f : 0.21f, 0.925f, 0.355f);
            if (session.InExecutionMode)
            {
                DecisionRecord d = session.Master.decision;
                string step = d.status == DecisionStatus.Ready ? "今天就做「" + CardCatalog.FindById(d.cardId).Name + "」" :
                    "按这个计划准备 · 不再重新比较";
                View.Button(overlay, "Execute next step", step, () => {
                    if (!CanPrepareMaster) return;
                    if (session.Master.decision.status == DecisionStatus.Ready)
                    { string id = session.Master.decision.cardId; CloseMasterPage();
                        HorizonCardDrag drag = cards.FirstOrDefault(pair => pair.Value.Id == id).Key; if (drag != null) CardPlayed(drag); }
                    else { session.ExecuteDecisionStep();
                        while (session.Master.decision.status != DecisionStatus.Ready) session.ExecuteDecisionStep();
                        PersistMasterAction(); ShowExecution(); }
                }, 0.07f, 0.132f, 0.72f, 0.205f, Palette.Mint, Palette.Ink, 27);
                View.Button(overlay, "Unlock decision", "解锁", () => { session.UnlockDecision(); PersistMasterAction(); CloseMasterPage(); BuildBoard(); }, 0.745f, 0.132f, 0.93f, 0.205f, Palette.Deep, Palette.Coral, 24);
            }
            else View.Button(overlay, "Choose to lock", "回到今天 · 选牌并锁定决定", () => { CloseMasterPage(); BuildBoard(); }, 0.07f, 0.132f, 0.93f, 0.205f, Palette.Mint, Palette.Ink, 28);
            View.RefreshText(overlay);
            if (session.UsesExpedition)
                View.Button(overlay, "Adjust action environment", "调整环境与念头", ShowEnvironmentChoices, 0.075f, 0.215f, 0.925f, 0.258f, Palette.Deep, Palette.Mint, 24);
        }
        private void ReturnFromExecution()
        {
            if (executionFromBoard) { CloseMasterPage(); BuildBoard(); }
            else ShowMasterHub();
        }
        private void ShowActionFactors()
        {
            if (!CanPrepareMaster) return;
            MasterPage("Action factors", "行动的条件", ShowExecution);
            var e = session.Master.engine;
            string[] names = { "动机", "能力", "行动提示", "准备摩擦", "情绪", "其他即时奖励", "疲劳", "外部承诺" };
            int[] values = { e.motivation, e.ability, e.trigger, e.friction, e.emotion, e.alternativeReward, e.fatigue, e.socialPressure };
            for (int i = 0; i < names.Length; i++)
            { float x = i % 2 == 0 ? 0.075f : 0.525f, y = 0.71f - (i / 2) * 0.105f;
                View.Label(overlay, "Engine variable " + i, names[i] + "  " + values[i] + "/10", 29, Palette.Text, TextAnchor.MiddleLeft, x, y, x + 0.4f, y + 0.08f); }
            View.Label(overlay, "Factors meaning", session.CatalogVersion >= 9 ? "备妥条件 = 动机+能力+提示+情绪+承诺-摩擦-其他奖励-疲劳。完成准备后至少6，且有匹配提示时，执行省1精力。" :
                "这条人生保持原来的准备方式：记录提示与执行，行动仍按原成本结算。可进入自由练习体验新的准备策略。", 29, Palette.Muted,
                TextAnchor.MiddleLeft, 0.075f, 0.215f, 0.925f, 0.375f);
            View.Button(overlay, "Explain motives", "看看不同动机的来源", ShowCouncil, 0.075f, 0.125f, 0.925f, 0.2f, Palette.Panel, Palette.Mint, 28);
        }
        private void ShowTriggers()
        {
            if (!CanPrepareMaster) return;
            MasterPage("Trigger equipment", "T R I G G E R", ShowExecution);
            View.Label(overlay, "Equipment limit", session.CatalogVersion >= 9 ? "最多三个提示。匹配行动并备妥条件，执行最多省1精力。" : "最多三个提示。当前人生记录准备，行动保持原成本。", 27, Palette.Muted, TextAnchor.MiddleLeft, 0.075f, 0.79f, 0.925f, 0.855f);
            for (int i = 0; i < TriggerEquipment.Ids.Length; i++)
            {
                int index = i; string id = TriggerEquipment.Ids[i]; bool equipped = session.Master.triggers.Contains(id);
                float x = i % 2 == 0 ? 0.075f : 0.525f, y = 0.675f - (i / 2) * 0.106f;
                Button equipment = View.Button(overlay, "Trigger " + id, TriggerEquipment.Names[index] + (equipped ? " · 已装备" : TriggerEquipment.Cost(id) > 0 && session.CatalogVersion >= 9 ? " · 金钱-1" : " · 免费") + "\n" + (session.CatalogVersion >= 9 ? TriggerEquipment.Effect(id) : "记录环境提示"), () => {
                    if (equipped && session.UsesExpedition) session.RemoveTrigger(id); else session.EquipTrigger(id); PersistMasterAction(); ShowTriggers();
                }, x, y, x + 0.4f, y + 0.08f, Palette.Panel, equipped ? Palette.Mint : Palette.Text, 27);
                equipment.interactable = equipped && session.UsesExpedition || !equipped && session.Master.triggers.Count < 3 && (session.CatalogVersion < 9 || session.Money >= TriggerEquipment.Cost(id));
            }
            View.Label(overlay, "Trigger observation", session.CatalogVersion >= 9 ? "选择与行动相符的提示。其作用会进入实际成本与因果图。" : "保留这条人生的原有成本，准备与提示会留下记录。", 24, Palette.Muted, TextAnchor.MiddleCenter, 0.075f, 0.15f, 0.925f, 0.22f);
        }
        private void ShowCouncil()
        {
            if (!CanPrepareMaster) { ShowMasterUnavailable("内在议会", "在正在进行的人生里，观察重大决定背后的动机来源。"); return; }
            MasterPage("Inner council", "I N N E R  C O U N C I L", ShowMasterHub);
            View.Label(overlay, "Council explanation", "动机可以同时存在。点开一个席位，看看它从哪里来。", 27, Palette.Muted, TextAnchor.MiddleLeft, 0.075f, 0.77f, 0.925f, 0.855f);
            var voices = session.UsesExpedition ? InnerCouncil.Explain(session) : InnerCouncil.Explain(session.Master.engine);
            overlay.Find("Master shade").GetComponent<Image>().color=new Color(.009f,.023f,.04f,.1f);
            world.ShowCouncilScene(voices);
            for (int i = 0; i < voices.Count; i++)
            {
                CouncilVoice voice = voices[i]; float y = 0.66f - i * (voices.Count > 4 ? 0.085f : 0.12f);
                View.Button(overlay, "Council voice " + voice.name, voice.name + "  " + voice.weight + "%", () => {
                    MasterPage("Council sources", voice.name + " · " + voice.weight + "%", ShowCouncil);
                    View.Label(overlay, "Voice sources", string.Join("\n\n", voice.sources), 32, Palette.Text, TextAnchor.UpperLeft, 0.09f, 0.35f, 0.91f, 0.78f);
                }, 0.075f, y, 0.925f, y + (voices.Count > 4 ? 0.075f : 0.095f), Palette.Panel, Palette.Text, 31);
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
            MasterPage("Imagine setup", "先练一次失败后的下一步", ReturnFromImagination);
            if (archive.imagination != null)
            {
                View.Label(overlay, "Saved imagination", archive.imagination.goal + "\n\n你的预演停在这里：" + archive.imagination.phase, 33, Palette.Text, TextAnchor.MiddleCenter, 0.1f, 0.4f, 0.9f, 0.76f);
                View.Button(overlay, "Resume imagination", "继续这条想象时间线", ShowImagineRun, 0.075f, 0.22f, 0.925f, 0.3f, Palette.Mint, Palette.Ink, 29); return;
            }
            View.Label(overlay, "Imagine prompt", "选择一个重要目标。短暂看见胜利，然后提前经历过程、挫折与重新开始。", 30, Palette.Text, TextAnchor.UpperLeft, 0.075f, 0.7f, 0.925f, 0.84f);
            MasterInput("Imagine goal", imagineGoal, value => imagineGoal = value, 0.615f);
            string[] labels = { "普通 · 1 次挫折", "挑战 · 3 次挫折", "困难 · 4 次挫折" };
            for (int i = 0; i < 3; i++) { int difficulty = i + 1; float y = 0.425f - i * 0.078f;
                View.Button(overlay, "Imagine difficulty " + difficulty, labels[i] + (imagineDifficulty == difficulty && imagineFailures == 0 ? " · 已选" : ""), () => { imagineDifficulty = difficulty; imagineFailures = 0; ShowImagineSetup(); },
                    0.075f, y, 0.925f, y + 0.07f, Palette.Panel, imagineDifficulty == difficulty ? Palette.Mint : Palette.Text, 28); }
            View.Button(overlay, "Imagine two failures", "两次困难与恢复" + (imagineFailures == 2 ? " · 已选" : ""), () => { imagineDifficulty = 2; imagineFailures = 2; ShowImagineSetup(); },
                0.075f, 0.207f, 0.925f, 0.261f, Palette.Panel, imagineFailures == 2 ? Palette.Mint : Palette.Text, 26);
            if (CanPrepareMaster && !archive.playGuide.imagineFromBoard)
            { CardSpec target = session.Hand.FirstOrDefault(c => c.Id == imagineTargetCardId) ?? session.Hand[1]; imagineTargetCardId = target.Id;
                View.Label(overlay, "Imagination actual target", "实际路径将对照今天的「" + target.Name + "」", 24, Palette.Gold, TextAnchor.MiddleLeft, 0.075f, 0.558f, 0.925f, 0.608f); }
            View.Button(overlay, "Start imagination", "开始预演 · 先看见抵达", () => {
                if (string.IsNullOrWhiteSpace(imagineGoal)) return;
                string goalId = archive.playGuide.imagineFromBoard && archive.playGuide.preparedRun == session?.RunNumber && archive.playGuide.preparedDay == session?.Day ?
                    archive.playGuide.preparedCardId : imagineTargetCardId ?? imagineGoal.Trim().ToLowerInvariant();
                var previous = archive.imaginationComparisons.LastOrDefault(c => c.goal == imagineGoal && (c.actionObserved && !c.actionMatched || c.recoveryObserved && !c.recoveryMatched));
                archive.imagination = ImaginationEngine.Begin(Guid.NewGuid().ToString("N"), goalId ?? imagineGoal.Trim().ToLowerInvariant(), imagineGoal, imagineDifficulty, session?.UsesExpedition == true ? 2 : 1, imagineFailures, AIText.Bound((previous?.difference ?? "") + imaginePersonalSituation, 300));
                Save(); ShowImagineRun();
            }, 0.075f, 0.125f, 0.925f, 0.2f, Palette.Mint, Palette.Ink, 29);
            View.Button(overlay, "Personalize imagination situation", "按这个目标生成困难情境", () => ShowContentStudio(NarrativePurpose.Imagination, imagineGoal, ShowImagineSetup),
                0.075f, 0.501f, 0.925f, 0.554f, Palette.Deep, Palette.Gold, 25);
        }
        private void ShowImagineRun()
        {
            ImagineRun run = archive.imagination; if (run == null) { ShowImagineSetup(); return; }
            MasterPage("Imagination run", run.phase == ImaginePhase.VictoryAnchor ? "看见抵达 · VICTORY ANCHOR" : run.phase == ImaginePhase.Recover ? "失败之后 · 选择恢复" : run.phase == ImaginePhase.Retry ? "再次开始 · FAIL & AGAIN" : "走过过程 · BUILD THE PATH", ReturnFromImagination);
            overlay.Find("Master shade").GetComponent<Image>().color = new Color(.009f,.023f,.04f,.04f);
            world.ShowImaginationScene(run);
            View.Panel(overlay, "Imagine readable copy", new Color(.009f,.023f,.04f,.94f), .045f,.43f,.955f,.56f,20);
            View.Label(overlay, "Imagine goal title", run.goal, 33, Palette.Gold, TextAnchor.MiddleCenter, 0.075f, 0.77f, 0.925f, 0.86f);
            View.Label(overlay, "Imagine beat", run.timeline.Last().text, 30, Palette.Text, TextAnchor.MiddleCenter, 0.075f, 0.438f, 0.925f, 0.552f);
            View.Label(overlay, "Imagine resilience", "挫折 " + run.failures + "/" + run.RequiredFailures + "  ·  RECOVERY " + run.recovered + "  ·  ×" + run.multiplier, 26, Palette.Muted, TextAnchor.MiddleCenter, 0.075f, 0.345f, 0.925f, 0.415f);
            if (run.pathVersion >= 2)
                View.Label(overlay, "Imagine resources", "预演中的状态 · 精力 " + run.energy + " / 专注 " + run.focus + " / 支援 " + run.support,
                    24, Palette.Mint, TextAnchor.MiddleCenter, 0.075f, 0.405f, 0.925f, 0.443f);
            if (run.phase == ImaginePhase.Recover)
            {
                string[] names = { "休息并安排下一步", "请求帮助", "缩小目标", "改变方法" };
                for (int i = 0; i < 4; i++) { RecoveryAction action = (RecoveryAction)i; float x = i % 2 == 0 ? 0.075f : 0.525f, y = 0.235f - (i / 2) * 0.1f;
                    View.Button(overlay, "Recovery action " + i, names[i], () => { ImaginationEngine.Recover(run, action); Save(); ShowImagineRun(); }, x, y, x + 0.4f, y + 0.08f, Palette.Panel, Palette.Mint, 26); }
            }
            else if (run.phase == ImaginePhase.Preparation && run.pathVersion >= 1)
            {
                string[] plans = { "缩成两分钟的一步", "固定时间并设置提醒", "约一个人一起准备" };
                for (int i = 0; i < plans.Length; i++) { PreparationAction action = (PreparationAction)i; float y = 0.245f - i * 0.085f;
                    View.Button(overlay, "Preparation action " + i, plans[i], () => { ImaginationEngine.Prepare(run, action); Save(); ShowImagineRun(); },
                        0.075f, y, 0.925f, y + 0.072f, Palette.Panel, Palette.Mint, 31); }
            }
            else if (run.phase == ImaginePhase.Complete)
                View.Button(overlay, "Keep future memory", "留下 FUTURE MEMORY", () => {
                    if (CanPrepareMaster)
                    {
                        session.AttachImagination(run);
                        string cardId = archive.playGuide.imagineFromBoard && archive.playGuide.preparedRun == session.RunNumber && archive.playGuide.preparedDay == session.Day ? archive.playGuide.preparedCardId : imagineTargetCardId;
                        archive.TrackImagination(run, session, cardId);
                    }
                    archive.KeepImagination(run); archive.imagination = null; PersistMasterAction(); ReturnFromImagination();
                }, 0.075f, 0.15f, 0.925f, 0.235f, Palette.Mint, Palette.Ink, 28);
            else View.Button(overlay, "Continue imagination", run.phase == ImaginePhase.VictoryAnchor ? "时间倒退 · 回到今天" : run.phase == ImaginePhase.Failure ? "失败以后怎么办？" : "经历下一步", () => {
                ImaginationEngine.Continue(run); if (run.phase == ImaginePhase.Complete) archive.KeepImagination(run); Save(); ShowImagineRun();
            }, 0.075f, 0.15f, 0.925f, 0.235f, Palette.Mint, Palette.Ink, 29);
        }
    }
}
