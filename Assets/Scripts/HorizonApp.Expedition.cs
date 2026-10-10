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
        private int selectedKnowledge;
        private float choiceVisibleSince;
        private int futureSelfPage;
        private void ShowLifeRoutes()
        {
            if (!CanPrepareMaster || !session.UsesExpedition) return;
            MasterPage("Life routes", "人生可能性 · 选择一条路", ShowMasterHub);
            View.Label(overlay, "Route tradeoff", "路线不能同时装备。改选需要1专注。\n你的选择会改变之后出现的行动机会。", 29, Palette.Text, TextAnchor.MiddleLeft, 0.075f, 0.75f, 0.925f, 0.85f);
            for (int i = 0; i < LifeRoutes.Ids.Length; i++)
            {
                string id = LifeRoutes.Ids[i]; float y = 0.64f - i * 0.10f;
                View.Button(overlay, "Life route " + id, LifeRoutes.Names[i] + (session.Master.expedition.route == id ? " · 当前路线" : "") + "\n" + LifeRoutes.Tradeoffs[i], () => {
                    if (session.ChooseLifeRoute(id)) PersistMasterAction(); ShowLifeRoutes();
                }, 0.075f, y, 0.925f, y + 0.085f, Palette.Panel, session.Master.expedition.route == id ? Palette.Mint : Palette.Text, 28);
            }
        }
        private void ShowEnvironmentChoices()
        {
            if (!CanPrepareMaster || !session.UsesExpedition) return;
            MasterPage("Environment choices", "改变条件，让动作开始", ShowExecution);
            View.Label(overlay, "Current environment", "今天的环境：" + session.Master.expedition.environment + "\n准备次数：" + session.Master.expedition.preparationsToday + " · 反复准备也会消耗动机窗口", 30, Palette.Text, TextAnchor.MiddleLeft, 0.075f, 0.71f, 0.925f, 0.85f);
            string[] ids = { "remove-reward", "two-minutes", "external-promise", "fixed-place" };
            string[] labels = { "移开即时奖励 · 专注-1，替代奖励-2", "缩成两分钟一步 · 专注-1，摩擦-2", "建立外部承诺 · 专注-1、关系-1，动机+2", "准备固定地点 · 专注-1，摩擦-2" };
            for (int i = 0; i < ids.Length; i++)
            { string id = ids[i]; float y = 0.59f - i * 0.11f;
                View.Button(overlay, "Environment " + id, labels[i], () => { session.AdjustEnvironment(id); PersistMasterAction(); ShowEnvironmentChoices(); },
                    0.075f, y, 0.925f, y + 0.09f, Palette.Panel, Palette.Text, 29); }
            View.Button(overlay, "Understand active thought", "看看阻挡行动的念头", ShowThoughtEncounter, 0.075f, 0.14f, 0.925f, 0.215f, Palette.Deep, Palette.Gold, 28);
        }
        private void ShowThoughtEncounter()
        {
            if (!CanPrepareMaster || !session.UsesExpedition) return;
            ThoughtMonster thought = session.ActiveThought;
            MasterPage("Thought encounter", "思维怪物 · " + thought.name, ShowExecution);
            overlay.Find("Master shade").GetComponent<Image>().color=new Color(.009f,.023f,.04f,.1f);
            world.ShowThoughtScene(thought.id);
            View.Label(overlay, "Thought rationale", "它有一部分合理性\n" + thought.reasonablePart + "\n\n也有代价\n" + thought.tradeoff, 34, Palette.Text, TextAnchor.UpperLeft, 0.08f, 0.48f, 0.92f, 0.84f);
            View.Label(overlay, "Thought perspective", session.WorldviewPerspective(session.Hand[1]), 25, Palette.Muted, TextAnchor.UpperLeft, 0.08f, 0.40f, 0.92f, 0.48f);
            View.Button(overlay, "Thought dialogue", "让这个念头说完，再作决定", () => ShowContentStudio(NarrativePurpose.Npc, thought.reasonablePart + " " + thought.tradeoff, ShowThoughtEncounter),
                0.075f, 0.332f, 0.925f, 0.391f, Palette.Deep, Palette.Gold, 25);
            string[] ids = { "act", "reframe", "delay" };
            string[] labels = { "带着念头，减少一步摩擦 · 专注-1", "重新解释情境，减少诱惑 · 专注-1", "暂时推迟 · 行动势头可能消退" };
            for (int i = 0; i < ids.Length; i++)
            { string id = ids[i]; float y = 0.25f - i * 0.064f;
                Button b = View.Button(overlay, "Thought response " + id, labels[i], () => { if (session.RespondToThought(id)) PersistMasterAction(); CloseMasterPage(); BuildBoard(); },
                    0.075f, y, 0.925f, y + 0.055f, Palette.Panel, Palette.Mint, 26);
                b.interactable = session.Master.expedition.monsterDay != session.Day && (id == "delay" || session.Insight >= 1); }
        }
        private bool ShowMirrorEncounter()
        {
            if (!session.MirrorEncounterPending) return false;
            MasterPage("Mirror encounter", "你又来到相似的节点");
            View.Label(overlay, "Mirror pattern", PatternEngine.Description(session.Master.expedition.mirrorKey) + "\n\n最近的记录被带到这一条人生。\n接下来，亲自走一次不同的路。", 35, Palette.Text, TextAnchor.MiddleLeft, 0.075f, 0.55f, 0.925f, 0.84f);
            string[] ids = { "continue", "recover", "compare" };
            string[] labels = { "保留决定 · 专注-1，再恢复并行动", "先恢复，再选择一次成长行动", "重新比较 · 保留这一次的模式记录" };
            for (int i = 0; i < ids.Length; i++)
            { string id = ids[i]; float y = 0.41f - i * 0.11f;
                Button response = View.Button(overlay, "Mirror response " + id, labels[i], () => { if (session.ResolveMirror(id)) PersistMasterAction(); CloseMasterPage(); BuildBoard(); },
                    0.075f, y, 0.925f, y + 0.09f, Palette.Panel, Palette.Mint, 29);
                response.interactable = id != "continue" || session.Insight >= 1; }
            return true;
        }
        private void ShowExpandedForge()
        {
            var skills = CanPrepareMaster ? session.Master.knowledge : archive.knowledgeSkills;
            if (skills.Count == 0) { ShowMasterUnavailable("知识熔炉", "在新的人生中练习识别情境，再把知识变成动作。"); return; }
            selectedKnowledge = Mathf.Clamp(selectedKnowledge, 0, skills.Count - 1); KnowledgeSkill skill = skills[selectedKnowledge];
            KnowledgeSkill remembered = archive.knowledgeSkills.Find(k => k.id == skill.id);
            if (remembered != null && remembered.stage > skill.stage) skill = remembered;
            MasterPage("Knowledge forge", skill.principle, ShowMasterHub);
            overlay.Find("Master shade").GetComponent<Image>().color=new Color(.009f,.023f,.04f,.1f);
            world.ShowForgeScene(skill);
            View.Label(overlay, "Knowledge rule", "IF · " + skill.condition + "\nTHEN · " + skill.action, 33, Palette.Text, TextAnchor.MiddleLeft, 0.075f, 0.69f, 0.925f, 0.845f);
            string[] stages = { "KNOW · 我知道", "RECOGNIZE · 识别过情境", "SIMULATE · 实际用过一次", "EXECUTE · 亲自报告现实动作", "EXPERIENCE · 回顾后留下经验" };
            for (int i = 0; i < stages.Length; i++)
            { float y = 0.61f - i * 0.072f; View.Label(overlay, "Knowledge stage " + i, ((int)skill.stage >= i ? "● " : "○ ") + stages[i], 28,
                (int)skill.stage >= i ? Palette.Mint : Palette.Muted, TextAnchor.MiddleLeft, 0.09f, y, 0.91f, y + 0.06f); }
            if (CanPrepareMaster && skill.stage == KnowledgeStage.Know)
            {
                bool recognized = session.CanRecognizeSkill(skill.id);
                Button button = View.Button(overlay, "Recognize knowledge", recognized ? "我认出了今天这个条件" : "这个条件尚未出现 · 保留观察", () => {
                    if (session.RecognizeSkill(skill.id)) PersistMasterAction(); ShowExpandedForge(); }, 0.075f, 0.215f, 0.925f, 0.275f, Palette.Panel, Palette.Mint, 27);
                button.interactable = recognized;
            }
            if (skill.stage == KnowledgeStage.Execute && archive.reality.nodes.Any(n => n.id == skill.realityNodeId))
                View.Button(overlay, "Reflect knowledge", "回顾这次真实动作 · 留下经验", () => {
                    KnowledgeForge.Advance(skill, KnowledgeStage.Experience, skill.realityNodeId); Save(); ShowExpandedForge();
                }, 0.075f, 0.215f, 0.925f, 0.275f, Palette.Panel, Palette.Mint, 27);
            View.Button(overlay, "Personalize knowledge action", "把这条知识转换成现实动作", () => ShowContentStudio(NarrativePurpose.Knowledge, skill.condition + " → " + skill.action, ShowExpandedForge),
                0.075f, 0.281f, 0.925f, 0.317f, Palette.Deep, Palette.Gold, 23);
            View.Button(overlay, "Previous knowledge", "上一项", () => { selectedKnowledge = (selectedKnowledge + skills.Count - 1) % skills.Count; ShowExpandedForge(); }, 0.075f, 0.125f, 0.42f, 0.19f, Palette.Deep, Palette.Text, 26);
            View.Label(overlay, "Knowledge page", (selectedKnowledge + 1) + " / " + skills.Count, 25, Palette.Gold, TextAnchor.MiddleCenter, 0.43f, 0.125f, 0.57f, 0.19f);
            View.Button(overlay, "Next knowledge", "下一项", () => { selectedKnowledge = (selectedKnowledge + 1) % skills.Count; ShowExpandedForge(); }, 0.58f, 0.125f, 0.925f, 0.19f, Palette.Deep, Palette.Text, 26);
        }
        private void ShowFutureGallery()
        {
            RunRecord run = archive.runs.LastOrDefault(); var versions = FutureSelfGallery.From(run);
            futureSelfPage = Mathf.Clamp(futureSelfPage, 0, versions.Length - 1); FutureSelfVersion self = versions[futureSelfPage];
            Clear(true); world.ShowStation(1, true); world.SetFutureIdentity(self.appearance, run?.master?.orbitBits ?? 0);
            var memories = run == null ? new System.Collections.Generic.List<MemoryChain>() : run.actions
                .OrderByDescending(a => CausalGraph.Descendants(GameSession.GraphForRun(run), a.nodeId).Count).Take(3)
                .Select(a => new MemoryChain(a, GameSession.GraphForRun(run))).ToList();
            if (memories.Count > 0) world.ShowCausalMemories(memories, true);
            int selected = 0;
            View.Label(root, "Future identity", self.name, 44, Palette.Mint, TextAnchor.MiddleCenter, 0.06f, 0.84f, 0.94f, 0.94f);
            View.Panel(root, "Future reflection backing", new Color(0.025f, 0.055f, 0.08f, 0.90f), 0.055f, 0.185f, 0.945f, 0.38f);
            Text question = View.Label(root, "Future question", self.question + "\n\n我记得：" + self.memory, 32, Palette.Text, TextAnchor.MiddleCenter, 0.075f, 0.19f, 0.925f, 0.38f);
            if (memories.Count > 0)
            {
                Image touch = View.Fill(root, "Approach memory space", new Color(0, 0, 0, 0), 0.035f, 0.405f, 0.965f, 0.825f, true);
                StationSwipe swipe = touch.gameObject.AddComponent<StationSwipe>(); swipe.ReadyAt = Time.unscaledTime + 0.25f;
                swipe.Advanced = () => { selected = (selected + 1) % memories.Count; world.TouchMemory(selected);
                    question.text = memories[selected].Summary + "\n" + memories[selected].Reflection; };
                View.Panel(root, "Memory hint backing", new Color(0.025f, 0.055f, 0.08f, 0.90f), 0.12f, 0.389f, 0.88f, 0.435f);
                View.Label(root, "Station spatial hint", "向前滑动，靠近一段真实回忆", 24, Palette.Text, TextAnchor.MiddleCenter, 0.14f, 0.39f, 0.86f, 0.432f);
            }
            View.Button(root, "Previous future self", "另一位自己", () => { futureSelfPage = (futureSelfPage + 1) % versions.Length; ShowFutureGallery(); }, 0.075f, 0.105f, 0.49f, 0.175f, Palette.Panel, Palette.Gold, 28);
            View.Button(root, "Explore future timeline", "看看这段未来", () => {
                if (run == null) return; var stories = CausalStoryPaths.For(run);
                int index = memories.Count == 0 ? 0 : stories.FindIndex(s => s.nodes.Any(n => n.id == memories[selected].Origin.nodeId));
                ShowCausalStory(run, false, Math.Max(0, index));
            }, 0.51f, 0.105f, 0.925f, 0.175f, Palette.Panel, Palette.Mint, 28);
            View.Button(root, "Future gallery home", "返回", ShowHome, 0.22f, 0.025f, 0.78f, 0.087f, Palette.Panel, Palette.Text, 28);
        }
    }
}
