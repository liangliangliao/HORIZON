using System;
using System.Collections.Generic;
using System.Linq;
using Horizon.Game;
using UnityEngine;

namespace Horizon.UI
{
    public sealed partial class HorizonWorld3D
    {
        private readonly Dictionary<string, Texture2D> memoryTextures=new Dictionary<string, Texture2D>();
        private readonly Dictionary<Renderer,Material> memoryMaterials=new Dictionary<Renderer,Material>();
        private Transform memoryFreezePlane;
        public void RecordImaginationFrame(ImagineRun run)
        {
            if(run==null || imaginedScene==null || run.id!=imaginedScene.id || run.phase!=imaginedScene.phase) return;
            if(run.phase!=ImaginePhase.VictoryAnchor && run.phase!=ImaginePhase.Effort && run.phase!=ImaginePhase.Failure &&
                run.phase!=ImaginePhase.Adjust && run.phase!=ImaginePhase.Victory) return;
            float priorTime=imaginationTime;
            Rect rect=WorldCamera.rect; RenderTexture prior=WorldCamera.targetTexture, active=RenderTexture.active;
            var target=RenderTexture.GetTemporary(128,96,24,RenderTextureFormat.ARGB32);
            Texture2D texture=null;
            try
            {
                // Finish the actual current shot before freezing it, including
                // when the player taps quickly. Never invent unvisited actions.
                imaginationTime=Mathf.Max(imaginationTime,run.phase==ImaginePhase.Victory?4.5f:2.6f);
                SampleImaginationScene();
                WorldCamera.targetTexture=target; WorldCamera.rect=new Rect(0,0,1,1);
                WorldCamera.Render(); RenderTexture.active=target;
                texture=new Texture2D(128,96,TextureFormat.RGB24,false);
                texture.ReadPixels(new Rect(0,0,128,96),0,0); texture.Apply();
                byte[] bytes=texture.EncodeToJPG(55);
                if(bytes.Length*4/3>MemoryFrame.MaximumEncodedLength) bytes=texture.EncodeToJPG(25);
                MemoryFrame.Record(run,new MemoryFrame { runId=run.id,beatIndex=run.timeline.Count-1,phase=run.phase,
                    actionKey=run.phase==ImaginePhase.Adjust?run.memories.LastOrDefault()?.actionKey:run.timeline.Last().actionKey,
                    width=128,height=96,jpeg=Convert.ToBase64String(bytes) });
            }
            catch(Exception e) { Debug.LogWarning("Memory frame unavailable: "+e.GetType().Name); }
            finally
            {
                WorldCamera.targetTexture=prior; WorldCamera.rect=rect; RenderTexture.active=active;
                RenderTexture.ReleaseTemporary(target); Dispose(texture);
                imaginationTime=priorTime; SampleImaginationScene();
            }
        }
        private void BindMemoryFilm(Transform film,IEnumerable<MemoryFrame> source)
        {
            ClearMemoryTextures();
            var frames=MemoryFrame.CopyFrames(source);
            for(int i=0;i<MemoryFrame.MaximumFrames;i++)
            {
                Transform cell=film.Find("Recorded scene "+i); if(cell==null) continue;
                var renderer=cell.GetComponent<Renderer>();
                cell.gameObject.SetActive(i<frames.Count);
                if(i>=frames.Count) continue;
                MemoryFrame frame=frames[i]; Texture2D texture=null;
                if(!memoryTextures.TryGetValue(frame.jpeg,out texture))
                {
                    try
                    {
                        byte[] bytes=Convert.FromBase64String(frame.jpeg);
                        // Bound dimensions before Unity allocates decoded pixels.
                        if(!IsMemoryJpeg(bytes,frame.width,frame.height)) { cell.gameObject.SetActive(false); continue; }
                        texture=new Texture2D(2,2,TextureFormat.RGB24,false);
                        if(!texture.LoadImage(bytes,true)) { Dispose(texture); cell.gameObject.SetActive(false); continue; }
                        texture.name="Recorded "+frame.phase+" beat "+frame.beatIndex;
                        memoryTextures.Add(frame.jpeg,texture);
                    }
                    catch(FormatException) { cell.gameObject.SetActive(false); continue; }
                }
                if(!memoryMaterials.TryGetValue(renderer,out Material material))
                { material=new Material(Resources.Load<Shader>("HorizonMemory")); materials.Add(material); memoryMaterials.Add(renderer,material); }
                material.mainTexture=texture; renderer.sharedMaterial=material; renderer.SetPropertyBlock(null);
            }
        }
        private void SampleMemoryFreeze(float progress)
        {
            var frames=moviePlan?.Event.receipt?.frames;
            if(frames==null || frames.Count==0) return;
            Transform film=movieObjects.FirstOrDefault(x=>x.name==RewardObjectKind.MemoryFilm.ToString());
            if(film==null) return;
            if(memoryFreezePlane==null)
                memoryFreezePlane=Shape(cinematicStage,"Frozen previsualization frame",PrimitiveType.Quad,Vector3.zero,Vector3.one,teal);
            float sequence=Mathf.Clamp01(progress/.65f)*frames.Count;
            int index=Mathf.Min(frames.Count-1,Mathf.FloorToInt(sequence));
            Transform cell=film.Find("Recorded scene "+index);
            for(int i=0;i<frames.Count;i++)
            {
                Transform completed=film.Find("Recorded scene "+i);
                if(completed!=null) completed.gameObject.SetActive(i<Mathf.FloorToInt(sequence));
            }
            memoryFreezePlane.gameObject.SetActive(progress<.65f && cell!=null && cell.GetComponent<Renderer>().sharedMaterial.mainTexture!=null);
            if(!memoryFreezePlane.gameObject.activeSelf) return;
            memoryFreezePlane.GetComponent<Renderer>().sharedMaterial=cell.GetComponent<Renderer>().sharedMaterial;
            float shrink=Smooth(.3f,.95f,Mathf.Repeat(sequence,1));
            memoryFreezePlane.position=Vector3.Lerp(cinematicStage.TransformPoint(new Vector3(0,1.65f,1.3f)),cell.position,shrink);
            memoryFreezePlane.localScale=Vector3.Lerp(new Vector3(2.4f,1.8f,1),new Vector3(.28f,.21f,1)*film.localScale.x,shrink);
            memoryFreezePlane.rotation=film.rotation;
        }
        public static bool IsMemoryJpeg(byte[] bytes,int width,int height)
        {
            if(bytes==null || bytes.Length<4 || bytes.Length>MemoryFrame.MaximumEncodedLength || bytes[0]!=255 || bytes[1]!=216) return false;
            int p=2;
            while(p+8<bytes.Length)
            {
                if(bytes[p++]!=255) return false;
                int marker=bytes[p++]; if(marker==255) { p--; continue; }
                int length=(bytes[p]<<8)|bytes[p+1]; if(length<2 || p+length>bytes.Length) return false;
                if(marker==192 || marker==194)
                    return ((bytes[p+3]<<8)|bytes[p+4])==height && ((bytes[p+5]<<8)|bytes[p+6])==width && width<=192 && height<=128;
                p+=length;
            }
            return false;
        }
        private void ClearMemoryTextures()
        { foreach(Texture2D texture in memoryTextures.Values) Dispose(texture); memoryTextures.Clear(); }
    }
}
