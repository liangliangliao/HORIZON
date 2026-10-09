using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Horizon.Game;
using UnityEngine;

namespace Horizon.UI
{
    [Serializable]
    public sealed class HorizonPerformanceReport
    {
        public string capturedUtc, device, operatingSystem, graphics, graphicsApi, applicationVersion;
        public string measurement="Rendered frame intervals; paused/background frames excluded. P95 uses the latest 1800 samples per scene/profile.";
        public bool probableEmulator;
        public int width,height;
        public List<FramePerformanceSummary> scenes=new List<FramePerformanceSummary>();
    }
    public sealed partial class HorizonWorld3D
    {
        private readonly AdaptiveFrameBudget adaptiveBudget=new AdaptiveFrameBudget();
        private readonly Dictionary<string,FramePerformanceWindow> performanceWindows=new Dictionary<string,FramePerformanceWindow>();
        private readonly Dictionary<string,string[]> performanceKeys=new Dictionary<string,string[]>();
        private int performanceWarmup=90;
        private float priorLodBias=1;
        private bool savedLodBias;
        public int AdaptiveQualityLevel => adaptiveBudget.Level;
        private bool LowCostEffects => preferences.batterySaver || adaptiveBudget.Level>0;
        private int ParticleBudget => preferences.batterySaver?(adaptiveBudget.Level==0?12:8):adaptiveBudget.Level==0?64:adaptiveBudget.Level==1?32:16;
        private void ConfigureActorLod(Transform person,Material coat)
        {
            Renderer[] close=person.GetComponentsInChildren<Renderer>(true);
            Renderer[] middle=close.Where(r=>r.name!="Jacket button" && r.name!="Eye light" && r.name!="Nose" && r.name!="Ear").ToArray();
            Transform silhouette=Group("Distant character silhouette",person);
            Box(silhouette,"Distant coat",new Vector3(0,1,0),new Vector3(.56f,.72f,.38f),coat);
            Box(silhouette,"Distant head",new Vector3(0,1.64f,0),new Vector3(.46f,.48f,.4f),skin);
            for(int i=-1;i<=1;i+=2) Box(silhouette,"Distant leg",new Vector3(i*.15f,.32f,0),new Vector3(.17f,.63f,.22f),dark);
            var lod=person.gameObject.AddComponent<LODGroup>();
            lod.SetLODs(new[] { new LOD(.16f,close),new LOD(.065f,middle),new LOD(0,silhouette.GetComponentsInChildren<Renderer>()) });
            lod.RecalculateBounds();
        }
        private void SamplePerformance()
        {
            if(paused || !Application.isFocused) { performanceWarmup=Math.Max(performanceWarmup,15); return; }
            if(performanceWarmup>0) { performanceWarmup--; return; }
            float seconds=Time.unscaledDeltaTime;
            string scene=moviePlan!=null?moviePlan.Event.kind+" / "+moviePlan.Event.tier:
                imaginedScene!=null?"Imagination / "+CurrentImaginationFrame.Shot:inspectedSystem??(station?"Future station":"Board");
            string quality=(preferences.batterySaver?"Battery30":"Normal60")+" / L"+adaptiveBudget.Level;
            string key=scene+"|"+quality; int target=preferences.batterySaver?30:60;
            if(!performanceWindows.TryGetValue(key,out FramePerformanceWindow window))
            { window=new FramePerformanceWindow(); performanceWindows.Add(key,window); performanceKeys.Add(key,new[] { scene,quality,target.ToString() }); }
            window.Record(seconds,target);
            if(adaptiveBudget.Record(seconds,target)) ApplyAdaptiveQuality();
        }
        public void ResetPerformanceRecording()
        { performanceWindows.Clear(); performanceKeys.Clear(); performanceWarmup=30; }
        public HorizonPerformanceReport PerformanceReport()
        {
            string gpu=SystemInfo.graphicsDeviceName;
            var report=new HorizonPerformanceReport { capturedUtc=DateTime.UtcNow.ToString("O"),device=SystemInfo.deviceModel,
                operatingSystem=SystemInfo.operatingSystem,graphics=gpu,graphicsApi=SystemInfo.graphicsDeviceType.ToString(),
                applicationVersion=Application.version,width=Screen.width,height=Screen.height,
                probableEmulator=gpu.IndexOf("swiftshader",StringComparison.OrdinalIgnoreCase)>=0 ||
                    SystemInfo.deviceModel.IndexOf("emulator",StringComparison.OrdinalIgnoreCase)>=0 || Application.isEditor };
            foreach(var pair in performanceWindows)
            { string[] data=performanceKeys[pair.Key]; report.scenes.Add(pair.Value.Summarize(data[0],data[1],int.Parse(data[2]))); }
            return report;
        }
        public string ExportPerformanceReport()
        {
            string json=JsonUtility.ToJson(PerformanceReport(),true);
            File.WriteAllText(Path.Combine(Application.persistentDataPath,"horizon-performance.json"),json);
            return json;
        }
        private void ApplyAdaptiveQuality()
        {
            if(!savedLodBias) { priorLodBias=QualitySettings.lodBias; savedLodBias=true; }
            QualitySettings.lodBias=preferences.batterySaver?.75f:adaptiveBudget.Level==0?1:adaptiveBudget.Level==1?.8f:.65f;
            if(runtimePipeline!=null)
            {
                runtimePipeline.renderScale=preferences.batterySaver?(adaptiveBudget.Level==0?.85f:adaptiveBudget.Level==1?.75f:.65f):adaptiveBudget.Level==0?1:adaptiveBudget.Level==1?.85f:.72f;
                runtimePipeline.shadowDistance=preferences.batterySaver || adaptiveBudget.Level==2?0:adaptiveBudget.Level==1?12:22;
            }
            if(movieParticles!=null)
            {
                var main=movieParticles.main;
                if(main.maxParticles!=ParticleBudget)
                { movieParticles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear); main.maxParticles=ParticleBudget; }
            }
            if(optics!=null) optics.LowPower=LowCostEffects;
            if(bloom!=null) bloom.enabled=!LowCostEffects && bloom.IsSupported;
        }
    }
}
