using System;
using System.Collections.Generic;
using System.Linq;
using Horizon.Game;
using Horizon.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Horizon
{
    public sealed partial class HorizonApp
    {
        private string selectedChapter = "uncertainty";

        private void ShowChapterSelection()
        {
            MasterPage("Story selection", "这次你想穿过什么？");
            View.Label(overlay, "Story introduction", "一个目标，一次真实挫折。\n你的准备、恢复和再次行动，决定这条未来。", 30, Palette.Text,
                TextAnchor.UpperLeft, 0.075f, 0.745f, 0.925f, 0.855f);
            string[] ids = { "uncertainty", "tomorrow", "perfection" };
            string[] titles = { "穿过不确定", "在机会关闭之前", "让不完美的作品出发" };
            string[] details = { "D12 面试 · 独自准备，或带着支持抵达", "D10 邀约过期 · 动机和机会都不会永远等待", "D12 交出作品 · 先交初稿，或继续打磨" };
            for (int i = 0; i < ids.Length; i++)
            {
                string id = ids[i]; float y = 0.56f - i * 0.145f;
                View.Button(overlay, "Select chapter " + id, (selectedChapter == id ? "● " : "○ ") + titles[i] + "\n" + details[i],
                    () => { selectedChapter = id; ShowChapterSelection(); }, 0.07f, y, 0.93f, y + 0.125f,
                    Palette.Panel, selectedChapter == id ? Palette.Mint : Palette.Text, 30);
            }
            bool current = CanPrepareMaster && session.CatalogVersion >= 9 && session.Master.chapter == null && session.Deadline - session.Day >= 7;
            if (current || archive.active == null)
                View.Button(overlay, "Begin story chapter", current ? "让当前人生走向这个目标" : "开始这条故事人生", () => {
                    if (!current) StartNewRun();
                    session.BeginChapter(selectedChapter); PersistMasterAction(); ShowChapterAnchor();
                }, 0.07f, 0.18f, 0.93f, 0.247f, Palette.Mint, Palette.Ink, 31);
            View.Button(overlay, "Practice story chapter", "先试玩 · 保留当前人生", () => StartStoryPractice(selectedChapter),
                0.07f, 0.103f, 0.93f, 0.169f, Palette.Panel, Palette.Gold, 29);
        }

        private void StartStoryPractice(string id)
        {
            if (!IsPractice) StartPractice(); else StartNewRun();
            session.BeginChapter(id); PersistMasterAction(); ShowChapterAnchor();
        }

        private void ShowChapterAnchor()
        {
            Clear(); world.ShowStation(1, true);
            StoryChapter c = session.Master.chapter;
            View.Label(root, "Victory anchor", "VICTORY ANCHOR", 36, Palette.Gold, TextAnchor.MiddleCenter, 0.06f, 0.84f, 0.94f, 0.94f);
            View.Label(root, "Chapter future", c.Goal + "\n先短暂看见想抵达的未来。", 42, Palette.Text, TextAnchor.MiddleCenter, 0.07f, 0.685f, 0.93f, 0.83f);
            View.Fill(root, "Anchor foreground", Palette.Ink, 0, 0, 1, 0.36f);
            View.Label(root, "Build the path", "时间倒回今天。\n接下来，亲自把通往它的路搭出来。", 32, Palette.Text, TextAnchor.MiddleCenter, 0.07f, 0.235f, 0.93f, 0.35f);
            View.Button(root, "Build chapter path", "回到今天 · 开始行动", BuildBoard, 0.1f, 0.135f, 0.9f, 0.215f, Palette.Mint, Palette.Ink, 33);
            View.Button(root, "Imagine chapter recovery", "先预演一次失败以后怎么办", () => {
                BuildBoard(); CardSpec card = session.Hand.FirstOrDefault(x => x.Kind == CardKind.Growth);
                if (card != null) PrepareCardImagination(card); else BeginBoardImagination();
            }, 0.1f, 0.055f, 0.9f, 0.12f, Palette.Panel, Palette.Gold, 28);
        }

        private bool ShowChapterMoment()
        {
            StoryChapter c = session?.Master?.chapter; if (c == null) return false;
            if (session.ChapterNeedsChoice) { ShowChapterEncounter(); return true; }
            string key = session.RunNumber + ":" + c.failureNode;
            if (!c.setbackOccurred || c.outcome != ChapterOutcome.InProgress || archive.chapterSeenSetbacks.Contains(key)) return false;
            Action acknowledge = () => { archive.chapterSeenSetbacks.Add(key); Save(); CloseMasterPage(); BuildBoard(); };
            MasterPage("Chapter setback", "第一次没有做好", acknowledge);
            CausalNode node = session.CausalNodes.Find(n => n.id == c.failureNode);
            View.Label(overlay, "Chapter setback cause", "作品收到反馈：还需要修改。\n" + PlayExperience.NowLabel(node.effect), 38, Palette.Coral,
                TextAnchor.MiddleLeft, 0.075f, 0.64f, 0.925f, 0.82f);
            View.Label(overlay, "Chapter recovery instruction", "这条人生还没有结束。\n\n先选一张恢复或支持牌。\n然后，再做一次成长行动。\n\n这两步会留下真实的恢复与再战节点。", 34, Palette.Text,
                TextAnchor.UpperLeft, 0.075f, 0.29f, 0.925f, 0.615f);
            View.Button(overlay, "Recover chapter setback", "回到行动 · 给自己一次恢复", acknowledge, 0.075f, 0.145f, 0.925f, 0.225f, Palette.Mint, Palette.Ink, 31);
            return true;
        }

        private void ShowChapterEncounter()
        {
            StoryChapter c = session.Master.chapter; bool preparation = session.ChapterNeedsPreparation;
            MasterPage("Chapter encounter", preparation ? "念头出现：再准备一下？" : "面对「" + c.BossName + "」", () => { CloseMasterPage(); BuildBoard(); });
            View.Label(overlay, "Chapter encounter goal", c.Goal, 36, Palette.Gold, TextAnchor.MiddleLeft, 0.075f, 0.765f, 0.925f, 0.85f);
            View.Label(overlay, "Chapter encounter explanation", preparation ? "现在选一种准备方法。\n它会改变随后遇到挫折时的真实代价。" :
                "机会就在眼前。\n带着实际积累和恢复，再决定怎样抵达。", 31, Palette.Text, TextAnchor.UpperLeft, 0.075f, 0.625f, 0.925f, 0.75f);
            string[] routes = preparation ? new[] { "step", "help", "delay" } : c.id == "perfection" ? new[] { "draft", "polish", "leave" } : new[] { "act", "help", "leave" };
            string[] labels = preparation ? new[] { "只准备下一步", "预约朋友的支持", "继续比较，明天再说" } : c.id == "perfection" ?
                new[] { "交出可以用的初稿", "交出充分打磨的作品", "放下这次机会" } : new[] { "带着作品，独自出发", "带着支持一起出发", "放下这次机会" };
            for (int i = 0; i < routes.Length; i++)
            {
                string route = routes[i]; bool can = preparation ? session.CanPrepareChapter(route) : session.CanResolveChapter(route);
                string detail = preparation ? route == "step" ? "Insight -1 · 挫折时精力-1、心情-1" : route == "help" ?
                    "金钱-1、关系+1 · 挫折时精力-1" : "现在无消耗 · 挫折时精力-2、心情-2" : session.ChapterRouteCondition(route);
                float y = 0.4f - i * 0.125f;
                UnityEngine.UI.Button button = View.Button(overlay, "Chapter route " + route, labels[i] + "\n" + detail, () => {
                    if (!(preparation ? session.PrepareChapter(route) : session.ResolveChapter(route))) return;
                    PersistMasterAction(); CloseMasterPage(); BuildBoard();
                }, 0.07f, y, 0.93f, y + 0.108f, Palette.Panel, can ? Palette.Mint : Palette.Muted, 28);
                button.interactable = can;
            }
            View.Label(overlay, "Chapter actual resources", "精力 " + session.Energy + " · 能力 " + session.Ability + " · 关系 " + session.Relation + " · 金钱 " + session.Money,
                26, Palette.Text, TextAnchor.MiddleCenter, 0.07f, 0.1f, 0.93f, 0.145f);
        }

        private void ShowChapterProgress()
        {
            StoryChapter c = session.Master.chapter;
            MasterPage("Chapter progress", c.Title);
            View.Label(overlay, "Chapter goal", c.Goal, 38, Palette.Gold, TextAnchor.MiddleLeft, 0.075f, 0.76f, 0.925f, 0.85f);
            string[] labels = { "选择准备方法", "经历第一次反馈", "实际恢复或求助", "重新开始一次成长行动" };
            string[] ids = { c.preparationNode, c.failureNode, c.recoveryNode, c.returnNode };
            for (int i = 0; i < ids.Length; i++)
                View.Label(overlay, "Chapter milestone " + i, (string.IsNullOrEmpty(ids[i]) ? "○ " : "● ") + labels[i], 34,
                    string.IsNullOrEmpty(ids[i]) ? Palette.Muted : Palette.Mint, TextAnchor.MiddleLeft, 0.09f, 0.65f - i * 0.095f, 0.91f, 0.725f - i * 0.095f);
            View.Label(overlay, "Chapter next step", session.ChapterNextStep, 31, Palette.Text, TextAnchor.UpperLeft, 0.075f, 0.15f, 0.925f, 0.31f);
            if (session.ChapterNeedsChoice)
                View.Button(overlay, "Resume chapter encounter", "处理眼前的选择", ShowChapterEncounter, 0.075f, 0.105f, 0.925f, 0.16f, Palette.Mint, Palette.Ink, 28);
        }

        private void RenderChapterFinale(RunRecord run)
        {
            Clear(); world.ShowStation(2, true);
            StoryChapter c = run.master.chapter; bool arrived = c.outcome == ChapterOutcome.Arrived;
            View.Label(root, "Chapter verdict", arrived ? "LIVE THE FUTURE" : "这次，停在了这里", 44, arrived ? Palette.Mint : Palette.Coral, TextAnchor.MiddleCenter, 0.06f, 0.84f, 0.94f, 0.94f);
            View.Label(root, "Chapter result", c.Goal + "\n" + (arrived ? "困难之后，你实际恢复过，又重新出发。" : "机会关闭了。来路会留下，下一次可以不同。"), 35, Palette.Text,
                TextAnchor.MiddleCenter, 0.07f, 0.68f, 0.93f, 0.825f);
            View.Fill(root, "Chapter finale foreground", Palette.Ink, 0, 0, 1, 0.47f);
            View.Label(root, "Chapter resilience", "准备 → 反馈 → 恢复 → 再行动\n" + (arrived ? "这些实际发生的节点，连接到了目标。" : "看看哪一步中断，再试一次。"), 32, Palette.Text,
                TextAnchor.MiddleCenter, 0.07f, 0.325f, 0.93f, 0.45f);
            View.Button(root, "Inspect timeline", "看看这条未来怎样形成", () => ShowCausalNetwork(run, false), 0.1f, 0.225f, 0.9f, 0.305f, Palette.Mint, Palette.Ink, 32);
            View.Button(root, "Try another timeline", "同一个困难 · 换一种选择", () => {
                string id = c.id; StartNewRun(); session.BeginChapter(id); PersistMasterAction(); ShowChapterAnchor();
            }, 0.1f, 0.135f, 0.9f, 0.21f, Palette.Panel, Palette.Gold, 30);
            View.Button(root, "Chapter finale home", IsPractice ? "退出试玩 · 返回原人生" : "回到未来站", () => {
                if (IsPractice) ExitPractice(); else ShowHome();
            }, 0.1f, 0.045f, 0.9f, 0.12f, Palette.Panel, Palette.Text, 29);
        }
    }
}
