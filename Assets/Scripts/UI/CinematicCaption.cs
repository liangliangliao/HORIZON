using System.Collections.Generic;
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
            public Vector2 Position;
            public Vector3 Scale;
            public Color Color;
        }

        public Text Phase,Title,Story,Multiplier,MultiplierCaption,Resources,Explanation,Prediction;
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
            CinematicTypography.Upgrade(Phase);
            CinematicTypography.Upgrade(Title,true);
            CinematicTypography.Upgrade(Story,true);
            CinematicTypography.Upgrade(Multiplier,true);
            CinematicTypography.Upgrade(MultiplierCaption);
            CinematicTypography.Upgrade(Resources);
            CinematicTypography.Upgrade(Explanation);
            CinematicTypography.Upgrade(Prediction);
            Remember(Phase); Remember(Title); Remember(Story); Remember(Multiplier); Remember(MultiplierCaption);
            Remember(Resources); Remember(Explanation); Remember(Prediction);
            director=value; director.Started+=OnStarted; director.Cue+=OnCue; director.Completed+=OnCompleted;
            director.Cancelled+=OnCancelled;
            if(director.Current!=null) OnStarted(director.Current);
            else if(HideWhenIdle) gameObject.SetActive(false);
        }

        private void Remember(Text text)
        {
            if(text==null || poses.ContainsKey(text)) return;
            var rect=(RectTransform)text.transform;
            poses.Add(text,new TextPose {Rect=rect,Position=rect.anchoredPosition,Scale=rect.localScale,Color=text.color});
        }

        private void OnStarted(RewardPlan value)
        {
            if(!string.IsNullOrEmpty(EventId) && value.Event.id!=EventId) return;
            if(HideWhenIdle) gameObject.SetActive(true);
            IsolatePage();
            plan=value; choreography=new CinematicTextChoreography(value);
            accent=value.Event.tier==RewardTier.Mythic?Palette.Gold:Palette.Mint;
            if(Title!=null) Title.text=value.Event.title;
            if(Story!=null) Story.text="";
            if(Phase!=null) Phase.text=CinematicTextChoreography.PhaseCopy(CinematicPhase.Anticipation);
            if(Resources!=null) Resources.text="";
            if(Explanation!=null) Explanation.text="";
            if(Prediction!=null) Prediction.text=PredictionCopy(value.Event.receipt);
            if(MultiplierCaption!=null) MultiplierCaption.text=MultiplierContext(value);
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
                if(choreography!=null) ApplyFrame(choreography.Sample(choreography.SettlementTime+.3f,VisualPreferences.ReducedMotion));
                return;
            }
            RestorePage(); plan=null; choreography=null; gameObject.SetActive(false);
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

        private static string MultiplierContext(RewardPlan value)
        {
            if(value.PresentationMultiplier<=1) return "";
            if(value.Event.kind==DomainEventKind.Cascade || value.Event.kind==DomainEventKind.CausalSingularity)
                return "演出倍率 · 真实因果深度 "+value.Event.chainSize;
            if(value.Event.kind==DomainEventKind.Comeback || value.Event.kind==DomainEventKind.PatternBroken)
                return "演出倍率 · 韧性链 "+Mathf.Max(1,value.Event.multiplier);
            return "演出倍率 · 视觉强度，不改变实际结算";
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
            if(Phase!=null) Phase.text=frame.PhaseCopy;
            if(Story!=null) Story.text=frame.Narration;
            CinematicTypography.Reveal(Story,VisualPreferences.ReducedMotion?int.MaxValue:Mathf.Max(1,Mathf.CeilToInt(frame.Narration.Length*frame.CharacterProgress)));
            CinematicTypography.Sweep(Multiplier,VisualPreferences.ReducedMotion?-1:frame.Time-choreography.MultiplierTime);
            float receipt=1-frame.ReceiptVeil;
            if(Explanation!=null) Explanation.text=receipt>.001f?plan.Event.detail:"";
            if(Resources!=null) Resources.text=receipt>.001f?plan.ResourceCopy:"";
            if(Multiplier!=null) Multiplier.text=frame.MultiplierAlpha>.001f && plan.PresentationMultiplier>1?"×"+plan.PresentationMultiplier:"";

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
            Style(Multiplier,frame.MultiplierScale,new Vector2(sx*5.5f,sy*3.5f),frame.MultiplierAlpha,
                Color.Lerp(Palette.Gold,Color.white,frame.Flash*.66f),Mathf.Max(frame.Glow,frame.MultiplierAlpha),.13f,7,true);
            Style(MultiplierCaption,.94f+frame.MultiplierAlpha*.06f,Vector2.zero,frame.MultiplierAlpha,
                Palette.Gold,frame.Glow*.8f,.04f,3,false);
            Style(Explanation,1,Vector2.zero,receipt*frame.EndAlpha,Palette.Text,.16f,.025f,0,false);
            Style(Resources,1,Vector2.zero,receipt*frame.EndAlpha,Palette.Mint,.22f,.03f,1,true);
            Style(Prediction,1,Vector2.zero,receipt*frame.EndAlpha,Palette.Text,.12f,.02f,0,false);
            if(Flash!=null)
            {
                float alpha=Mathf.Clamp01(frame.Flash*.26f);
                Flash.color=new Color(1,.82f,.36f,alpha);
            }
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
            RestorePage(); if(pageParking!=null) Destroy(pageParking.gameObject);
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
            Color clear=new Color(.002f,.007f,.018f,0), deep=new Color(.002f,.007f,.018f,.88f), side=new Color(.002f,.007f,.018f,.23f);
            Quad(mesh,new Vector2(r.xMin,r.yMin+r.height*.62f),new Vector2(r.xMax,r.yMax),clear,deep);
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
