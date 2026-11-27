using System.Collections.Generic;
using System.Linq;
using Horizon.Game;
using UnityEngine;
using UnityEngine.UI;

namespace Horizon.UI
{
    public sealed class CinematicCaption : MonoBehaviour
    {
        private sealed class TextPose
        {
            public RectTransform Rect;
            public Vector2 Position, Min, Max;
            public Vector3 Scale;
            public Color Color;
            public int Size;
        }

        public Text Phase,Title,Story,Multiplier,MultiplierCaption,OutcomeContext,Resources,Explanation,Prediction;
        public Image Flash;
        public string EventId;
        public bool HideWhenIdle;
        private readonly Dictionary<Text,TextPose> poses=new Dictionary<Text,TextPose>();
        private CinematicDirector director;
        private CinematicTextChoreography choreography;
        private RewardPlan plan;
        private Color accent;
        private readonly List<Transform> parkedPage=new List<Transform>();
        private RectTransform pageParking;
        private OutcomeRadianceGraphic outcomeLight;
        private CinematicRevealGraphic releaseField;
        private float revealSeed;
        private string echoDebit;
        private int echoGainCount;
        private LifeActionStoryboard actionStory;

        private void IsolatePage()
        {
            if(!HideWhenIdle || transform.parent==null) return;
            if(pageParking==null)
            {
                pageParking=View.Rect(transform.parent,"Cinematic page parking",0,0,1,1);
                var group=pageParking.gameObject.AddComponent<CanvasGroup>();
                group.alpha=0; group.interactable=false; group.blocksRaycasts=false;
            }
            // The receipt can be built after Started fires. Re-scan in LateUpdate
            // so late-created cards, lists and their input never cover the film.
            var siblings=new List<Transform>();
            foreach(Transform sibling in transform.parent) siblings.Add(sibling);
            foreach(Transform sibling in siblings)
            {
                if(sibling==transform || sibling==pageParking || !sibling.gameObject.activeSelf) continue;
                // One group on a full-size wrapper, never a second CanvasGroup
                // on the page item itself. Anchors stay in the same coordinate
                // space and the item's own entrance can finish normally.
                parkedPage.Add(sibling); sibling.SetParent(pageParking,false);
            }
            transform.SetAsLastSibling();
        }
        private void RestorePage()
        {
            for(int i=0;i<parkedPage.Count;i++)
                if(parkedPage[i]!=null) { parkedPage[i].SetParent(transform.parent,false); parkedPage[i].SetSiblingIndex(i); }
            parkedPage.Clear();
        }
        private void OnCancelled()
        {
            RestorePage(); plan=null; choreography=null;
            if(releaseField!=null) releaseField.Clear();
            // Clear/Close destroys the old page at end-of-frame. Detach now,
            // otherwise a new Started in that same frame can resurrect the old
            // caption and let it park the new page inside a doomed container.
            Unbind(); if(HideWhenIdle) gameObject.SetActive(false);
        }
        private void Unbind()
        {
            if(director==null) return;
            director.Started-=OnStarted; director.Cue-=OnCue; director.Completed-=OnCompleted; director.Cancelled-=OnCancelled; director=null;
        }
        private void LateUpdate() { if(plan!=null && director!=null && director.IsPlaying) IsolatePage(); }

        public void Bind(CinematicDirector value)
        {
            var lightRect=View.Rect(transform,"Outcome optical radiance",.015f,
                HorizonCinemaLayout.MultiplierBottom-.08f,.985f,HorizonCinemaLayout.MultiplierTop+.08f);
            lightRect.SetSiblingIndex(1); outcomeLight=lightRect.gameObject.AddComponent<OutcomeRadianceGraphic>(); outcomeLight.raycastTarget=false;
            releaseField=CinematicRevealGraphic.Attach((RectTransform)transform,1);
            CinematicTypography.Upgrade(Phase);
            CinematicTypography.Upgrade(Title,true);
            CinematicTypography.Upgrade(Story,true);
            CinematicTypography.Upgrade(Multiplier,true);
            CinematicTypography.Upgrade(MultiplierCaption);
            CinematicTypography.Upgrade(OutcomeContext);
            CinematicTypography.Emboss(Multiplier);
            CinematicTypography.Emboss(Title);
            CinematicTypography.Upgrade(Resources);
            CinematicTypography.Upgrade(Explanation);
            CinematicTypography.Upgrade(Prediction);
            Remember(Phase); Remember(Title); Remember(Story); Remember(Multiplier); Remember(MultiplierCaption);
            Remember(Resources); Remember(Explanation); Remember(Prediction);
            Remember(OutcomeContext);
            director=value; director.Started+=OnStarted; director.Cue+=OnCue; director.Completed+=OnCompleted;
            director.Cancelled+=OnCancelled;
            if(director.Current!=null) OnStarted(director.Current);
            else if(HideWhenIdle) gameObject.SetActive(false);
        }

        private void Remember(Text text)
        {
            if(text==null || poses.ContainsKey(text)) return;
            var rect=(RectTransform)text.transform;
            poses.Add(text,new TextPose {Rect=rect,Position=rect.anchoredPosition,Min=rect.anchorMin,Max=rect.anchorMax,
                Scale=rect.localScale,Color=text.color,Size=text.fontSize});
        }

        private void OnStarted(RewardPlan value)
        {
            if(!string.IsNullOrEmpty(EventId) && value.Event.id!=EventId) return;
            if(HideWhenIdle) gameObject.SetActive(true);
            IsolatePage();
            plan=value; choreography=new CinematicTextChoreography(value);
            revealSeed=CinematicEnergy.VisualSeed(value.Event.id);
            echoDebit=plan.RecordedEcho?plan.Outcome.EchoCosts:"";
            echoGainCount=plan.RecordedEcho?plan.Outcome.Changes.Count(change=>change.Amount>0):0;
            bool tallReceipt=echoGainCount>2;
            EchoLayout(Multiplier,tallReceipt?.232f:.293f,.423f);
            EchoLayout(MultiplierCaption,.418f,.452f);
            EchoLayout(Explanation,.149f,.189f);
            if(Multiplier!=null && poses.TryGetValue(Multiplier,out TextPose resultPose))
                CinematicTypography.Size(Multiplier,tallReceipt?Mathf.RoundToInt(resultPose.Size*2.8f/echoGainCount):resultPose.Size);
            var lightRect=outcomeLight.rectTransform;
            lightRect.anchorMin=new Vector2(.015f,plan.RecordedEcho?(tallReceipt?.18f:.223f):HorizonCinemaLayout.MultiplierBottom-.08f);
            lightRect.anchorMax=new Vector2(.985f,plan.RecordedEcho?.495f:HorizonCinemaLayout.MultiplierTop+.08f);
            actionStory=new LifeActionStoryboard(value);
            accent=value.Event.tier==RewardTier.Mythic?Palette.Gold:Palette.Mint;
            if(Title!=null) Title.text=CinematicOutcome.EventHeading(value.Event);
            if(Story!=null) Story.text="";
            if(Phase!=null) Phase.text=CinematicTextChoreography.PhaseCopy(CinematicPhase.Anticipation);
            if(Resources!=null) Resources.text="";
            if(Explanation!=null) Explanation.text="";
            if(Prediction!=null) Prediction.text=PredictionCopy(value.Event.receipt);
            if(MultiplierCaption!=null) MultiplierCaption.text="";
            if(Multiplier!=null) Multiplier.text="";
            if(Flash!=null) Flash.color=Color.clear;
            ApplyFrame(choreography.Sample(0,VisualPreferences.ReducedMotion));
        }

        private void OnCompleted(RewardPlan value)
        {
            if(!string.IsNullOrEmpty(EventId) && value.Event.id!=EventId) return;
            if(!HideWhenIdle)
            {
                // A finished movie leaves a readable result, not its nearly
                // transparent last fade frame, while the user decides to go on.
                if(choreography!=null)
                    ApplyFrame(choreography.Sample(plan.RecordedEcho?plan.Duration-.32f:choreography.SettlementTime+1.1f,VisualPreferences.ReducedMotion));
                releaseField.Clear();
                return;
            }
            releaseField.Clear(); RestorePage(); plan=null; choreography=null; gameObject.SetActive(false);
        }

        private void EchoLayout(Text text,float bottom,float top)
        {
            if(text==null || !poses.TryGetValue(text,out TextPose pose)) return;
            pose.Rect.anchorMin=plan.RecordedEcho?new Vector2(pose.Min.x,bottom):pose.Min;
            pose.Rect.anchorMax=plan.RecordedEcho?new Vector2(pose.Max.x,top):pose.Max;
        }

        public static string PredictionCopy(RewardReceipt receipt)
        {
            if(receipt==null || !receipt.predictionRecorded) return "";
            string copy="原预测 · "+AxesCopy(receipt.predicted);
            return receipt.predictionResolved?copy+"\n实际变化 · "+AxesCopy(receipt.actual):copy;
        }

        private static string AxesCopy(ResourceDelta values)
        {
            string[] names={"精力","心情","专注","金钱","关系","能力"};
            int[] axes=ResourceMath.Axes(values); var copy=new string[axes.Length];
            for(int i=0;i<axes.Length;i++) copy[i]=names[i]+(axes[i]>=0?"+":"")+axes[i];
            return string.Join("  ",copy);
        }

        private void OnCue(CinematicCue cue)
        {
            if(director.Current==null || !string.IsNullOrEmpty(EventId) && director.Current.Event.id!=EventId) return;
            // Copy and its entrance are sampled from the director clock in
            // Update, so seeking and slow frames reproduce the same typography.
        }

        private void Update()
        {
            if(VisualPreferences.Paused || director==null || director.Current==null || choreography==null) return;
            ApplyFrame(choreography.Sample(director.Elapsed,VisualPreferences.ReducedMotion));
        }

        private void ApplyFrame(CinematicTextFrame frame)
        {
            if(plan==null) return;
            if(plan.RecordedEcho)
            {
                float age=frame.Time-choreography.ImpactTime;
                float poseAge=RecordedEchoStory.Sample(frame.Time,false).PoseTime-RecordedEchoStory.ImpactAt;
                releaseField.Sample(poseAge,.28f*CinematicEnergy.RecordedEchoCharge(poseAge),
                    CinematicEnergy.RecordedEchoRelease(age),revealSeed,director.EchoRevealOrigin,true,VisualPreferences.ReducedMotion);
            }
            else releaseField.Clear();
            float second=plan.Cues.First(c=>!c.NodeHit && c.Phase==CinematicPhase.SecondReveal).Time;
            OutcomeFrame outcome=plan.RecordedEcho?
                plan.Outcome.SampleRecordedEcho(frame.Time,choreography.MultiplierTime,second,choreography.SettlementTime,VisualPreferences.ReducedMotion):
                plan.Outcome.Sample(frame.Time,choreography.MultiplierTime,second,choreography.SettlementTime,VisualPreferences.ReducedMotion);
            LifeActionFrame lifeAction=default(LifeActionFrame);
            bool actionBeforeReveal=actionStory!=null && actionStory.Enabled && frame.Time<actionStory.Reveal;
            if(actionBeforeReveal) lifeAction=actionStory.Sample(frame.Time,VisualPreferences.ReducedMotion);
            if(Phase!=null) Phase.text=outcome.Visible?(outcome.Future?
                plan.Event.receipt?.destinationDay>plan.Event.day?"接下来，回声会抵达":"变化留下的含义":"把这次变化，看清楚"):frame.PhaseCopy;
            if(Phase!=null && actionBeforeReveal)
            {
                LifeActionShot shot=lifeAction.Shot;
                Phase.text=shot==LifeActionShot.Suspense?"等一等 · 变化即将显现":shot==LifeActionShot.Establish?"看见这次行动":
                    shot==LifeActionShot.Arrive?"这一步，已经发生":"正在行动";
            }
            if(Phase!=null && plan.RecordedEcho)
            {
                RecordedEchoBeat beat=RecordedEchoStory.Sample(frame.Time,VisualPreferences.ReducedMotion).Beat;
                switch(beat)
                {
                    case RecordedEchoBeat.Origin: Phase.text="来自过去的行动"; break;
                    case RecordedEchoBeat.Prepare: Phase.text="拾起行动印记"; break;
                    case RecordedEchoBeat.Develop: Phase.text="导轨逐段接通"; break;
                    case RecordedEchoBeat.Dock: Phase.text="印记即将入槽"; break;
                    case RecordedEchoBeat.Release: Phase.text=frame.Time>=RecordedEchoStory.ResultContactAt(0,echoGainCount)-.45f?
                        "成果落位":"回声抵达"; break;
                    default: Phase.text="已经入账"; break;
                }
                if(VisualPreferences.ReducedMotion) Phase.text="已经入账";
            }
            string narration=CinematicOutcome.Readable(frame.Narration);
            if(plan.RecordedEcho && !outcome.Visible && frame.Time<choreography.MultiplierTime) narration=plan.Outcome.Origin;
            if(plan.Event.kind==DomainEventKind.TimeEcho && !outcome.Visible && plan.Event.receipt?.sourceDay>0 &&
                frame.Phase>=CinematicPhase.HeroMoment)
                narration="第 "+plan.Event.receipt.sourceDay+" 天 · 「"+PresentationCopy.Chinese(plan.Event.receipt.action)+"」\n"+narration;
            if((plan.Event.kind==DomainEventKind.Cascade || plan.Event.kind==DomainEventKind.CausalSingularity) &&
                frame.Narration==plan.Event.title) narration=plan.Outcome.Title;
            string context=outcome.Visible?string.IsNullOrWhiteSpace(outcome.Context)?plan.Outcome.Meaning:outcome.Context:narration;
            if(outcome.Visible && context==outcome.Copy)
            {
                context=plan.Outcome.Origin;
                if(string.IsNullOrWhiteSpace(context) || context==outcome.Copy)
                    context=plan.Event.day>0?"第 "+plan.Event.day+" 天 · 原始记录":"原始记录已保留";
            }
            if(plan.Outcome.WalkRecovery && outcome.HasResource)
                context=plan.Outcome.Changes[outcome.Index].Axis==0?"脚步落稳，感受身体恢复。":"慢慢呼气，让肩膀放松。";
            if(Story!=null) Story.text=plan.RecordedEcho?
                plan.Outcome.Origin+"  →  第 "+plan.Event.day+" 天":context;
            // At a release beat, reveal the new thought as one sentence and
            // let opacity/scale carry the entrance. A type-on during the flash
            // leaves isolated glyphs in screenshots and reads like corruption.
            bool wholeSentence=outcome.Visible || plan.RecordedEcho || VisualPreferences.ReducedMotion ||
                frame.Phase==CinematicPhase.Impact || frame.Phase==CinematicPhase.SecondReveal ||
                frame.Phase==CinematicPhase.Jackpot || actionBeforeReveal && lifeAction.Shot==LifeActionShot.Suspense;
            // The event heading is the viewer's fixed orientation landmark.
            // It must never inherit a partial TMP reveal from a prior frame,
            // especially while a slow device crosses the hit-stop boundary.
            CinematicTypography.Reveal(Title,int.MaxValue);
            CinematicTypography.Reveal(Story,wholeSentence?int.MaxValue:Mathf.Max(1,Mathf.CeilToInt((Story?.text?.Length??0)*frame.CharacterProgress)));
            CinematicTypography.Sweep(Multiplier,VisualPreferences.ReducedMotion?-1:outcome.Age);
            float receipt=1-frame.ReceiptVeil;
            if(Explanation!=null) Explanation.text=receipt>.001f?CinematicOutcome.Readable(plan.Event.detail):"";
            bool showDebit=plan.RecordedEcho && outcome.Visible && echoDebit.Length>0 &&
                (VisualPreferences.ReducedMotion || frame.Time>=RecordedEchoStory.SettleAt);
            if(Resources!=null) Resources.text=plan.RecordedEcho?(showDebit?"本次付出  "+echoDebit:""):
                receipt>.001f?plan.ResourceCopy:"";
            if(Multiplier!=null)
            {
                string result=outcome.Visible?outcome.Copy:"";
                Multiplier.text=plan.RecordedEcho && outcome.HasResource?result.Replace('　','\n'):result;
            }
            if(MultiplierCaption!=null) MultiplierCaption.text=outcome.Visible?outcome.Heading:"";
            if(OutcomeContext!=null) OutcomeContext.text=outcome.Visible && !outcome.Future && outcome.Index>=0?
                plan.RecordedEcho?"实际记录":(outcome.Index+1)+" / "+plan.Outcome.Changes.Count+" · 实际记录":"";

            if(OutcomeContext!=null && plan.Outcome.WalkRecovery && outcome.Visible)
            {
                if(outcome.Future) OutcomeContext.text=plan.Outcome.RecoverySummary;
                else if(outcome.Index>0) OutcomeContext.text=plan.Outcome.Changes[0].Copy()+"　·　已恢复";
            }
            if (plan.Orbit != null)
            {
                OrbitCinematicBeat beat = plan.Orbit.BeatAt(frame.Time);
                bool lit = frame.Time >= beat.Time;
                bool overview = frame.Time >= second;
                if (Phase != null) Phase.text = lit ? "方向已点亮" : "方向即将点亮";
                if (Story != null) Story.text = overview ? plan.Orbit.ReceiptCopy : MasterSpecification.OrbitNames[beat.Direction];
                if (Multiplier != null) Multiplier.text = "";
                if (MultiplierCaption != null) MultiplierCaption.text = "";
                if (OutcomeContext != null) OutcomeContext.text = "";
                CinematicTypography.Reveal(Story, int.MaxValue);
            }

            float sx=VisualPreferences.ReducedMotion?0:
                (Mathf.Sin(frame.Time*71)+Mathf.Sin(frame.Time*113)*.45f)*frame.Tremor;
            float sy=VisualPreferences.ReducedMotion?0:
                (Mathf.Cos(frame.Time*83)+Mathf.Sin(frame.Time*47)*.35f)*frame.Tremor;
            Style(Title,frame.TitleScale,new Vector2(sx*2.2f,sy*1.35f),frame.EndAlpha,
                Color.Lerp(accent,Color.white,frame.Flash*.52f),frame.Glow,.08f,8,true);
            Style(Phase,.96f+frame.CopyAlpha*.04f,new Vector2(-sx*.45f,sy*.25f),frame.CopyAlpha*frame.EndAlpha,
                Color.Lerp(accent,Palette.Gold,frame.Glow*.42f),frame.Glow*.72f,.035f,4,false);
            Style(Story,frame.CopyScale,new Vector2(sx*.68f,-sy*.42f),frame.CopyAlpha*frame.EndAlpha,
                Color.Lerp(Palette.Text,Color.white,frame.Flash*.45f),frame.Glow*.82f,.045f,2,true);
            float revealAlpha=outcome.Visible?(plan.RecordedEcho && !outcome.Future ? Mathf.Lerp(.82f,1,Mathf.Clamp01(outcome.Age/.16f)) :
                Mathf.Clamp01(outcome.Age/.16f))*frame.EndAlpha:0;
            float revealScale=1;
            if(!VisualPreferences.ReducedMotion)
                revealScale=plan.RecordedEcho?1+.28f*ImmersiveMotion.Punch(outcome.Age,.27f):
                    1+Mathf.Exp(-Mathf.Max(0,outcome.Age)/.18f)*.16f;
            Color resultColor=outcome.Negative?Palette.Coral:Palette.Gold;
            if(plan.Outcome.WalkRecovery && outcome.HasResource && plan.Outcome.Changes[outcome.Index].Axis==0)
                resultColor=Palette.Mint;
            Color radiance=resultColor; radiance.a=revealAlpha;
            if(plan.RecordedEcho) radiance.a*=.36f;
            outcomeLight?.Sample(outcome.Visible?outcome.Age:-1,radiance,VisualPreferences.ReducedMotion,outcome.Negative);
            Style(Multiplier,revealScale,Vector2.zero,revealAlpha,resultColor,.65f+frame.Glow*.35f,.16f,2,true);
            Style(MultiplierCaption,1,Vector2.zero,revealAlpha,resultColor,.35f,.05f,3,false);
            Style(OutcomeContext,1,Vector2.zero,revealAlpha,Palette.Text,.12f,.02f,0,false);
            Style(Explanation,1,Vector2.zero,receipt*frame.EndAlpha,Palette.Text,.16f,.025f,0,false);
            Style(Resources,1,Vector2.zero,(showDebit?1:receipt)*frame.EndAlpha,showDebit?Palette.Coral:Palette.Mint,.22f,.03f,1,true);
            Style(Prediction,1,Vector2.zero,receipt*frame.EndAlpha,Palette.Text,.12f,.02f,0,false);
            if(Flash!=null) Flash.color=Color.clear;
        }

        private void Style(Text text,float scale,Vector2 offset,float alpha,Color tint,float glow,float outline,float tracking,bool bold)
        {
            if(text==null || !poses.TryGetValue(text,out TextPose pose)) return;
            pose.Rect.localScale=pose.Scale*scale;
            pose.Rect.anchoredPosition=pose.Position+offset;
            tint.a=pose.Color.a*Mathf.Clamp01(alpha); text.color=tint;
            CinematicTypography.Present(text,Color.Lerp(accent,Palette.Gold,.58f),glow,outline,tracking,bold);
        }

        private void OnDestroy()
        {
            // Normal completion and cancellation restore the live page. During
            // scene teardown the page and parking group are already being destroyed.
            parkedPage.Clear();
            if(pageParking!=null && pageParking.childCount==0) Destroy(pageParking.gameObject);
            Unbind();
        }
    }

    // Transparent edge gradients preserve full-screen motion behind the copy.
    // They replace the former opaque top and bottom letterbox panels.
    public sealed class CinematicCurtainGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); Rect r=rectTransform.rect;
            Color clear=new Color(.002f,.007f,.018f,0), mid=new Color(.002f,.007f,.018f,.62f),
                deep=new Color(.002f,.007f,.018f,.94f), side=new Color(.002f,.007f,.018f,.23f);
            // Two-stage falloff gives the three top text bands stable contrast
            // without becoming an opaque card over the cinematic subject.
            Quad(mesh,new Vector2(r.xMin,r.yMin+r.height*.62f),new Vector2(r.xMax,r.yMin+r.height*.78f),clear,mid);
            Quad(mesh,new Vector2(r.xMin,r.yMin+r.height*.78f),new Vector2(r.xMax,r.yMax),mid,deep);
            Quad(mesh,new Vector2(r.xMin,r.yMin),new Vector2(r.xMax,r.yMin+r.height*.34f),deep,clear);
            SideQuad(mesh,new Vector2(r.xMin,r.yMin),new Vector2(r.xMin+r.width*.12f,r.yMax),side,clear);
            SideQuad(mesh,new Vector2(r.xMax-r.width*.12f,r.yMin),new Vector2(r.xMax,r.yMax),clear,side);
        }

        private static void Quad(VertexHelper mesh,Vector2 min,Vector2 max,Color bottom,Color top)
        {
            int n=mesh.currentVertCount;
            Add(mesh,new Vector2(min.x,min.y),bottom); Add(mesh,new Vector2(min.x,max.y),top);
            Add(mesh,new Vector2(max.x,max.y),top); Add(mesh,new Vector2(max.x,min.y),bottom);
            mesh.AddTriangle(n,n+1,n+2); mesh.AddTriangle(n,n+2,n+3);
        }
        private static void SideQuad(VertexHelper mesh,Vector2 min,Vector2 max,Color left,Color right)
        {
            int n=mesh.currentVertCount;
            Add(mesh,new Vector2(min.x,min.y),left); Add(mesh,new Vector2(min.x,max.y),left);
            Add(mesh,new Vector2(max.x,max.y),right); Add(mesh,new Vector2(max.x,min.y),right);
            mesh.AddTriangle(n,n+1,n+2); mesh.AddTriangle(n,n+2,n+3);
        }
        private static void Add(VertexHelper mesh,Vector2 p,Color color)
        { UIVertex v=UIVertex.simpleVert; v.position=p; v.color=color; mesh.AddVert(v); }
    }
}
