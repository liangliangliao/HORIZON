using System.Collections.Generic;
using Horizon.Game;
using UnityEngine;

namespace Horizon.UI
{
    public sealed partial class HorizonWorld3D
    {
        private Mesh precisionRing, fineRing, knurledBody;
        private readonly Dictionary<Transform,Transform[]> revealLeaves=new Dictionary<Transform,Transform[]>();

        private void DetailRewardObject(Transform item,RewardObjectKind kind)
        {
            if(precisionRing==null)
            {
                precisionRing=Own(HorizonSculpt.Torus(.42f,.022f));
                fineRing=Own(HorizonSculpt.Torus(.42f,.009f));
                knurledBody=Own(HorizonSculpt.Profile("Machined bevel collar",new[] {
                    new Vector2(.32f,-.06f),new Vector2(.4f,-.06f),new Vector2(.44f,-.025f),
                    new Vector2(.44f,.025f),new Vector2(.4f,.06f),new Vector2(.32f,.06f) },48));
            }
            if(kind==RewardObjectKind.EnergyCell || kind==RewardObjectKind.ReservoirCore)
            {
                float radius=kind==RewardObjectKind.EnergyCell?.22f:.38f;
                for(int row=0;row<3;row++)
                {
                    Transform collar=Sculpt(item,"Machined energy collar "+row,knurledBody,
                        new Vector3(0,-.25f+row*.25f,0),Vector3.one*(radius/.42f),gold);
                    for(int tick=0;tick<8;tick++)
                    {
                        float a=tick*Mathf.PI/4;
                        Soft(collar,"Collar cooling flute "+tick,new Vector3(Mathf.Cos(a)*.43f,0,Mathf.Sin(a)*.43f),new Vector3(.035f,.09f,.035f),ivory);
                    }
                }
                var leaves=new Transform[6]; revealLeaves[item]=leaves;
                for(int i=0;i<leaves.Length;i++)
                {
                    float a=i*Mathf.PI/3;
                    Transform hinge=Group("Cinematic casing leaf "+i,item);
                    hinge.localPosition=new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius);
                    hinge.localRotation=Quaternion.Euler(0,90-a*Mathf.Rad2Deg,0);
                    Soft(hinge,"Enamel energy shield",Vector3.zero,new Vector3(.15f,.5f,.035f),showcaseEnamel);
                    Soft(hinge,"Energy shield inlay",new Vector3(0,0,-.025f),new Vector3(.035f,.33f,.015f),portalLight);
                    leaves[i]=hinge;
                }
            }
            if(kind==RewardObjectKind.FocusLens || kind==RewardObjectKind.HorizonLens)
            {
                for(int side=0;side<2;side++)
                {
                    Sculpt(item,"Polished optical bezel "+side,precisionRing,new Vector3(0,0,side==0?-.055f:.13f),Vector3.one*1.14f,gold);
                    for(int tick=0;tick<12;tick++)
                    {
                        float a=tick*Mathf.PI/6;
                        Soft(item,"Focus calibration tooth "+side+" "+tick,new Vector3(Mathf.Cos(a)*.46f,Mathf.Sin(a)*.46f,side==0?-.06f:.14f),
                            new Vector3(.027f,.085f,.055f),tick%3==0?gold:ivory).localRotation=Quaternion.Euler(0,0,a*Mathf.Rad2Deg-90);
                    }
                }
                Sculpt(item,"Glass optical reticle",fineRing,new Vector3(0,0,-.075f),Vector3.one*.74f,portalLight);
            }
            if(kind==RewardObjectKind.WarmLamp)
            {
                for(int i=0;i<6;i++)
                {
                    float a=i*Mathf.PI/3;
                    Soft(item,"Lantern protective rib "+i,new Vector3(Mathf.Cos(a)*.25f,.1f,Mathf.Sin(a)*.25f),new Vector3(.024f,.5f,.024f),gold);
                }
                for(int row=0;row<2;row++)
                    Sculpt(item,"Warm lamp machined trim "+row,precisionRing,new Vector3(0,-.13f+row*.48f,0),Vector3.one*.64f,gold).localRotation=Quaternion.Euler(90,0,0);
                Shape(item,"Warm filament",PrimitiveType.Sphere,new Vector3(0,.13f,-.18f),new Vector3(.13f,.25f,.13f),warmLight);
            }
            if(kind==RewardObjectKind.MoneyWallet || kind==RewardObjectKind.ToolKit || kind==RewardObjectKind.RepairKit)
            {
                Transform shell=Soft(item,"Bevelled fitted case",new Vector3(0,-.28f,.13f),new Vector3(.87f,.43f,.49f),kind==RewardObjectKind.MoneyWallet?wood:showcaseEnamel);
                for(int side=-1;side<=1;side+=2)
                {
                    Soft(item,"Case metallic corner "+side,new Vector3(side*.38f,-.28f,-.11f),new Vector3(.075f,.38f,.055f),gold);
                    Soft(item,"Case latch "+side,new Vector3(side*.24f,-.14f,-.135f),new Vector3(.1f,.085f,.035f),ivory);
                }
                for(int stitch=0;stitch<9;stitch++)
                    Soft(shell,"Fitted case stitch "+stitch,new Vector3((stitch-4)*.09f,-.34f,-.53f),new Vector3(.045f,.017f,.015f),gold);
            }
            if(kind==RewardObjectKind.ConnectionRing || kind==RewardObjectKind.BrokenLink || kind==RewardObjectKind.CausalChain)
            {
                foreach(Transform link in item.GetComponentsInChildren<Transform>())
                {
                    if(!link.name.Contains("link") || link.GetComponent<MeshFilter>()==null) continue;
                    Sculpt(link,"Engraved outer link band",fineRing,new Vector3(0,0,-.08f),Vector3.one*1.12f,gold);
                    for(int stud=0;stud<8;stud++)
                    {
                        float a=stud*Mathf.PI/4;
                        Shape(link,"Link coordinate stud "+stud,PrimitiveType.Sphere,new Vector3(Mathf.Cos(a)*.42f,Mathf.Sin(a)*.42f,-.06f),Vector3.one*.055f,ivory);
                    }
                }
            }
            if(kind==RewardObjectKind.InsightPrism || kind==RewardObjectKind.ConvergencePrism)
            {
                for(int i=0;i<3;i++)
                {
                    float a=i*Mathf.PI*2/3+Mathf.PI/2;
                    Vector3 at=new Vector3(Mathf.Cos(a)*.2f,Mathf.Sin(a)*.2f,-.23f);
                    Shape(item,"Prism inner optical facet "+i,PrimitiveType.Cube,at,new Vector3(.035f,.2f,.035f),portalLight).localRotation=Quaternion.Euler(0,0,a*Mathf.Rad2Deg);
                }
                Sculpt(item,"Prism optical coordinate halo",fineRing,new Vector3(0,0,.22f),Vector3.one*1.15f,gold);
            }
        }

        private void SampleRewardDetail(Transform item,float time,float open)
        {
            if(!preferences.reducedMotion)
            {
                foreach(Transform part in item)
                {
                    if(part.name.StartsWith("Causal link ") && rewardRestPositions.TryGetValue(part,out Vector3 rest))
                    {
                        float progress=Mathf.SmoothStep(0,1,Mathf.Clamp01(open*1.3f-(part.GetSiblingIndex()%3)*.14f));
                        part.localPosition=rest+new Vector3(rest.x*1.7f,.18f,0)*(1-progress);
                        part.localRotation=Quaternion.Slerp(rewardRestRotations[part]*Quaternion.Euler(0,70,0),rewardRestRotations[part],progress);
                    }
                    if(part.name.StartsWith("Prism inner optical facet "))
                        part.localScale=rewardRestScales[part]*(.65f+.35f*open+.06f*Mathf.Sin(time*6));
                }
            }
            if(!revealLeaves.TryGetValue(item,out Transform[] leaves)) return;
            for(int i=0;i<leaves.Length;i++)
            {
                Transform leaf=leaves[i];
                Vector3 rest=rewardRestPositions[leaf];
                Vector3 radial=new Vector3(rest.x,0,rest.z).normalized;
                leaf.localPosition=rest+radial*open*.28f+Vector3.up*Mathf.Sin(open*Mathf.PI)*.11f;
                leaf.localRotation=rewardRestRotations[leaf]*Quaternion.Euler(0,0,open*18);
            }
        }
    }
}
