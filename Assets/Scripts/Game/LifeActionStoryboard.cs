using System;
using System.Linq;

namespace Horizon.Game
{
    public enum LifeActionTheme { None, Walk, Rest, Read, Work, Connect, Earn, Spend, Prepare, Explore, Practice }
    public enum LifeActionShot { Establish, Action, Arrive, Suspense, Reveal, Overview }

    // Small everyday scenes are interpretations of the recorded action, not
    // invented causes, guaranteed future outcomes or a second reward engine.
    public sealed class LifeActionStoryboard
    {
        public readonly LifeActionTheme Theme;
        public readonly string Action;
        public readonly float ActionEnd, Impact, Reveal, Settlement, Duration;
        public bool Enabled => Theme!=LifeActionTheme.None;
        public LifeActionStoryboard(RewardPlan plan)
        {
            Action=plan.Event.receipt?.action ?? "";
            Theme=ThemeFor(plan.Event);
            Impact=plan.Cues.First(c=>!c.NodeHit && c.Phase==CinematicPhase.Impact).Time;
            Reveal=plan.Cues.First(c=>!c.NodeHit && c.Phase==CinematicPhase.Multiplication).Time;
            Settlement=plan.Cues.First(c=>!c.NodeHit && c.Phase==CinematicPhase.Settlement).Time;
            Duration=plan.Duration; ActionEnd=Math.Min(7.2f,Impact*.60f);
        }
        public static LifeActionTheme ThemeFor(DomainEvent e)
        {
            if(e==null || e.receipt==null || string.IsNullOrWhiteSpace(e.receipt.action) ||
                e.kind!=DomainEventKind.ActionTaken && e.kind!=DomainEventKind.TimeEcho ||
                e.kind==DomainEventKind.TimeEcho && e.receipt.sourceDay<=0) return LifeActionTheme.None;
            return ThemeForAction(e.receipt.action);
        }
        public static LifeActionTheme ThemeForAction(string s)
        {
            if(string.IsNullOrWhiteSpace(s)) return LifeActionTheme.None;
            if(Has(s,"暂未","尚未","来路")) return LifeActionTheme.None;
            if(Has(s,"走走","散步","公园","跑步","慢跑","运动","出门")) return LifeActionTheme.Walk;
            if(Has(s,"休息","睡","恢复","十分钟","照顾自己")) return LifeActionTheme.Rest;
            if(Has(s,"书","阅读","学习","知识")) return LifeActionTheme.Read;
            if(Has(s,"朋友","关系","吃饭","消息","见面","求助","陪伴","一起")) return LifeActionTheme.Connect;
            if(Has(s,"赚钱","收入","接单","兼职")) return LifeActionTheme.Earn;
            if(Has(s,"买","刷","快乐","安慰","消费")) return LifeActionTheme.Spend;
            if(Has(s,"计划","准备","预约","承诺","决定")) return LifeActionTheme.Prepare;
            if(Has(s,"探索","尝试","机会")) return LifeActionTheme.Explore;
            if(Has(s,"练习","复盘","失败","再来","重试")) return LifeActionTheme.Practice;
            return LifeActionTheme.Work;
        }
        private static bool Has(string s,params string[] words) => words.Any(s.Contains);
        public LifeActionFrame Sample(float seconds,bool reduced=false)
        {
            float t=Math.Max(0,Math.Min(Duration,seconds));
            float p=Math.Max(0,Math.Min(1,t/Math.Max(.01f,ActionEnd)));
            LifeActionShot shot=t<ActionEnd*.32f?LifeActionShot.Establish:t<ActionEnd*.72f?LifeActionShot.Action:
                t<ActionEnd?LifeActionShot.Arrive:t<Reveal?LifeActionShot.Suspense:t<Settlement?LifeActionShot.Reveal:LifeActionShot.Overview;
            float pressure=t>=ActionEnd && t<Impact?(t-ActionEnd)/Math.Max(.01f,Impact-ActionEnd):0;
            pressure=pressure*pressure*(3-2*pressure);
            return new LifeActionFrame(shot,p,reduced?0:pressure,Copy(shot,p));
        }
        private string Copy(LifeActionShot shot,float progress)
        {
            if(shot==LifeActionShot.Suspense) return "行动已经留下痕迹。接着，看清它带来的变化。";
            if(shot==LifeActionShot.Overview) return "看完这一刻，再决定下一步。";
            if(Theme==LifeActionTheme.Walk) return progress<.32f?"推开家门，先走出去。":progress<.72f?"沿着小路，走进公园。":"停下来，给自己一次呼吸。";
            if(Theme==LifeActionTheme.Rest) return "把手里的事放下，给自己留一点恢复的时间。";
            if(Theme==LifeActionTheme.Read) return "翻开书，把注意力放在眼前的一页。";
            if(Theme==LifeActionTheme.Connect) return Action.Contains("消息")?"写下一条消息，先把联系发出去。":"向另一个人靠近，让这次联系真正发生。";
            if(Theme==LifeActionTheme.Prepare) return "把计划写下来，为下一步留好入口。";
            if(Theme==LifeActionTheme.Spend) return "把注意力交给眼前的选择。它也会留下后果。";
            if(Theme==LifeActionTheme.Earn) return "完成眼前的一份工作，记录实际的付出。";
            if(Theme==LifeActionTheme.Explore) return "走向一条新的路，看看眼前的可能。";
            if(Theme==LifeActionTheme.Practice) return "先做一次，再看哪里需要调整。";
            return "开始「"+PresentationCopy.Chinese(Action)+"」，把想法变成一个具体动作。";
        }
    }
    public readonly struct LifeActionFrame
    {
        public readonly LifeActionShot Shot;
        public readonly float Progress,Pressure;
        public readonly string Copy;
        public LifeActionFrame(LifeActionShot shot,float progress,float pressure,string copy)
        { Shot=shot; Progress=progress; Pressure=pressure; Copy=copy; }
    }
}
