using Horizon.Game;
using Horizon.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Horizon
{
    public sealed partial class HorizonApp
    {
        private void ShowRewardCollection()
        {
            Save(); MasterPage("Reward collection", "留下的因果 · 纪念物", ShowMasterHub);
            View.Label(overlay, "Memento purpose", "每件纪念物都来自已经发生的突破、回声或现实行动。", 28, Palette.Muted,
                TextAnchor.MiddleLeft, .075f,.77f,.925f,.86f);
            RectTransform viewport=View.Rect(overlay,"Memento scroll",.075f,.16f,.925f,.76f);
            viewport.gameObject.AddComponent<RectMask2D>(); viewport.gameObject.AddComponent<Image>().color=Color.clear;
            var scroll=viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport=viewport; scroll.horizontal=false;
            RectTransform content=View.Rect(viewport,"Collected milestones",0,1,1,1); content.pivot=new Vector2(.5f,1);
            int count=archive.rewardCollection.items.Count; content.sizeDelta=new Vector2(0,Mathf.Max(700,count*194)); scroll.content=content;
            if(count==0) View.Label(content,"No mementos yet","当一条真实因果链抵达，纪念物会留在这里。",31,Palette.Text,TextAnchor.UpperLeft,0,0,1,1);
            for(int i=0;i<count;i++)
            {
                RewardMemento item=archive.rewardCollection.items[count-1-i];
                RectTransform row=View.Panel(content,"Memento "+item.id,Palette.Panel,0,1,1,1,20).rectTransform;
                row.pivot=new Vector2(.5f,1); row.sizeDelta=new Vector2(0,176); row.anchoredPosition=new Vector2(0,-i*194);
                View.Label(row,"Memento title",item.title.Replace("\n"," "),30,Palette.Gold,TextAnchor.MiddleLeft,.035f,.61f,.965f,.94f);
                View.Label(row,"Memento provenance",(item.run==0?"现实星座":"人生 "+item.run+" · D"+item.day)+" · "+MementoName(item.kind),24,Palette.Mint,TextAnchor.MiddleLeft,.035f,.39f,.965f,.62f);
                View.Label(row,"Memento meaning",item.detail,25,Palette.Text,TextAnchor.MiddleLeft,.035f,.05f,.965f,.4f);
                View.Button(row,"Inspect memento "+item.id,"查看",()=>ShowMemento(item),.80f,.63f,.965f,.93f,Palette.Deep,Palette.Mint,23);
            }
        }
        private void ShowMemento(RewardMemento item)
        {
            MasterPage("Memento object",MementoName(item.kind),ShowRewardCollection);
            overlay.Find("Master shade").GetComponent<Image>().color=new Color(.009f,.023f,.04f,.06f);
            world.ShowMementoScene(item.kind);
            View.Panel(overlay,"Memento safe copy",Palette.Panel,.05f,.18f,.95f,.41f,24);
            View.Label(overlay,"Memento event",item.title,38,Palette.Gold,TextAnchor.MiddleCenter,.075f,.31f,.925f,.40f);
            View.Label(overlay,"Memento original meaning",item.detail,29,Palette.Text,TextAnchor.MiddleCenter,.075f,.19f,.925f,.31f);
        }
        private static string MementoName(RewardObjectKind kind)
        {
            switch(kind)
            {
                case RewardObjectKind.BrokenLink:return "断裂链环";
                case RewardObjectKind.RealityMilestone:return "现实里程碑";
                case RewardObjectKind.ConvergencePrism:return "三棱汇流体";
                case RewardObjectKind.MemoryFilm:return "未来记忆胶片";
                case RewardObjectKind.HorizonLens:return "远望装置";
                case RewardObjectKind.OrbitNode:return "未来节点";
                default:return "因果链纪念物";
            }
        }
    }
}
