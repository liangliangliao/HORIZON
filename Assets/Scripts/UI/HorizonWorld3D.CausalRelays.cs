using System.Linq;
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
        private readonly Transform[] relayCrowns = new Transform[CausalRelayPlan.MaximumNodes];
        private readonly Renderer[,] relayBeams = new Renderer[CausalRelayPlan.MaximumNodes,2];
        private readonly Renderer[][] relaySolids = new Renderer[CausalRelayPlan.MaximumNodes][];
        private readonly Color[][] relaySolidColors = new Color[CausalRelayPlan.MaximumNodes][];
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
                Mesh rim = Own(HorizonSculpt.Torus(.37f,.028f));
                Mesh shell=Own(HorizonSculpt.Profile("Machined causal relay",new[] {
                    new Vector2(0,-.21f),new Vector2(.31f,-.21f),new Vector2(.4f,-.13f),new Vector2(.4f,-.03f),new Vector2(.34f,.03f),new Vector2(0,.03f) },32));
                for (int i=0;i<causalRelays.Length;i++)
                {
                    Transform relay=Group("Causal relay "+i,relayStage); causalRelays[i]=relay;
                    Sculpt(relay,"Evidence metal base",shell,Vector3.zero,Vector3.one,showcaseEnamel);
                    Sculpt(relay,"Evidence bezel",rim,new Vector3(0,.02f,0),Vector3.one,gold).localRotation=Quaternion.Euler(90,0,0);
                    relayCrowns[i]=Group("Mechanical cue crown",relay);
                    Sculpt(relayCrowns[i],"Opening optical collar",rim,Vector3.zero,Vector3.one,ivory).localRotation=Quaternion.Euler(90,0,0);
                    for(int tooth=0;tooth<6;tooth++)
                    {
                        float a=tooth*Mathf.PI/3;
                        Soft(relayCrowns[i],"Cue collar tooth "+tooth,new Vector3(Mathf.Cos(a)*.36f,0,Mathf.Sin(a)*.36f),new Vector3(.065f,.11f,.065f),gold);
                    }
                    relayRings[i]=Group("Cue activation ring",relay);
                    Ring(relayRings[i],"Activation wave",new Vector3(0,.03f,0),.39f,portalLight,false);
                    for (int plane=0;plane<2;plane++)
                    {
                        Transform shaft=Shape(relay,"Director driven causal light shaft "+plane,PrimitiveType.Quad,Vector3.zero,Vector3.one,beam);
                        shaft.localRotation=Quaternion.Euler(0,plane*90,0);
                        relayBeams[i,plane]=shaft.GetComponent<Renderer>();
                    }
                    relaySolids[i]=relay.GetComponentsInChildren<Renderer>().Where(r=>!(r is LineRenderer) && !r.name.StartsWith("Director driven causal light shaft")).ToArray();
                    relaySolidColors[i]=relaySolids[i].Select(r=>r.sharedMaterial.GetColor("_Color")).ToArray();
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

        private void SampleCausalRelays(float time,ref Vector3 camera,ref Vector3 look)
        {
            if(relayPlan==null || relayStage==null || !relayStage.gameObject.activeSelf) return;
            bool final=time>=relayPlan.ImpactTime;
            camera=new Vector3(2.8f,7.2f,-7.5f); look=new Vector3(0,.15f,2);
            if(cinemaTension!=null && cinemaTension.SourceCloseup && time<cinemaTension.FirstRelease+1.10f && !preferences.reducedMotion)
            {
                CausalRelayNode source=relayPlan.Nodes[0]; Vector3 center=new Vector3(source.X,source.Y,source.Z);
                float push=Smooth(0,cinemaTension.FirstHold,tensionFrame.PoseTime);
                Vector3 eye=center+Vector3.Lerp(new Vector3(.6f,1.1f,-2.1f),new Vector3(.32f,.9f,-1.5f),push);
                camera=Vector3.Lerp(camera,eye,tensionFrame.Focus);
                look=Vector3.Lerp(look,center+Vector3.up*.08f,tensionFrame.Focus);
            }
            int columnBudget=preferences.reducedMotion?0:LowCostEffects?4:causalRelays.Length;
            for(int i=0;i<relayPlan.Nodes.Count;i++)
            {
                CausalRelayFrame frame=relayPlan.Sample(i,time,preferences.reducedMotion);
                bool primary=i==0 && cinemaTension!=null && time<cinemaTension.FirstRelease+1.10f;
                float weight=1-(primary?tensionFrame.Preview:tensionFrame.Focus)*.90f;
                for(int part=0;part<relaySolids[i].Length;part++)
                    if(weight<.999f) SetMovieColor(relaySolids[i][part].transform,relaySolidColors[i][part]*weight);
                    else ResetMovieColor(relaySolids[i][part].transform);
                Color tint=relayPlan.Nodes[i].Evidence.kind==CausalNodeKind.Action?Palette.Gold:Palette.Mint;
                SetMovieColor(movieNodes[i],(frame.Lit?Color.Lerp(tint,tint*1.6f,frame.Pulse)*frame.Fade:Palette.Muted*.25f)*weight);
                movieNodes[i].localScale=Vector3.one*(.24f+frame.Pulse*.16f);
                if(i==0 && cinemaTension!=null && cinemaTension.SourceCloseup && time<cinemaTension.FirstRelease && !preferences.reducedMotion)
                {
                    float charge=tensionFrame.Charge;
                    causalRelays[i].localScale=Vector3.one*(.92f-charge*.045f);
                    movieNodes[i].localScale=Vector3.one*(.24f+charge*.10f);
                    SetMovieColor(movieNodes[i],Color.Lerp(Palette.Muted*.25f,tint*.8f,charge));
                }
                else causalRelays[i].localScale=Vector3.one;
                relayCrowns[i].localPosition=Vector3.up*(.08f+frame.Pulse*.33f);
                relayCrowns[i].localRotation=Quaternion.Euler(0,preferences.reducedMotion?0:frame.Pulse*90,0);
                relayRings[i].localScale=Vector3.one*(1+frame.Pulse*1.1f);
                SetMovieColor(relayRings[i],(frame.Lit?tint:Palette.Muted*.15f)*frame.Fade*weight);
                if(i==0 && cinemaTension!=null && cinemaTension.SourceCloseup && time<cinemaTension.FirstRelease && !preferences.reducedMotion)
                {
                    relayCrowns[i].localPosition=Vector3.up*(.08f-tensionFrame.Charge*.055f);
                    relayCrowns[i].localRotation=Quaternion.Euler(0,tensionFrame.Charge*18,0);
                    SetMovieColor(relayRings[i],tint*(.1f+tensionFrame.Charge*.65f));
                }
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
                    renderer.transform.localScale=new Vector3(.46f+frame.Pulse*.2f,frame.Height,1);
                    relayProperties.Clear(); relayProperties.SetColor("_Color",tint);
                    relayProperties.SetFloat("_Intensity",frame.Beam*(1-tensionFrame.Preview*.94f));
                    relayProperties.SetFloat("_Clock",time); renderer.SetPropertyBlock(relayProperties);
                }
            }
            for(int i=0;i<relayPlan.Edges.Count;i++)
            {
                CausalRelayEdge edge=relayPlan.Edges[i];
                CausalRelayFrame from=relayPlan.Sample(edge.From,time,preferences.reducedMotion),to=relayPlan.Sample(edge.To,time,preferences.reducedMotion);
                bool lit=from.Lit && to.Lit;
                relayEdges[i].widthMultiplier=lit?.022f+to.Pulse*.035f:.012f;
                SetMovieColor(relayEdges[i].transform,(lit?final?Palette.Gold:Palette.Mint:Palette.Muted*.2f)*to.Fade*(1-tensionFrame.Focus*.9f));
            }
            foreach(Transform tile in movieTiles)
                if(tensionFrame.Focus>.001f) SetMovieColor(tile,floor.GetColor("_Color")*(1-tensionFrame.Focus*.8f));
                else ResetMovieColor(tile);
            // One full source at a time keeps the actual action readable on a
            // narrow phone while all physical nodes remain present in the graph.
            int current=-1;
            for(int i=0;i<relayPlan.Nodes.Count;i++) if(time>=relayPlan.Nodes[i].ActivationTime) current=i;
            if((spectacle!=null && time>=spectacle.HeroTime) || tensionFrame.Preview>.08f)
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
                int lines=1+Mathf.Max(1,Mathf.CeilToInt(label.Length/14f));
                movieLabels[25].characterSize=.05f*Mathf.Min(1,2.5f/lines);
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
