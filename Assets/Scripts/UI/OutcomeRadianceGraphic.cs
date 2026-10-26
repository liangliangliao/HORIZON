using UnityEngine;
using UnityEngine.UI;

namespace Horizon.UI
{
    // A soft, open optical field around the WORDS. Not an opaque receipt card,
    // not a bitmap/asset copied from the reference. One small UI draw on GLES.
    public sealed class OutcomeRadianceGraphic : MaskableGraphic
    {
        private float age=-1;
        private bool reduced,negative;
        public void Sample(float value,Color tint,bool gentle,bool loss)
        { age=value; color=tint; reduced=gentle; negative=loss; SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); if(age<0 || color.a<=.001f) return;
            Rect r=rectTransform.rect; Vector2 center=r.center;
            float pulse=reduced?0:Mathf.Exp(-age*2.5f),turn=reduced?0:age*.055f;
            const int count=96;
            for(int i=0;i<count;i++)
            {
                float a=i*Mathf.PI*2/count,b=(i+1)*Mathf.PI*2/count;
                Band(mesh,center,r.size,a,b,.12f,.38f,0,.13f+pulse*.13f);
                Band(mesh,center,r.size,a,b,.38f,1,.13f+pulse*.13f,0);
            }
            if(reduced || negative) return;
            for(int i=0;i<24;i++)
            {
                float a=i*2.399963f+turn,width=.009f+(i%3)*.004f;
                Band(mesh,center,r.size,a-width,a+width,.35f,.72f,0,.06f+pulse*.27f);
                Band(mesh,center,r.size,a-width,a+width,.72f,1.12f,.06f+pulse*.27f,0);
            }
        }
        private void Band(VertexHelper mesh,Vector2 c,Vector2 size,float a,float b,float inner,float outer,float ai,float ao)
        {
            int n=mesh.currentVertCount;
            Add(mesh,c+Point(a,size)*inner,ai); Add(mesh,c+Point(a,size)*outer,ao);
            Add(mesh,c+Point(b,size)*outer,ao); Add(mesh,c+Point(b,size)*inner,ai);
            mesh.AddTriangle(n,n+1,n+2); mesh.AddTriangle(n,n+2,n+3);
        }
        private static Vector2 Point(float a,Vector2 size) {return new Vector2(Mathf.Cos(a)*size.x*.5f,Mathf.Sin(a)*size.y*.5f);}
        private void Add(VertexHelper mesh,Vector2 p,float alpha)
        {var v=UIVertex.simpleVert; v.position=p; Color tint=color; tint.a*=alpha; v.color=tint; mesh.AddVert(v);}
    }
}
