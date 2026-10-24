using System;
using System.Collections.Generic;
using System.Linq;
using Horizon.Game;
using Horizon.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Horizon
{
    public sealed partial class HorizonApp
    {
        private readonly List<GameObject> masterHiddenUI = new List<GameObject>();
        private void HideMasterBackground()
        {
            foreach (Transform child in root)
                if (child != overlay && child.gameObject.activeSelf)
                { masterHiddenUI.Add(child.gameObject); child.gameObject.SetActive(false); }
        }
        private void RestoreMasterBackground()
        {
            foreach (GameObject item in masterHiddenUI) if (item != null) item.SetActive(true);
            masterHiddenUI.Clear();
        }
        private bool TryMasterSpectacle(Action resume)
        {
            MasterRunState state = CurrentMaster; if (state == null) return false;
            // Preserve the product's feedback density: ordinary receipts stay
            // on their compact result page.  "Every animation is full-screen"
            // applies whenever a spectacle is authored; it must not convert
            // every minor bookkeeping event into another confirmation movie.
            DomainEvent e = state.events.FirstOrDefault(x => !x.acknowledged && x.tier >= RewardTier.Epic);
            if (e == null) return false;
            PlayMasterSpectacle(e, () => { if (!TryMasterSpectacle(resume)) resume(); }); return true;
        }
        private void PlayMasterSpectacle(DomainEvent e, Action resume)
        {
            bool finished = false;
            Action finish = () => { if (finished) return; finished = true; e.acknowledged = true; busy = false;
                PersistMasterAction(); CloseMasterPage(); resume(); };
            MasterPage("Master reward spectacle", "H O R I Z O N", finish);
            overlay.Find("Master shade").GetComponent<Image>().color = new Color(.009f,.023f,.04f,.025f);
            overlay.Find("Master back").gameObject.SetActive(false);
            overlay.Find("Back master").gameObject.SetActive(false);
            Color accent = e.tier == RewardTier.Mythic ? Palette.Gold : Palette.Mint;
            RectTransform heading=(RectTransform)overlay.Find("Master title");
            heading.anchorMin=new Vector2(.07f,.955f); heading.anchorMax=new Vector2(.73f,.992f);
            heading.GetComponent<Text>().fontSize=31; heading.GetComponent<Text>().color=new Color(Palette.Mint.r,Palette.Mint.g,Palette.Mint.b,.82f);
            var curtain=View.Rect(overlay,"Full-screen cinematic edge gradients",0,0,1,1).gameObject.AddComponent<CinematicCurtainGraphic>();
            curtain.raycastTarget=false;
            var caption = overlay.gameObject.AddComponent<CinematicCaption>(); caption.EventId = e.id;
            caption.Flash = View.Fill(overlay,"Cinematic impact flash",Color.clear,0,0,1,1); caption.Flash.raycastTarget=false;
            caption.Phase=View.Label(overlay,"Cinematic phase copy","",23,accent,TextAnchor.MiddleCenter,.06f,HorizonCinemaLayout.PhaseBottom,.94f,HorizonCinemaLayout.PhaseTop);
            caption.Title = View.Label(overlay, "Reward event title", e.title, e.tier == RewardTier.Mythic ? 76 : 62, accent, TextAnchor.MiddleCenter, .035f,HorizonCinemaLayout.TitleBottom,.965f,HorizonCinemaLayout.TitleTop);
            caption.Story = View.Label(overlay, "Reward story", e.detail,35,Palette.Text,TextAnchor.MiddleCenter,.055f,HorizonCinemaLayout.StoryBottom,.945f,HorizonCinemaLayout.StoryTop);
            caption.Multiplier = View.Label(overlay, "Reward resilience", "",126,Palette.Gold,TextAnchor.MiddleCenter,.07f,HorizonCinemaLayout.MultiplierBottom,.93f,HorizonCinemaLayout.MultiplierTop);
            caption.MultiplierCaption=View.Label(overlay,"Presentation multiplier meaning","",22,Palette.Gold,TextAnchor.MiddleCenter,.075f,.355f,.925f,.395f);
            caption.Explanation = View.Label(overlay, "Reward original explanation", "",25,Palette.Text,TextAnchor.MiddleCenter,.065f,.145f,.935f,.205f);
            caption.Resources = View.Label(overlay, "Reward actual resources", "",25,Palette.Mint,TextAnchor.MiddleCenter,.065f,.11f,.935f,.15f);
            if(e.receipt!=null && e.receipt.predictionRecorded)
            {
                caption.Prediction=View.Label(overlay,"Reward original prediction","",22,Palette.Text,TextAnchor.MiddleCenter,.05f,HorizonCinemaLayout.PredictionBottom,.95f,HorizonCinemaLayout.PredictionTop);
            }
            if (e.kind == DomainEventKind.RealityConvergence)
                View.Label(overlay, "Convergence evidence labels", "IMAGINATION · SIMULATION · REALITY",24,Palette.Gold,TextAnchor.MiddleCenter,.035f,.665f,.965f,.72f);
            Button next = View.Button(overlay, "Continue master event", "让时间继续",finish,.075f,.027f,.925f,.095f,accent,Palette.Ink,30);
            next.interactable=false; next.gameObject.SetActive(false); busy = true;
            Button skip=View.Button(overlay,"Skip cinematic","跳过 · 保留结果",()=> { world.Cinematics.Skip(); },.54f,.012f,.925f,.058f,new Color(Palette.Panel.r,Palette.Panel.g,Palette.Panel.b,.62f),Palette.Text,21);
            Button pause=View.Button(overlay,"Cinematic pause","暂停 / 继续",()=>SetPaused(!VisualPreferences.Paused),.075f,.012f,.46f,.058f,new Color(Palette.Panel.r,Palette.Panel.g,Palette.Panel.b,.62f),Palette.Text,21);
            caption.Bind(world.Cinematics);
            world.PresentMasterEvent(e, () => { if (next == null) return; next.gameObject.SetActive(true); next.interactable=true;
                if(skip!=null) skip.gameObject.SetActive(false); if(pause!=null) pause.gameObject.SetActive(false);
                busy=false; e.acknowledged=true; PersistMasterAction(); }, true);
            View.RefreshText(overlay);
        }
        private void BindLocalCinematicCopy()
        {
            RectTransform layer = View.Rect(root, "Reward cinematic captions", 0,0,1,1);
            var curtain=layer.gameObject.AddComponent<CinematicCurtainGraphic>(); curtain.raycastTarget=false;
            var caption = layer.gameObject.AddComponent<CinematicCaption>(); caption.HideWhenIdle=true;
            caption.Flash = View.Fill(layer, "Synchronous impact flash",Color.clear,0,0,1,1); caption.Flash.raycastTarget = false;
            caption.Phase=View.Label(layer,"Cinematic phase copy","",21,Palette.Mint,TextAnchor.MiddleCenter,.06f,.905f,.94f,.94f);
            caption.Title = View.Label(layer, "Joined reward event", "",58,Palette.Mint,TextAnchor.MiddleCenter,.04f,.815f,.96f,.905f);
            caption.Story = View.Label(layer, "Cinematic meaning", "",31,Palette.Text,TextAnchor.MiddleCenter,.06f,.715f,.94f,.81f);
            caption.Multiplier=View.Label(layer,"Reward resilience","",112,Palette.Gold,TextAnchor.MiddleCenter,.08f,.39f,.92f,.56f);
            caption.MultiplierCaption=View.Label(layer,"Presentation multiplier meaning","",21,Palette.Gold,TextAnchor.MiddleCenter,.08f,.35f,.92f,.395f);
            caption.Resources = View.Label(layer, "Physical resource copy", "",24,Palette.Gold,TextAnchor.MiddleCenter,.06f,.11f,.94f,.15f);
            caption.Explanation=View.Label(layer,"Reward original explanation","",24,Palette.Text,TextAnchor.MiddleCenter,.06f,.15f,.94f,.21f);
            caption.Bind(world.Cinematics);
        }
    }
}
