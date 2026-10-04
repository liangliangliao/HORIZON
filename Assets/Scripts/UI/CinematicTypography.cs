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
        private static TMP_FontAsset sharedFont;
        private Text source;
        private TextMeshProUGUI rendered;
        public static void Upgrade(Text text)
        {
            if(text==null || text.font==null || text.GetComponent<CinematicTypography>()!=null) return;
            if(sharedFont==null) sharedFont=TMP_FontAsset.CreateFontAsset(text.font,64,7,GlyphRenderMode.SDFAA,1024,1024,AtlasPopulationMode.Dynamic,true);
            if(sharedFont==null) return;
            var mirror=text.gameObject.AddComponent<CinematicTypography>(); mirror.source=text;
            RectTransform rect=View.Rect(text.transform,"Cinematic TMP text",0,0,1,1);
            mirror.rendered=rect.gameObject.AddComponent<TextMeshProUGUI>(); mirror.rendered.font=sharedFont;
            mirror.rendered.raycastTarget=false; mirror.rendered.richText=false; mirror.rendered.fontSize=text.fontSize;
            mirror.rendered.alignment=TextAlignmentOptions.Center; mirror.rendered.textWrappingMode=TextWrappingModes.Normal;
            mirror.rendered.enableAutoSizing=true; mirror.rendered.fontSizeMin=text.fontSize*.75f; mirror.rendered.fontSizeMax=text.fontSize;
            text.enabled=false; mirror.Copy();
        }
        private void Copy()
        { if(source==null || rendered==null) return; if(rendered.text!=source.text) rendered.text=source.text; rendered.color=source.color; }
        private void LateUpdate() { Copy(); }
        private void OnDestroy()
        { if(source!=null) source.enabled=true; }
    }
}
