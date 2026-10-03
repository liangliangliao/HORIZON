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
        private void ShowForge()
        {
            MasterPage("Knowledge forge", "K N O W L E D G E  F O R G E", ShowMasterHub);
            KnowledgeSkill k = CanPrepareMaster ? session.Master.knowledge[0] : archive.knowledgeSkills.FirstOrDefault();
            KnowledgeSkill remembered = archive.knowledgeSkills.FirstOrDefault(); if (remembered != null && (k == null || remembered.stage > k.stage)) k = remembered;
            string[] stages = { "KNOW · 我知道", "RECOGNIZE · 识别情境", "SIMULATE · 游戏中用过", "EXECUTE · 现实中行动", "EXPERIENCE · 形成经验" };
            for (int i = 0; i < stages.Length; i++)
            { float y = 0.73f - i * 0.095f;
                View.Label(overlay, "Knowledge stage " + i, (k != null && (int)k.stage >= i ? "● " : "○ ") + stages[i], 31,
                    k != null && (int)k.stage >= i ? Palette.Mint : Palette.Muted, TextAnchor.MiddleLeft, 0.09f, y, 0.91f, y + 0.075f); }
            View.Label(overlay, "If then skill", "IF · 遇到不确定，想退出时\nTHEN · 恢复、求助或缩小一步，然后继续", 29, Palette.Text, TextAnchor.MiddleLeft, 0.09f, 0.21f, 0.91f, 0.33f);
            if (CanPrepareMaster && session.Master.knowledge[0].stage == KnowledgeStage.Know)
                View.Button(overlay, "Recognize knowledge", "我能识别这个节点", () => { session.RecognizeKnowledge(); PersistMasterAction(); ShowForge(); }, 0.075f, 0.12f, 0.925f, 0.19f, Palette.Mint, Palette.Ink, 28);
            else if (k != null && k.stage == KnowledgeStage.Execute)
                View.Button(overlay, "Reflect knowledge", "回想这次行动，把它留下为经验", () => { KnowledgeForge.Advance(k, KnowledgeStage.Experience, "reflection:" + k.realityNodeId); Save(); ShowForge(); }, 0.075f, 0.12f, 0.925f, 0.19f, Palette.Mint, Palette.Ink, 26);
        }
        private void ShowWorldviews()
        {
            MasterPage("Worldview deck", "世界观 · 认知工具", ShowMasterHub);
            View.Label(overlay, "Worldview autonomy", "思想提供视角。最后的选择仍然属于你。", 29, Palette.Text, TextAnchor.MiddleLeft, 0.075f, 0.77f, 0.925f, 0.84f);
            for (int i = 0; i < WorldviewDeck.Names.Length; i++)
            { int index = i; float x = i % 2 == 0 ? 0.075f : 0.525f, y = 0.65f - (i / 2) * 0.12f;
                string name = WorldviewDeck.Names[i];
                View.Button(overlay, "Worldview " + i, name + (archive.worldview.Contains(name) ? " · 已加入" : ""), () => {
                    MasterPage("Worldview perspective", name, ShowWorldviews);
                    View.Label(overlay, "Perspective", WorldviewDeck.Perspectives[index], 36, Palette.Text, TextAnchor.MiddleCenter, 0.1f, 0.4f, 0.9f, 0.74f);
                    View.Button(overlay, "Worldview equip", archive.worldview.Contains(name) ? "从卡组移出" : "加入我的世界观卡组", () => {
                        if (archive.worldview.Contains(name)) archive.worldview.Remove(name); else archive.worldview.Add(name); Save(); ShowWorldviews();
                    }, 0.075f, 0.22f, 0.925f, 0.3f, Palette.Mint, Palette.Ink, 28);
                }, x, y, x + 0.4f, y + 0.09f, Palette.Panel, Palette.Text, 26); }
        }
        private void ShowMe()
        {
            MasterPage("Recent behavior model", "近期模式 · HORIZON ME", ShowMasterHub);
            View.Label(overlay, "Recent model", archive.me.Summary, 29, Palette.Text, TextAnchor.UpperLeft, 0.075f, 0.7f, 0.925f, 0.855f);
            if (session != null && session.UsesMasterRules)
            {
                string[] names = { "精力", "心情", "专注", "金钱", "关系", "能力" };
                for (int i = 0; i < 6; i++)
                { ResourceTrend t = ResourceMath.Trend(session.Master.resources, i); float y = 0.58f - i * 0.068f;
                    View.Label(overlay, "Resource trend " + i, names[i] + "  " + t.current + "   趋势 " + (t.trend > 0 ? "+" : "") + t.trend.ToString("0.0") + "   波动 " + t.volatility.ToString("0.0"), 27,
                        Palette.Text, TextAnchor.MiddleLeft, 0.075f, y, 0.925f, y + 0.06f); }
                View.Label(overlay, "Model context", "记录近期选择与情境，不是永久人格标签。\n低资源会缩小选择范围，恢复能重新打开空间。", 25, Palette.Muted, TextAnchor.MiddleLeft, 0.075f, 0.12f, 0.925f, 0.195f);
            }
        }
        private MasterRunState CurrentMaster
        { get { if (session != null && session.UsesMasterRules && (archive.active?.runNumber == session.RunNumber || archive.runs.Contains(session.CompletedRun))) return session.Master;
            RunRecord latest = archive.runs.LastOrDefault(); return latest?.catalogVersion >= 7 ? latest.master : null; } }
        private void ShowOrbit()
        {
            MasterRunState state = CurrentMaster;
            if (state == null) { ShowMasterUnavailable("未来轨道", "在新的人生中，成长、关系与恢复会逐渐点亮八个未来方向。"); return; }
            MasterPage("Future orbit", "F U T U R E  O R B I T", ShowMasterHub);
            var orbit = View.Rect(overlay, "Eight future orbit nodes", 0.12f, 0.32f, 0.88f, 0.81f).gameObject.AddComponent<FutureOrbitGraphic>();
            orbit.Bits = state.orbitBits; orbit.color = Palette.Mint; orbit.raycastTarget = false;
            for (int i = 0; i < 8; i++)
            { float a = (90 - i * 45) * Mathf.Deg2Rad; float x = 0.5f + Mathf.Cos(a) * 0.335f, y = 0.565f + Mathf.Sin(a) * 0.215f;
                View.Label(overlay, "Orbit label " + i, MasterSpecification.OrbitNames[i], 25, (state.orbitBits & (1 << i)) != 0 ? Palette.Mint : Palette.Muted,
                    TextAnchor.MiddleCenter, x - 0.07f, y - 0.028f, x + 0.07f, y + 0.028f); }
            View.Label(overlay, "Future self identity", state.allLinked ? "ALL LINKED\n未来自己 · 已连成星图" : "未来自己\n" + (session != null && session.Master?.resilienceChain > 1 ? "失败但继续的我" : "正在形成的我"), 29,
                Palette.Gold, TextAnchor.MiddleCenter, 0.32f, 0.5f, 0.68f, 0.64f);
            View.Label(overlay, "Causal reservoir", "CAUSAL RESERVOIR  " + state.reservoir + "/6\n积累需要能力、状态、关系与机会同时成熟。", 28, Palette.Text, TextAnchor.MiddleCenter, 0.075f, 0.215f, 0.925f, 0.315f);
            View.Label(overlay, "Overdrive state", "OVERDRIVE  " + state.overdriveEnergy + "%  ·  Resilience ×" + state.resilienceChain,
                26, Palette.Mint, TextAnchor.MiddleCenter, 0.075f, 0.12f, 0.925f, 0.19f);
        }
        private void ShowReality()
        {
            MasterPage("Reality bridge", "R E A L I T Y  B R I D G E", ShowMasterHub);
            DateTime now = DateTime.Now;
            RealityQuest quest = archive.reality.quests.FirstOrDefault(q => q.localDate == now.ToString("yyyy-MM-dd"));
            FutureMemory memory = archive.futureMemories.OrderByDescending(m => !string.IsNullOrEmpty(m.simulationNodeId)).ThenByDescending(m => m.value).FirstOrDefault();
            View.Label(overlay, "Reality intention", "每天最多一个现实小动作。\n由你亲自完成并确认，它会留下不可重复领取的现实节点。", 30, Palette.Text, TextAnchor.UpperLeft, 0.075f, 0.69f, 0.925f, 0.85f);
            string title = quest?.title ?? (memory?.actionKey == "AskHelp" ? "向一个人发出求助消息" : memory?.actionKey == "ChangeMethod" ? "用另一种方法尝试一个小步骤" : memory?.actionKey == "LowerTarget" ? "把一件事缩成两分钟的一步并开始" : "站起来，给自己五分钟恢复");
            View.Label(overlay, "Reality quest title", title, 39, Palette.Mint, TextAnchor.MiddleCenter, 0.1f, 0.5f, 0.9f, 0.67f);
            if (quest == null)
                View.Button(overlay, "Accept reality quest", "选择今天的现实行动", () => {
                    archive.reality.Offer(DateTime.Now, memory?.goalId ?? "small-recovery", title, memory?.id); Save(); ShowReality();
                }, 0.075f, 0.365f, 0.925f, 0.445f, Palette.Mint, Palette.Ink, 29);
            else if (!quest.completed)
                View.Button(overlay, "Complete reality quest", "我已经在现实中完成", () => {
                    if (!archive.reality.Complete(quest.id, DateTime.Now, archive.futureMemories)) return;
                    FutureMemory m = archive.futureMemories.Find(x => x.id == quest.memoryId);
                    foreach (KnowledgeSkill k in archive.knowledgeSkills.Where(k => k.stage == KnowledgeStage.Simulate && k.simulationNodeId == m?.simulationNodeId && k.simulationRun == m?.simulationRun))
                        KnowledgeForge.Advance(k, KnowledgeStage.Execute, quest.id);
                    Save(); DomainEvent e = archive.reality.events.Last(); PlayMasterSpectacle(e, ShowReality);
                }, 0.075f, 0.365f, 0.925f, 0.445f, Palette.Mint, Palette.Ink, 29);
            else View.Label(overlay, "Reality completed", "REALITY NODE · 今天已经留下", 31, Palette.Gold, TextAnchor.MiddleCenter, 0.075f, 0.365f, 0.925f, 0.445f);
            View.Label(overlay, "Reality memory", memory == null ? "先在想象中练习失败后的下一步。" : "Future Memory\n" + memory.text,
                27, Palette.Muted, TextAnchor.MiddleLeft, 0.075f, 0.24f, 0.925f, 0.345f);
            View.Button(overlay, "Reality constellation", "现实星座  ·  " + archive.reality.quests.Count(q => q.completed) + " 个真实行动", ShowConstellation,
                0.075f, 0.125f, 0.925f, 0.205f, Palette.Panel, Palette.Text, 29);
        }
        private void ShowConstellation()
        {
            MasterPage("Reality constellation", "R E A L I T Y  C O N S T E L L A T I O N", ShowReality);
            var nodes = archive.reality.nodes.TakeLastPortable(12).ToList();
            for (int i = 0; i < nodes.Count; i++)
            {
                float x = 0.075f + i % 3 * 0.3f, y = 0.65f - i / 3 * 0.13f;
                View.Panel(overlay, "Reality star", nodes[i].kind == CausalNodeKind.Reality ? Palette.Mint : Palette.Gold, x + 0.12f, y + 0.07f, x + 0.145f, y + 0.09f, 12);
                View.Label(overlay, "Constellation node " + i, nodes[i].label, 23, Palette.Text, TextAnchor.UpperCenter, x, y, x + 0.26f, y + 0.06f);
                foreach (string parent in nodes[i].parents)
                {
                    int p = nodes.FindIndex(n => n.id == parent); if (p < 0) continue;
                    var thread = View.Rect(overlay, "Convergence link", 0, 0, 1, 1).gameObject.AddComponent<TimeThreadGraphic>();
                    thread.From = new Vector2(0.2075f + p % 3 * 0.3f, 0.73f - p / 3 * 0.13f); thread.To = new Vector2(x + 0.1325f, y + 0.08f);
                    thread.color = new Color(Palette.Mint.r, Palette.Mint.g, Palette.Mint.b, 0.3f); thread.Thickness = 3; thread.raycastTarget = false;
                }
            }
            View.Label(overlay, "Reality convergence count", "IMAGINATION → SIMULATION → REALITY\n" + archive.reality.convergenceMemories.Count + " 次路径重合", 27, Palette.Mint, TextAnchor.MiddleCenter, 0.075f, 0.12f, 0.925f, 0.23f);
        }
        private void ShowModes()
        {
            MasterPage("Life modes", "下一条人生", ShowMasterHub);
            if (archive.active != null)
            {
                View.Label(overlay, "Mode active life", "当前人生正在进行。先走到未来站与截止日，再选择下一条人生。", 34, Palette.Text, TextAnchor.MiddleCenter, 0.1f, 0.4f, 0.9f, 0.74f);
                View.Button(overlay, "Resume before new mode", "继续当前人生", () => { CloseMasterPage(); ContinueRun(); }, 0.075f, 0.365f, 0.925f, 0.445f, Palette.Mint, Palette.Ink, 29);
            }
            else for (int i = 0; i < ModeNames.Length; i++)
            { RunMode mode = (RunMode)i; float x = i % 2 == 0 ? 0.075f : 0.525f, y = 0.7f - (i / 2) * 0.12f;
                View.Button(overlay, "Run mode " + i, ModeNames[i] + "\n" + ModeDescriptions[i], () => {
                    requestedMasterMode = mode; requestedLifeLength = mode == RunMode.ThirtyDays || mode == RunMode.LongRun ? 30 : 12;
                    CloseMasterPage(); StartNewRun(); if (mode == RunMode.ImaginationRun) ShowImagineSetup();
                }, x, y, x + 0.4f, y + 0.095f, Palette.Panel, Palette.Text, 22); }
            View.Button(overlay, "Local parallel comparison", "相同起点 · 本地平行重演", ShowParallelComparison, 0.075f, 0.15f, 0.925f, 0.23f, Palette.Deep, Palette.Gold, 27);
            View.Button(overlay, "Future messages", "Future Message · 留一段经验", ShowFutureMessages, 0.075f, 0.24f, 0.925f, 0.31f, Palette.Deep, Palette.Text, 25);
        }
        private void ShowParallelComparison()
        {
            RunRecord run = archive.runs.LastOrDefault();
            if (run == null) { ShowMasterUnavailable("平行人生", "完成一条人生以后，可以从相同起点选择另一条路线。"); return; }
            for (int day = 1; day <= GameSession.RunLength(run); day++)
            {
                CardSpec alternate = GameSession.AlternativesForDay(run, day).FirstOrDefault(c => c.Id != run.actions[day - 1].cardId);
                if (alternate == null) continue;
                RunRecord branch = GameSession.ReplayAlternative(run, day, alternate.Id); if (branch == null) continue;
                ParallelComparison comparison = ParallelLives.Compare(run, branch);
                MasterPage("Parallel lives comparison", "P A R A L L E L  L I V E S", ShowModes);
                View.Label(overlay, "Parallel divergence", "相同起点 · D" + comparison.divergenceDay + "\n\n" + comparison.firstChoice + " → " + comparison.firstGates + "/3 门\n\n" + comparison.secondChoice + " → " + comparison.secondGates + "/3 门", 35, Palette.Text, TextAnchor.MiddleCenter, 0.075f, 0.39f, 0.925f, 0.8f);
                View.Button(overlay, "Plan parallel life", "亲自选择这条平行人生", () => { CloseMasterPage(); ShowBranchPlanner(run, day, true); }, 0.075f, 0.22f, 0.925f, 0.3f, Palette.Mint, Palette.Ink, 28); return;
            }
        }
        private void ShowFutureMessages()
        {
            MasterPage("Future messages", "F U T U R E  M E S S A G E", ShowModes);
            string text = "失败后的第二天，我先恢复，再重新开始。";
            MasterInput("Future message input", text, value => text = value, 0.66f, 200);
            View.Label(overlay, "Message sharing", "把一段经验留成时间回声。复制后，由你选择分享给谁。", 29, Palette.Muted, TextAnchor.UpperLeft, 0.075f, 0.52f, 0.925f, 0.635f);
            View.Button(overlay, "Copy future message", "留下并复制经验", () => {
                RunRecord run = archive.runs.LastOrDefault(); if (run == null || string.IsNullOrWhiteSpace(text)) return;
                FutureMessage message = ParallelLives.Message(run, text); archive.futureMessages.Add(message);
                GUIUtility.systemCopyBuffer = JsonUtility.ToJson(message); Save(); ShowFutureMessages();
            }, 0.075f, 0.41f, 0.925f, 0.485f, Palette.Mint, Palette.Ink, 28);
            Text status = View.Label(overlay, "Message import status", "本地已留下 " + archive.futureMessages.Count + " 段经验。", 27, Palette.Text, TextAnchor.MiddleLeft, 0.075f, 0.22f, 0.925f, 0.32f);
            View.Button(overlay, "Import future message", "从剪贴板接住一段回声", () => {
                string data = GUIUtility.systemCopyBuffer;
                if (string.IsNullOrEmpty(data) || data.Length > 8192) { status.text = "没有可读取的经验。"; return; }
                try {
                    FutureMessage m = JsonUtility.FromJson<FutureMessage>(data);
                    if (m == null || string.IsNullOrWhiteSpace(m.id) || m.id.Length > 100 || string.IsNullOrWhiteSpace(m.text) || m.text.Length > 200 || m.day < 1 || m.day > 30 || archive.futureMessages.Any(x => x.id == m.id)) { status.text = "这段经验已接住，或格式不完整。"; return; }
                    archive.futureMessages.Add(m); Save(); status.text = m.text;
                } catch (Exception) { status.text = "这段内容不是 HORIZON 的经验回声。"; }
            }, 0.075f, 0.125f, 0.925f, 0.2f, Palette.Panel, Palette.Text, 27);
        }
    }
}
