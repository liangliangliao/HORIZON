using System;
using System.Linq;
using System.Text;
using Horizon.UI;
using UnityEngine;

namespace Horizon
{
    public sealed partial class HorizonApp
    {
        private void ShowPerformance()
        {
            HorizonPerformanceReport report=world.PerformanceReport();
            SettingsPanel("运行表现",report.device+" · "+report.width+" × "+report.height);
            var text=new StringBuilder();
            foreach(var item in report.scenes.OrderByDescending(s=>s.seconds).Take(7))
                text.AppendLine(item.scene+" · "+item.quality+"\n"+item.seconds.ToString("F0")+" 秒 · 平均 "+item.averageFps.ToString("F1")+" 帧 · P95 "+item.p95Milliseconds.ToString("F1")+" ms\n");
            if(report.scenes.Count==0) text.Append("还没有足够的画面样本。\n继续游玩至少一分钟，再查看这里。");
            View.Label(overlay,"Performance measurements",text.ToString(),22,Palette.Text,TextAnchor.UpperLeft,.075f,.28f,.925f,.81f);
            View.Label(overlay,"Performance explanation","暂停与后台时间不计入。\n标准画质目标 60 帧，省电画质目标 30 帧。",22,Palette.Muted,TextAnchor.MiddleLeft,.075f,.19f,.925f,.27f);
            View.Button(overlay,"Copy performance report","复制报告",()=> {
                try { GUIUtility.systemCopyBuffer=world.ExportPerformanceReport(); }
                catch(Exception) { GUIUtility.systemCopyBuffer=JsonUtility.ToJson(report,true); }
            },.075f,.12f,.49f,.183f,Palette.Panel,Palette.Mint,25);
            View.Button(overlay,"Restart performance recording","重新记录并继续",()=> { world.ResetPerformanceRecording(); CloseSettings(); },.51f,.12f,.925f,.183f,Palette.Mint,Palette.Ink,25);
            View.Button(overlay,"Back from performance","返回设置",RenderSettings,.075f,.035f,.925f,.098f,Palette.Panel,Palette.Text,26);
        }
    }
}
