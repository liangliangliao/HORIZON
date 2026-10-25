using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace Horizon.UI
{
    // Keep the existing readable caption source for receipts/accessibility, and
    // render cinematic text with TMP using the project's licensed Chinese font.
    public sealed class CinematicTypography : MonoBehaviour
    {
        private static readonly int GlowColor=Shader.PropertyToID("_GlowColor");
        private static readonly int GlowOffset=Shader.PropertyToID("_GlowOffset");
        private static readonly int GlowInner=Shader.PropertyToID("_GlowInner");
        private static readonly int GlowOuter=Shader.PropertyToID("_GlowOuter");
        private static readonly int GlowPower=Shader.PropertyToID("_GlowPower");
        private static TMP_FontAsset sharedFont;
        private Text source;
        private TextMeshProUGUI rendered;
        private Material styledMaterial;
        private bool headline;
        private float lastGlow=-1,lastOutline=-1,lastTracking=float.MinValue;
        private Color lastGlowColor=new Color(-1,-1,-1,-1);
        private bool lastBold;
        private float sweep=-1;
        private TextMeshProUGUI relief;
        private Material reliefMaterial;
        public static void Emboss(Text text)
        {
            var t=text!=null?text.GetComponent<CinematicTypography>():null;
            if(t==null || t.rendered==null || t.relief!=null) return;
            RectTransform rect=View.Rect(text.transform,"Embossed text depth",0,0,1,1);
            rect.anchoredPosition=new Vector2(3,-7); rect.SetAsFirstSibling();
            t.relief=rect.gameObject.AddComponent<TextMeshProUGUI>();
            t.relief.font=t.rendered.font; t.relief.raycastTarget=false; t.relief.richText=false;
            t.relief.alignment=TextAlignmentOptions.Center; t.relief.textWrappingMode=TextWrappingModes.Normal;
            t.relief.fontStyle=FontStyles.Bold;
            t.reliefMaterial=new Material(t.rendered.fontSharedMaterial);
            t.relief.fontSharedMaterial=t.reliefMaterial; t.relief.outlineWidth=.22f;
            t.relief.outlineColor=new Color(.015f,.009f,.025f,1);
            Shader bevel=Resources.Load<Shader>("HorizonCaption");
            if(bevel!=null && bevel.isSupported)
            {
                t.styledMaterial=new Material(t.rendered.fontSharedMaterial) {shader=bevel};
                t.rendered.fontSharedMaterial=t.styledMaterial;
            }
        }
        public static void Reveal(Text text,int count)
        { var t=text!=null?text.GetComponent<CinematicTypography>():null; if(t!=null && t.rendered!=null) t.rendered.maxVisibleCharacters=count; }
        public static void Sweep(Text text,float time)
        { var t=text!=null?text.GetComponent<CinematicTypography>():null; if(t!=null) t.sweep=time; }
        public static void Upgrade(Text text,bool asHeadline=false)
        {
            if(text==null || text.font==null) return;
            CinematicTypography existing=text.GetComponent<CinematicTypography>();
            if(existing!=null) { existing.headline|=asHeadline; return; }
            // Keep the readable source caption when essential TMP resources
            // are unavailable instead of interrupting the reward continuation.
            if(TMP_Settings.instance==null) return;
            if(sharedFont==null) sharedFont=TMP_FontAsset.CreateFontAsset(text.font,64,7,GlyphRenderMode.SDFAA,1024,1024,AtlasPopulationMode.Dynamic,true);
            if(sharedFont==null) return;
            var mirror=text.gameObject.AddComponent<CinematicTypography>(); mirror.source=text; mirror.headline=asHeadline;
            RectTransform rect=View.Rect(text.transform,"Cinematic TMP text",0,0,1,1);
            mirror.rendered=rect.gameObject.AddComponent<TextMeshProUGUI>(); mirror.rendered.font=sharedFont;
            mirror.rendered.raycastTarget=false; mirror.rendered.richText=false; mirror.rendered.fontSize=text.fontSize;
            mirror.rendered.alignment=TextAlignmentOptions.Center; mirror.rendered.textWrappingMode=TextWrappingModes.Normal;
            mirror.rendered.enableAutoSizing=true; mirror.rendered.fontSizeMin=text.fontSize*.75f; mirror.rendered.fontSizeMax=text.fontSize;
            mirror.rendered.fontStyle=asHeadline?FontStyles.Bold:FontStyles.Normal;
            text.enabled=false; mirror.Copy();
        }
        public static void Present(Text text,Color glowColor,float glow,float outline,float tracking,bool bold=false)
        {
            if(text==null) return;
            CinematicTypography typography=text.GetComponent<CinematicTypography>();
            if(typography!=null) typography.SetPresentation(glowColor,glow,outline,tracking,bold);
        }
        private void SetPresentation(Color glowColor,float glow,float outline,float tracking,bool bold)
        {
            if(rendered==null) return;
            bool effectiveBold=headline || bold;
            float boundedOutline=Mathf.Clamp(outline,0,.35f);
            Color colorDelta=lastGlowColor-glowColor;
            bool changed=Mathf.Abs(lastGlow-glow)>.008f || Mathf.Abs(lastOutline-boundedOutline)>.002f ||
                Mathf.Abs(lastTracking-tracking)>.01f || lastBold!=effectiveBold ||
                colorDelta.r*colorDelta.r+colorDelta.g*colorDelta.g+colorDelta.b*colorDelta.b+colorDelta.a*colorDelta.a>.0001f;
            if(!changed) return;
            bool geometryChanged=styledMaterial==null || Mathf.Abs(lastOutline-boundedOutline)>.002f ||
                Mathf.Abs(lastTracking-tracking)>.01f || lastBold!=effectiveBold;
            lastGlow=glow; lastOutline=boundedOutline; lastTracking=tracking; lastBold=effectiveBold; lastGlowColor=glowColor;
            rendered.characterSpacing=tracking;
            rendered.fontStyle=effectiveBold?FontStyles.Bold:FontStyles.Normal;
            rendered.outlineWidth=boundedOutline;
            rendered.outlineColor=new Color(glowColor.r*.26f,glowColor.g*.22f,glowColor.b*.12f,Mathf.Clamp01(.45f+glow*.35f));
            if(styledMaterial==null)
            {
                styledMaterial=new Material(rendered.fontSharedMaterial); rendered.fontSharedMaterial=styledMaterial;
                styledMaterial.EnableKeyword("GLOW_ON");
            }
            if(styledMaterial.HasProperty(GlowColor))
                styledMaterial.SetColor(GlowColor,new Color(glowColor.r,glowColor.g,glowColor.b,Mathf.Clamp01(.2f+glow*.78f)));
            if(styledMaterial.HasProperty(GlowOffset)) styledMaterial.SetFloat(GlowOffset,-.08f);
            if(styledMaterial.HasProperty(GlowInner)) styledMaterial.SetFloat(GlowInner,.05f);
            if(styledMaterial.HasProperty(GlowOuter)) styledMaterial.SetFloat(GlowOuter,.08f+glow*.34f);
            if(styledMaterial.HasProperty(GlowPower)) styledMaterial.SetFloat(GlowPower,.72f);
            if(geometryChanged) rendered.UpdateMeshPadding();
        }
        private void Copy()
        { if(source==null || rendered==null) return; if(rendered.text!=source.text) rendered.text=source.text; rendered.color=source.color; }
        private void LateUpdate()
        {
            Copy();
            if(rendered==null) return;
            // TMP already owns mesh dirtiness. Rebuilding a static headline
            // twice per frame was costly on phones and software render capture.
            if(rendered.havePropertiesChanged) rendered.ForceMeshUpdate();
            if(relief!=null)
            {
                relief.text=rendered.text; relief.fontSize=rendered.fontSize; relief.characterSpacing=rendered.characterSpacing;
                relief.maxVisibleCharacters=rendered.maxVisibleCharacters;
                Color depth=Color.Lerp(new Color(.18f,.055f,.012f),source.color,.16f); depth.a=source.color.a;
                relief.color=depth;
            }
            if(sweep<0 || rendered==null || string.IsNullOrEmpty(rendered.text)) return;
            // A moving white specular stripe over a gold-to-amber face. This
            // follows the director clock, including pause and deterministic seek.
            var info=rendered.textInfo;
            float shine=Mathf.Repeat(sweep*.42f,1.8f)-.3f;
            for(int i=0;i<info.characterCount;i++)
            {
                var c=info.characterInfo[i]; if(!c.isVisible) continue;
                var colors=info.meshInfo[c.materialReferenceIndex].colors32;
                for(int v=0;v<4;v++)
                {
                    bool top=v==1 || v==2;
                    Color face=top?Color.Lerp(source.color,Color.white,.76f):source.color*.9f;
                    float glint=Mathf.Clamp01(1-Mathf.Abs((i+(v>=2?1:0))/(float)Mathf.Max(1,info.characterCount)-shine)/.16f);
                    face=Color.Lerp(face,Color.white,glint*.92f); face.a=source.color.a;
                    colors[c.vertexIndex+v]=face;
                }
            }
            rendered.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
        }
        private void OnDestroy()
        { if(source!=null) source.enabled=true; if(styledMaterial!=null) Destroy(styledMaterial); if(reliefMaterial!=null) Destroy(reliefMaterial); }
    }
}
