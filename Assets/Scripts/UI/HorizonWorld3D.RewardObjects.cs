using System.Collections.Generic;
using Horizon.Game;
using UnityEngine;
using UnityEngine.Rendering;

namespace Horizon.UI
{
    public sealed partial class HorizonWorld3D
    {
        private readonly Dictionary<string, List<Transform>> rewardPool = new Dictionary<string, List<Transform>>();
        private readonly List<Transform> movieObjects = new List<Transform>();
        private readonly List<MaterialReward> movieObjectReceipts = new List<MaterialReward>();
        private Mesh rewardRing;
        private Transform RewardObject(RewardObjectKind kind, int index)
        {
            bool ticket = moviePlan?.Event.receipt?.triggerId == "ticket" || moviePlan?.Event.receipt?.triggerId == "appointment";
            string key = kind.ToString() + (kind == RewardObjectKind.TriggerObject ? ticket ? ":ticket" : ":clock" : "");
            if (!rewardPool.TryGetValue(key, out List<Transform> pool))
            { pool = new List<Transform>(); rewardPool.Add(key, pool); }
            while (pool.Count <= index) pool.Add(BuildRewardObject(kind, ticket));
            Transform item = pool[index]; item.localRotation = Quaternion.identity;
            ResetMovieColor(item);
            if (kind == RewardObjectKind.ToolKit) item.Find("Tool shaft").localPosition = new Vector3(-.17f,.3f,0);
            if (kind == RewardObjectKind.ReservoirCore) item.Find("Storage lid").localPosition = new Vector3(0,.55f,0);
            item.gameObject.SetActive(true); return item;
        }
        private Transform BuildRewardObject(RewardObjectKind kind, bool ticket)
        {
            Transform item = Group(kind.ToString(), cinematicStage);
            if (rewardRing == null) rewardRing = Own(HorizonSculpt.Torus(.42f, .065f));
            switch (kind)
            {
                case RewardObjectKind.EnergyCell:
                    Shape(item, "Cell body", PrimitiveType.Cylinder, Vector3.zero, new Vector3(.32f,.3f,.32f), teal);
                    Shape(item, "Positive terminal", PrimitiveType.Cylinder, new Vector3(0,.32f,0), new Vector3(.15f,.045f,.15f), ivory);
                    Ring(item, "Charge band", Vector3.zero, .17f, portalLight, false);
                    Box(item, "Charge window", new Vector3(0,0,-.17f), new Vector3(.09f,.36f,.025f), portalLight); break;
                case RewardObjectKind.FocusLens:
                case RewardObjectKind.HorizonLens:
                    Sculpt(item, "Optical rim", rewardRing, Vector3.zero, Vector3.one, ivory);
                    Transform lens = Shape(item, "Glass lens", PrimitiveType.Cylinder, Vector3.zero, new Vector3(.75f,.025f,.75f), glass);
                    lens.localRotation = Quaternion.Euler(90,0,0);
                    if (kind == RewardObjectKind.HorizonLens)
                    { Box(item, "Telescope body", new Vector3(0,0,.28f), new Vector3(.43f,.43f,.7f), teal); Box(item,"Viewfinder",new Vector3(0,.35f,.35f),new Vector3(.12f,.15f,.3f),ivory); } break;
                case RewardObjectKind.WarmLamp:
                    Shape(item, "Lamp base", PrimitiveType.Cylinder, new Vector3(0,-.36f,0),new Vector3(.42f,.04f,.42f),wood);
                    Box(item, "Lamp stem", new Vector3(0,-.16f,0), new Vector3(.055f,.4f,.055f), ivory);
                    Shape(item,"Warm globe",PrimitiveType.Sphere,new Vector3(0,.12f,0),Vector3.one*.48f,warmLight); break;
                case RewardObjectKind.MoneyWallet:
                    Box(item,"Wallet",Vector3.zero,new Vector3(.7f,.45f,.19f),wood);
                    Box(item,"Wallet flap",new Vector3(0,.17f,-.11f),new Vector3(.71f,.17f,.035f),gold);
                    for(int i=0;i<3;i++) { Transform coin=Shape(item,"Coin",PrimitiveType.Cylinder,new Vector3(.1f-i*.12f,.28f+i*.06f,0),new Vector3(.18f,.025f,.18f),gold); coin.localRotation=Quaternion.Euler(75,0,0); } break;
                case RewardObjectKind.ConnectionRing:
                case RewardObjectKind.BrokenLink:
                    Sculpt(item,"Player link",rewardRing,new Vector3(-.2f,0,0),Vector3.one*.7f,teal);
                    Transform other=Sculpt(item,"Other link",rewardRing,new Vector3(.2f,0,0),Vector3.one*.7f,kind==RewardObjectKind.BrokenLink?gold:ivory);
                    other.localRotation=Quaternion.Euler(0,75,0); break;
                case RewardObjectKind.ToolKit:
                case RewardObjectKind.RepairKit:
                    Box(item,"Tool case",new Vector3(0,-.18f,0),new Vector3(.7f,.35f,.4f),kind==RewardObjectKind.RepairKit?ivory:teal);
                    Box(item,"Handle",new Vector3(0,.06f,0),new Vector3(.3f,.07f,.09f),gold);
                    Box(item,"Tool shaft",new Vector3(-.17f,.3f,0),new Vector3(.065f,.43f,.065f),ivory);
                    Box(item,"Tool head",new Vector3(-.17f,.5f,0),new Vector3(.24f,.13f,.11f),gold);
                    if(kind==RewardObjectKind.RepairKit) { Box(item,"Repair cross vertical",new Vector3(.12f,-.16f,-.215f),new Vector3(.06f,.18f,.035f),teal); Box(item,"Repair cross horizontal",new Vector3(.12f,-.16f,-.215f),new Vector3(.18f,.06f,.035f),teal); } break;
                case RewardObjectKind.MemoryFilm:
                    for(int i=0;i<4;i++)
                    {
                        Box(item,"Film frame "+i,new Vector3((i-1.5f)*.25f,0,0),new Vector3(.23f,.4f,.055f),dark);
                        Box(item,"Recorded scene "+i,new Vector3((i-1.5f)*.25f,0,-.035f),new Vector3(.16f,.26f,.025f),teal);
                        for(int side=-1;side<=1;side+=2) for(int hole=0;hole<3;hole++) Box(item,"Film perforation",new Vector3((i-1.5f)*.25f+(hole-1)*.07f,side*.17f,-.035f),Vector3.one*.025f,ivory);
                    } break;
                case RewardObjectKind.RouteLock:
                    Box(item,"Mechanical lock",new Vector3(0,-.18f,0),new Vector3(.48f,.35f,.2f),gold);
                    Sculpt(item,"Lock shackle",rewardRing,new Vector3(0,.08f,0),Vector3.one*.5f,ivory);
                    Box(item,"Keyhole",new Vector3(0,-.16f,-.115f),new Vector3(.05f,.13f,.025f),dark); break;
                case RewardObjectKind.TriggerObject:
                    if(ticket)
                    { Box(item,"Appointment ticket",Vector3.zero,new Vector3(.68f,.36f,.06f),ivory); Box(item,"Ticket date",new Vector3(0,0,-.04f),new Vector3(.36f,.12f,.02f),teal); }
                    else { Shape(item,"Alarm clock",PrimitiveType.Cylinder,Vector3.zero,new Vector3(.52f,.09f,.52f),ivory).localRotation=Quaternion.Euler(90,0,0);
                        Box(item,"Clock minute hand",new Vector3(0,.09f,-.1f),new Vector3(.025f,.18f,.025f),dark); Box(item,"Clock hour hand",new Vector3(.06f,0,-.1f),new Vector3(.15f,.025f,.025f),dark);
                        Shape(item,"Clock bell",PrimitiveType.Sphere,new Vector3(-.2f,.26f,0),Vector3.one*.16f,gold); Shape(item,"Clock bell",PrimitiveType.Sphere,new Vector3(.2f,.26f,0),Vector3.one*.16f,gold); } break;
                case RewardObjectKind.Projector:
                    Box(item,"Projection device",Vector3.zero,new Vector3(.65f,.35f,.48f),dark);
                    Shape(item,"Projector lens",PrimitiveType.Cylinder,new Vector3(0,0,-.28f),new Vector3(.24f,.07f,.24f),glass).localRotation=Quaternion.Euler(90,0,0);
                    for(int i=-1;i<=1;i+=2) Shape(item,"Film spool",PrimitiveType.Cylinder,new Vector3(i*.23f,.32f,0),new Vector3(.34f,.07f,.34f),ivory).localRotation=Quaternion.Euler(90,0,0); break;
                case RewardObjectKind.RealityMilestone:
                    Box(item,"Heavy milestone plinth",new Vector3(0,-.3f,0),new Vector3(.7f,.22f,.55f),wood);
                    Box(item,"Permanent coordinate",new Vector3(0,.1f,0),new Vector3(.28f,.65f,.23f),ivory);
                    Box(item,"Reality mark",new Vector3(0,.17f,-.13f),new Vector3(.15f,.15f,.025f),gold); break;
                case RewardObjectKind.ReservoirCore:
                    Shape(item,"Transparent reservoir",PrimitiveType.Cylinder,Vector3.zero,new Vector3(.67f,.55f,.67f),glass);
                    Ring(item,"Storage base",new Vector3(0,-.55f,0),.37f,ivory,false);
                    // The animated transform owns the lid's height; keep its
                    // vertices centred so reuse does not apply that offset twice.
                    Ring(item,"Storage lid",Vector3.zero,.37f,gold,false);
                    Shape(item,"Accumulated cause energy",PrimitiveType.Sphere,Vector3.zero,Vector3.one*.4f,portalLight); break;
                case RewardObjectKind.OrbitNode:
                    Sculpt(item,"Orbit carrier",rewardRing,Vector3.zero,Vector3.one,gold);
                    Shape(item,"Future node",PrimitiveType.Sphere,Vector3.zero,Vector3.one*.18f,portalLight); break;
                case RewardObjectKind.CausalChain:
                    for(int i=0;i<3;i++) { Sculpt(item,"Causal link "+i,rewardRing,new Vector3((i-1)*.35f,0,0),Vector3.one*.55f,teal).localRotation=Quaternion.Euler(0,i%2*75,0); } break;
                case RewardObjectKind.PredictionPanel:
                    Box(item,"Transparent prediction",Vector3.zero,new Vector3(.8f,.52f,.025f),glass);
                    CausalLine(item,"Predicted trajectory",new Vector3(-.3f,-.12f,-.035f),new Vector3(.3f,.18f,-.035f),portalLight,.025f); break;
                default:
                    // An actual triangular optical prism; no generic reward gem.
                    Mesh prism = RewardPrism(); Sculpt(item,"Three sided prism",prism,Vector3.zero,Vector3.one,ivory);
                    if(kind==RewardObjectKind.ConvergencePrism) for(int i=0;i<3;i++)
                        CausalLine(item,"Convergence inlet "+i,new Vector3((i-1)*.65f,-.4f,.15f),new Vector3(0,0,0),i==2?warmLight:portalLight,.04f);
                    break;
            }
            foreach(Renderer renderer in item.GetComponentsInChildren<Renderer>()) { renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false; }
            item.gameObject.SetActive(false); return item;
        }
        private Mesh prismMesh;
        private Mesh RewardPrism()
        {
            if(prismMesh!=null) return prismMesh;
            Vector3[] points={new Vector3(-.32f,-.24f,-.2f),new Vector3(.32f,-.24f,-.2f),new Vector3(0,.34f,-.2f),
                new Vector3(-.32f,-.24f,.2f),new Vector3(.32f,-.24f,.2f),new Vector3(0,.34f,.2f)};
            int[] indices={0,2,1,3,4,5,0,1,4,0,4,3,1,2,5,1,5,4,2,0,3,2,3,5};
            prismMesh=Own(new Mesh { name="HORIZON optical prism",vertices=points,triangles=indices }); prismMesh.RecalculateNormals(); return prismMesh;
        }
        private void PrepareRewardObjects(RewardPlan plan)
        {
            foreach(Transform item in movieObjects) item.gameObject.SetActive(false);
            movieObjects.Clear(); movieObjectReceipts.Clear();
            var indices=new Dictionary<RewardObjectKind,int>();
            foreach(MaterialReward reward in plan.Objects)
            {
                for(int i=0;i<reward.VisualCount;i++)
                {
                    indices.TryGetValue(reward.Kind,out int index); indices[reward.Kind]=index+1;
                    Transform item=RewardObject(reward.Kind,index); movieObjects.Add(item); movieObjectReceipts.Add(reward);
                    ResetMovieColor(item);
                    item.localPosition=new Vector3((movieObjects.Count%5-2)*.62f,2.7f,1.1f);
                    item.localRotation=Quaternion.identity; item.localScale=Vector3.one*.05f;
                }
            }
        }
    }
}
