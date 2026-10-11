using Horizon.Game;
using UnityEngine;
using UnityEngine.Rendering;

namespace Horizon.UI
{
    public sealed partial class HorizonWorld3D
    {
        private Transform relayStage;
        private CausalRelayPlan relayPlan;
        private readonly Transform[] causalRelays = new Transform[CausalRelayPlan.MaximumNodes];
        private readonly Transform[] relayRings = new Transform[CausalRelayPlan.MaximumNodes];
        private readonly Renderer[,] relayBeams = new Renderer[CausalRelayPlan.MaximumNodes,2];
        private readonly LineRenderer[] relayEdges = new LineRenderer[CausalRelayPlan.MaximumEdges];
        private MaterialPropertyBlock relayProperties;
        private int currentRelaySource = -1;

        private void PrepareCausalRelays()
        {
            relayPlan = new CausalRelayPlan(moviePlan);
            if (relayPlan.Nodes.Count == 0) { ResetCausalRelays(); return; }
            if (relayStage == null)
            {
                relayStage = Group("Pooled actual causal relays",cinematicStage);
                relayProperties = new MaterialPropertyBlock();
                var beam = new Material(Resources.Load<Shader>("HorizonCausalBeam")); materials.Add(beam);
                Mesh rim = Own(HorizonSculpt.Torus(.22f,.018f));
                for (int i=0;i<causalRelays.Length;i++)
                {
                    Transform relay=Group("Causal relay "+i,relayStage); causalRelays[i]=relay;
                    Shape(relay,"Evidence metal base",PrimitiveType.Cylinder,new Vector3(0,-.13f,0),new Vector3(.48f,.055f,.48f),showcaseEnamel);
                    Sculpt(relay,"Evidence bezel",rim,new Vector3(0,-.07f,0),Vector3.one,gold).localRotation=Quaternion.Euler(90,0,0);
                    relayRings[i]=Group("Cue activation ring",relay);
                    Ring(relayRings[i],"Activation wave",new Vector3(0,-.055f,0),.24f,portalLight,false);
                    for (int plane=0;plane<2;plane++)
                    {
                        Transform shaft=Shape(relay,"Director driven causal light shaft "+plane,PrimitiveType.Quad,Vector3.zero,Vector3.one,beam);
                        shaft.localRotation=Quaternion.Euler(0,plane*90,0);
                        relayBeams[i,plane]=shaft.GetComponent<Renderer>();
                    }
                }
                for(int i=0;i<relayEdges.Length;i++)
                    relayEdges[i]=CausalLine(relayStage,"Recorded causal edge "+i,Vector3.zero,Vector3.zero,portalLight,.018f);
                foreach(Renderer renderer in relayStage.GetComponentsInChildren<Renderer>())
                { renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false; }
            }
            relayStage.gameObject.SetActive(true);
            foreach(Transform relay in causalRelays) relay.gameObject.SetActive(false);
            foreach(LineRenderer edge in relayEdges) edge.gameObject.SetActive(false);
            // Chronological neighbours can be independent causes. Only the
            // recorded parent edges are allowed to join these physical relays.
            foreach(LineRenderer line in movieLines) line.gameObject.SetActive(false);
            foreach(CausalRelayNode node in relayPlan.Nodes)
            {
                Vector3 at=new Vector3(node.X,node.Y,node.Z);
                movieNodes[node.Index].localPosition=at;
                causalRelays[node.Index].localPosition=at; causalRelays[node.Index].gameObject.SetActive(true);
                causalRelays[node.Index].name="Causal relay · "+node.Evidence.id;
            }
            for(int i=0;i<relayPlan.Edges.Count;i++)
            {
                CausalRelayEdge edge=relayPlan.Edges[i]; relayEdges[i].gameObject.SetActive(true);
                relayEdges[i].name="Recorded cause · "+relayPlan.Nodes[edge.From].Evidence.id+" → "+relayPlan.Nodes[edge.To].Evidence.id;
                MovieLine(relayEdges[i],movieNodes[edge.From].localPosition,movieNodes[edge.To].localPosition);
            }
        }

        private void SampleCausalRelays(float time)
        {
            if(relayPlan==null || relayStage==null || !relayStage.gameObject.activeSelf) return;
            bool final=time>=relayPlan.ImpactTime;
            int columnBudget=preferences.reducedMotion?0:LowCostEffects?4:causalRelays.Length;
            for(int i=0;i<relayPlan.Nodes.Count;i++)
            {
                CausalRelayFrame frame=relayPlan.Sample(i,time,preferences.reducedMotion);
                Color tint=relayPlan.Nodes[i].Evidence.kind==CausalNodeKind.Action?Palette.Gold:Palette.Mint;
                SetMovieColor(movieNodes[i],frame.Lit?Color.Lerp(tint,tint*1.6f,frame.Pulse)*frame.Fade:Palette.Muted*.25f);
                relayRings[i].localScale=Vector3.one*(1+frame.Pulse*1.1f);
                SetMovieColor(relayRings[i],(frame.Lit?tint:Palette.Muted*.15f)*frame.Fade);
                // Low quality preserves every source and connection, reducing
                // only simultaneous decorative columns around the newest hits.
                int recent=0;
                for(int next=i+1;next<relayPlan.Nodes.Count;next++)
                    if(time>=relayPlan.Nodes[next].ActivationTime) recent++;
                bool show=columnBudget>0 && recent<columnBudget && frame.Beam>.015f;
                for(int plane=0;plane<2;plane++)
                {
                    Renderer renderer=relayBeams[i,plane]; renderer.gameObject.SetActive(show);
                    if(!show) continue;
                    renderer.transform.localPosition=Vector3.up*(frame.Height*.5f-.03f);
                    renderer.transform.localScale=new Vector3(.28f+frame.Pulse*.16f,frame.Height,1);
                    relayProperties.Clear(); relayProperties.SetColor("_Color",tint);
                    relayProperties.SetFloat("_Intensity",frame.Beam);
                    relayProperties.SetFloat("_Clock",time); renderer.SetPropertyBlock(relayProperties);
                }
            }
            for(int i=0;i<relayPlan.Edges.Count;i++)
            {
                CausalRelayEdge edge=relayPlan.Edges[i];
                CausalRelayFrame from=relayPlan.Sample(edge.From,time,preferences.reducedMotion),to=relayPlan.Sample(edge.To,time,preferences.reducedMotion);
                bool lit=from.Lit && to.Lit;
                relayEdges[i].widthMultiplier=lit?.022f+to.Pulse*.035f:.012f;
                SetMovieColor(relayEdges[i].transform,(lit?final?Palette.Gold:Palette.Mint:Palette.Muted*.2f)*to.Fade);
            }
            // One full source at a time keeps the actual action readable on a
            // narrow phone while all physical nodes remain present in the graph.
            int current=-1;
            for(int i=0;i<relayPlan.Nodes.Count;i++) if(time>=relayPlan.Nodes[i].ActivationTime) current=i;
            if(spectacle!=null && time>=spectacle.HeroTime)
            { movieLabels[25].gameObject.SetActive(false); return; }
            if(current>=0 && currentRelaySource!=current)
            {
                currentRelaySource=current;
                RewardEvidence evidence=relayPlan.Nodes[current].Evidence;
                string label=evidence.label??"";
                string wrapped="";
                for(int start=0;start<label.Length;start+=14)
                    wrapped+=(start==0?"":"\n")+label.Substring(start,Mathf.Min(14,label.Length-start));
                EvidenceLabel(25,"Day "+evidence.day+"\n"+wrapped,new Vector3(0,2.8f,2));
                movieLabels[25].characterSize=.033f/Mathf.Max(1,Mathf.CeilToInt(label.Length/28f));
                movieLabels[25].color=Palette.Text;
            }
        }
        private void ResetCausalRelays()
        {
            relayPlan=null;
            currentRelaySource=-1;
            if(relayStage!=null) relayStage.gameObject.SetActive(false);
        }
    }
}
