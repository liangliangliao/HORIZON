using UnityEngine;
using UnityEngine.Rendering;

namespace Horizon.UI
{
    // The same embossed 3D coins upgrade board, gate and echo bursts. All slots
    // are allocated once; repeated rewards cannot accumulate transient objects.
    public sealed class HorizonRewardSparkPool : MonoBehaviour
    {
        private readonly Transform[] slots=new Transform[64];
        private readonly Renderer[] renderers=new Renderer[64];
        private readonly Vector3[] velocities=new Vector3[64];
        private readonly float[] ages=new float[64];
        private Material gold,mint,coral;
        private int cursor;
        private bool paused;
        public int LiveCount { get; private set; }

        public void Initialize(Mesh mesh,Material goldMaterial,Material mintMaterial,Material coralMaterial)
        {
            gold=goldMaterial; mint=mintMaterial; coral=coralMaterial;
            for(int i=0;i<slots.Length;i++)
            {
                var item=new GameObject("Pooled world reward coin "+i,typeof(MeshFilter),typeof(MeshRenderer));
                item.transform.SetParent(transform,false); slots[i]=item.transform;
                item.GetComponent<MeshFilter>().sharedMesh=mesh; renderers[i]=item.GetComponent<MeshRenderer>();
                renderers[i].sharedMaterial=gold; renderers[i].shadowCastingMode=ShadowCastingMode.Off;
                renderers[i].receiveShadows=false; item.SetActive(false);
            }
        }

        public void Emit(Vector3 position,Color color,int count,int budget)
        {
            if(VisualPreferences.ReducedMotion) return;
            count=Mathf.Min(count,Mathf.Max(0,budget-LiveCount));
            for(int i=0;i<count;i++)
            {
                int index=cursor++%slots.Length;
                for(int search=0;search<slots.Length && slots[index].gameObject.activeSelf;search++) index=cursor++%slots.Length;
                Transform item=slots[index]; item.gameObject.SetActive(true); LiveCount++;
                item.position=position; item.localScale=Vector3.one*.32f; ages[index]=0;
                item.localRotation=Quaternion.Euler(index*31,index*47,index*13);
                renderers[index].sharedMaterial=color==Palette.Coral?coral:color==Palette.Mint?mint:gold;
                float angle=i*2.39996f;
                velocities[index]=new Vector3(Mathf.Cos(angle)*1.8f,2.2f+i%5*.35f,Mathf.Sin(angle)*1.8f);
            }
        }

        public void SetBudget(int budget,bool reducedMotion)
        {
            int cap=reducedMotion?0:Mathf.Clamp(budget,0,slots.Length);
            for(int i=0;i<slots.Length && LiveCount>cap;i++)
                if(slots[i]!=null && slots[i].gameObject.activeSelf) { slots[i].gameObject.SetActive(false); LiveCount--; }
        }

        public void SetPaused(bool value) { paused=value; }

        private void Update()
        {
            if(paused || VisualPreferences.Paused) return;
            for(int i=0;i<slots.Length;i++)
            {
                Transform item=slots[i]; if(item==null || !item.gameObject.activeSelf) continue;
                ages[i]+=Time.unscaledDeltaTime;
                if(VisualPreferences.ReducedMotion || ages[i]>=1.35f)
                { item.gameObject.SetActive(false); LiveCount--; continue; }
                velocities[i]+=Vector3.down*Time.unscaledDeltaTime*4;
                item.position+=velocities[i]*Time.unscaledDeltaTime;
                item.Rotate(120*Time.unscaledDeltaTime,150*Time.unscaledDeltaTime,80*Time.unscaledDeltaTime);
                item.localScale=Vector3.one*(.32f*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.8f,1.35f,ages[i]))));
            }
        }
    }
}
