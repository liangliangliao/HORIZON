using System;
using System.Collections.Generic;
using UnityEngine;

namespace Horizon.UI
{
    public static class HorizonRewardMeshes
    {
        public static Mesh Star(float radius = .5f, float depth = .08f)
        {
            var vertices = new Vector3[22]; var triangles = new List<int>();
            for (int face = 0; face < 2; face++)
            {
                int offset = face * 11; vertices[offset] = new Vector3(0,0,(face==0?-1:1)*depth);
                for (int i=0;i<10;i++)
                { float a=Mathf.PI*.5f+i*Mathf.PI/5, r=i%2==0?radius:radius*.43f; vertices[offset+i+1]=new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r,(face==0?-1:1)*depth); }
                for(int i=0;i<10;i++)
                { int next=(i+1)%10+1; triangles.Add(offset); triangles.Add(offset+(face==0?next:i+1)); triangles.Add(offset+(face==0?i+1:next)); }
            }
            for(int i=0;i<10;i++)
            { int a=i+1,b=(i+1)%10+1; triangles.AddRange(new[] { a,b,b+11,a,b+11,a+11 }); }
            var mesh=new Mesh { name="HORIZON embossed five point star",vertices=vertices,triangles=triangles.ToArray() };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }

        public static Mesh Coin()
        {
            Mesh disc=HorizonSculpt.Profile("Bevelled coin",new[] { new Vector2(0,-.045f),new Vector2(.25f,-.045f),new Vector2(.285f,-.025f),
                new Vector2(.285f,.025f),new Vector2(.25f,.045f),new Vector2(0,.045f) },24);
            Mesh seal=Star(.18f,.014f), rim=HorizonSculpt.Torus(.23f,.012f);
            var pieces=new List<CombineInstance> { new CombineInstance { mesh=disc,transform=Matrix4x4.Rotate(Quaternion.Euler(90,0,0)) } };
            for(int side=-1;side<=1;side+=2)
            {
                pieces.Add(new CombineInstance { mesh=seal,transform=Matrix4x4.Translate(new Vector3(0,0,side*.049f)) });
                pieces.Add(new CombineInstance { mesh=rim,transform=Matrix4x4.Translate(new Vector3(0,0,side*.045f)) });
            }
            var mesh=new Mesh { name="HORIZON gold coin with embossed seal" }; mesh.CombineMeshes(pieces.ToArray(),true,true);
            Release(disc); Release(seal); Release(rim); return mesh;
        }

        private static void Release(UnityEngine.Object value)
        { if(Application.isPlaying) UnityEngine.Object.Destroy(value); else UnityEngine.Object.DestroyImmediate(value); }
    }

    public sealed class HorizonRewardDigits
    {
        [Serializable] private sealed class Atlas { public Glyph[] glyphs; }
        [Serializable] private sealed class Glyph { public string character; public float advance; public float[] vertices; public int[] triangles; }
        private readonly Dictionary<char,Mesh> meshes=new Dictionary<char,Mesh>();
        private readonly Dictionary<char,float> advances=new Dictionary<char,float>();
        public HorizonRewardDigits(Action<Mesh> own)
        {
            TextAsset data=Resources.Load<TextAsset>("HorizonRewardDigits");
            if(data==null) return;
            foreach(Glyph glyph in JsonUtility.FromJson<Atlas>(data.text).glyphs)
            {
                var vertices=new Vector3[glyph.vertices.Length/3];
                for(int i=0;i<vertices.Length;i++) vertices[i]=new Vector3(glyph.vertices[i*3],glyph.vertices[i*3+1],glyph.vertices[i*3+2]);
                var mesh=new Mesh { name="HORIZON extruded gold "+glyph.character,vertices=vertices,triangles=glyph.triangles };
                mesh.RecalculateNormals(); mesh.RecalculateBounds(); own(mesh);
                meshes.Add(glyph.character[0],mesh); advances.Add(glyph.character[0],glyph.advance);
            }
        }
        public Mesh Mesh(char value) { return meshes.TryGetValue(value,out Mesh mesh)?mesh:null; }
        public float Advance(char value) { return advances.TryGetValue(value,out float width)?width:.6f; }
        public float Width(string text) { float width=0; foreach(char value in text) width+=Advance(value); return width; }
    }
}
