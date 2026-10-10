using System.IO;
using UnityEditor;
using UnityEngine;

namespace Horizon.Editor
{
    // Bake once in the editor. Players sample 16 frames from one small atlas
    // instead of simulating transparent dust clouds or reservoir fluid.
    internal static class CinematicAtlases
    {
        internal static void Ensure()
        {
            Bake("HorizonDustFlipbook",false);
            Bake("HorizonFlowFlipbook",true);
        }
        private static void Bake(string name,bool energy)
        {
            string path="Assets/Resources/"+name+".png";
            if(File.Exists(path)) return;
            const int cell=64,side=cell*4;
            var texture=new Texture2D(side,side,TextureFormat.RGBA32,false,true);
            var pixels=new Color32[side*side];
            for(int frame=0;frame<16;frame++)
            {
                float t=frame/15f;
                for(int y=0;y<cell;y++) for(int x=0;x<cell;x++)
                {
                    float u=(x+.5f)/cell*2-1,v=(y+.5f)/cell*2-1;
                    float radius=Mathf.Sqrt(u*u+v*v),angle=Mathf.Atan2(v,u);
                    float noise=Mathf.PerlinNoise(u*4+7.1f+t*1.5f,v*4+11.7f);
                    float density;
                    Color tint;
                    if(energy)
                    {
                        float winding=Mathf.Sin(angle*3-radius*14+t*Mathf.PI*2);
                        density=Mathf.Clamp01((winding*.4f+noise*.7f)*Mathf.Clamp01((1-radius)*4));
                        tint=Color.Lerp(new Color(.05f,.32f,.5f),new Color(.45f,1,.85f),density);
                    }
                    else
                    {
                        float front=.12f+t*.73f;
                        density=Mathf.Exp(-Mathf.Pow((radius-front)/(.1f+t*.12f),2))*noise*(1-t)*1.8f;
                        density*=Mathf.Clamp01((1-radius)*5);
                        tint=Color.Lerp(new Color(.21f,.16f,.12f),new Color(.82f,.67f,.44f),noise);
                    }
                    tint.a=Mathf.Clamp01(density);
                    pixels[((frame/4)*cell+y)*side+(frame%4)*cell+x]=tint;
                }
            }
            texture.SetPixels32(pixels); texture.Apply();
            File.WriteAllBytes(path,texture.EncodeToPNG()); Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Default; importer.sRGBTexture=true;
            importer.alphaSource=TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency=true; importer.mipmapEnabled=false;
            importer.wrapMode=TextureWrapMode.Clamp; importer.filterMode=FilterMode.Bilinear;
            importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.isReadable=false; importer.SaveAndReimport();
        }
    }
}
