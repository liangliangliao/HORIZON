using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Horizon.UI
{
    public static class HorizonPortraitRenderer
    {
        // A URP base camera clears its target. Compose the real scene render as
        // a canvas background before rendering UI, preserving a partial viewport
        // and the phone's full text layer. No pipeline switch or fake screenshot.
        public static void Render(HorizonWorld3D world, Camera ui, Canvas canvas, RenderTexture output)
        {
            if (GraphicsSettings.currentRenderPipeline == null)
            { world.BackgroundCamera.Render(); world.WorldCamera.Render(); ui.Render(); return; }
            Camera scene = world.WorldCamera; Rect rect = scene.rect; RenderTexture prior = scene.targetTexture;
            var sceneImage = RenderTexture.GetTemporary(Mathf.Max(16, Mathf.RoundToInt(output.width * rect.width)),
                Mathf.Max(16, Mathf.RoundToInt(output.height * rect.height)), 24, RenderTextureFormat.Default);
            GameObject composite = null;
            CameraClearFlags clear = ui.clearFlags; Color background = ui.backgroundColor;
            try
            {
                scene.rect = new Rect(0,0,1,1); scene.targetTexture = sceneImage; scene.Render();
                scene.rect = rect; scene.targetTexture = prior;
                RectTransform backdrop = View.Rect(canvas.transform, "Portrait scene composition", rect.xMin, rect.yMin, rect.xMax, rect.yMax);
                composite = backdrop.gameObject; composite.layer = 5; backdrop.SetAsFirstSibling();
                RawImage image = composite.AddComponent<RawImage>(); image.texture = sceneImage; image.raycastTarget = false;
                ui.clearFlags = CameraClearFlags.SolidColor; ui.backgroundColor = Palette.Ink;
                Canvas.ForceUpdateCanvases(); ui.Render();
            }
            finally
            {
                scene.rect = rect; scene.targetTexture = prior; ui.clearFlags = clear; ui.backgroundColor = background;
                if (composite != null) { composite.SetActive(false); Object.DestroyImmediate(composite); }
                RenderTexture.ReleaseTemporary(sceneImage);
            }
        }
    }
}
