using System;
using Horizon.Game;
using UnityEngine;
using UnityEngine.UI;

namespace Horizon.UI
{
    // Presentation subdivisions deliberately do not add states to the saved game FSM.
    public enum ImaginationShot { VictoryAnchor, Rewind, BuildThePath, Action, Difficulty, Failure, Recovery, Again, ChangedStrategy, FinalPush, ImaginedVictory }
    public struct ImaginationFrame
    {
        public ImaginationShot Shot;
        public float Progress;
        public string Caption;
    }
    public static class ImaginationStoryboard
    {
        public static ImaginationFrame Sample(ImagineRun run, float elapsed)
        {
            ImaginationShot shot; float start=0, duration=2.8f;
            switch(run.phase)
            {
                case ImaginePhase.VictoryAnchor: shot=ImaginationShot.VictoryAnchor; break;
                case ImaginePhase.Preparation:
                    shot=elapsed<2.4f?ImaginationShot.Rewind:ImaginationShot.BuildThePath;
                    start=elapsed<2.4f?0:2.4f; duration=2.4f; break;
                case ImaginePhase.Effort:
                    shot=elapsed<3?ImaginationShot.Action:ImaginationShot.Difficulty;
                    start=elapsed<3?0:3; duration=elapsed<3?3:2; break;
                case ImaginePhase.Failure: shot=ImaginationShot.Failure; break;
                case ImaginePhase.Recover: shot=ImaginationShot.Recovery; duration=4; break;
                case ImaginePhase.Retry: shot=ImaginationShot.Again; break;
                case ImaginePhase.Adjust: shot=ImaginationShot.ChangedStrategy; duration=3.6f; break;
                case ImaginePhase.Victory:
                    shot=elapsed<3?ImaginationShot.FinalPush:ImaginationShot.ImaginedVictory;
                    start=elapsed<3?0:3; duration=3; break;
                default: shot=ImaginationShot.ImaginedVictory; break;
            }
            return new ImaginationFrame { Shot=shot,Progress=Mathf.SmoothStep(0,1,Mathf.Clamp01((elapsed-start)/duration)),Caption=Caption(shot,run) };
        }
        public static string Caption(ImaginationShot shot,ImagineRun run)
        {
            switch(shot)
            {
                case ImaginationShot.VictoryAnchor:return "VICTORY ANCHOR · 这是一个可能抵达的未来。";
                case ImaginationShot.Rewind:return "REWIND · 现在，让我们看看你是怎样走到那里的。";
                case ImaginationShot.BuildThePath:return "BUILD THE PATH · 准备材料 → 留出时间 → 开始最小一步。";
                case ImaginationShot.Action:return "ACTION · "+(run.goalFamily=="health"?"穿好鞋，走出门，开始练习。":run.goalFamily=="relationship"?"拿起手机，发出邀请，等待回应。":run.goalFamily=="career"?"拿起作品，修改，交出这一次尝试。":"打开资料，动手练习，检验一次结果。");
                case ImaginationShot.Difficulty:return "DIFFICULTY · "+(!string.IsNullOrEmpty(run.adaptation)?run.adaptation:run.preparationKey=="WithSupport"?"约好的人暂时没空。":run.preparationKey=="FixedTime"?"提醒响起，疲劳和诱惑也在。":"下一步比预想困难，先停下来观察。");
                case ImaginationShot.Failure:return "FAILURE · 这一次失败了。但模拟没有结束。";
                case ImaginationShot.Recovery:return "RECOVERY · 喘息、观察、整理下一步；选择一个真实恢复动作。";
                case ImaginationShot.Again:return "AGAIN · 抬头，再次前行。"+(run.recovered>0?" ×"+run.multiplier:"");
                case ImaginationShot.ChangedStrategy:return "CHANGED STRATEGY · "+(run.memories.Count>0?run.memories[run.memories.Count-1].text:"保留有效部分，缩小下一步。");
                case ImaginationShot.FinalPush:return "FINAL PUSH · 已恢复的精力、专注、支援和未来记忆一起抵达。";
                default:return "IMAGINED VICTORY · 你已经在想象中完整走过一次。";
            }
        }
    }
    public sealed class ImaginationCaption : MonoBehaviour
    {
        public HorizonWorld3D World;
        public Text Label;
        public string Original;
        private ImaginationShot? last;
        private void Update()
        {
            if(World==null || Label==null || !World.ImaginationPlaying || last==World.CurrentImaginationFrame.Shot) return;
            last=World.CurrentImaginationFrame.Shot;
            Label.text=World.CurrentImaginationFrame.Caption+"\n"+Original;
        }
    }
}
