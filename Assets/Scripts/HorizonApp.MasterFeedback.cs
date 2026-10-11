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
            overlay.Find("Master shade").GetComponent<Image>().color = new Color(.009f,.023f,.04f,.06f);
            overlay.Find("Master back").gameObject.SetActive(false);
            overlay.Find("Back master").gameObject.SetActive(false);
            Color accent = e.tier == RewardTier.Mythic ? Palette.Gold : Palette.Mint;
            RectTransform heading=(RectTransform)overlay.Find("Master title");
            heading.anchorMin=new Vector2(.07f,.945f); heading.anchorMax=new Vector2(.73f,.988f);
            View.Panel(overlay, "Movie title safe area", new Color(.008f,.02f,.04f,.66f), .025f,HorizonCinemaLayout.TitleBottom,.975f,HorizonCinemaLayout.TitleTop,14);
            View.Panel(overlay, "Movie copy safe area", new Color(.008f,.02f,.04f,.82f), .035f,HorizonCinemaLayout.CopyBottom,.965f,HorizonCinemaLayout.CopyTop,16);
            var caption = overlay.gameObject.AddComponent<CinematicCaption>(); caption.EventId = e.id;
            caption.Title = View.Label(overlay, "Reward event title", e.title, e.tier == RewardTier.Mythic ? 62 : 46, accent, TextAnchor.MiddleCenter, .045f,.858f,.955f,.932f);
            caption.Story = View.Label(overlay, "Reward story", e.detail,28,Palette.Text,TextAnchor.MiddleCenter,.065f,.195f,.935f,.245f);
            caption.Explanation = View.Label(overlay, "Reward original explanation", e.detail,24,Palette.Text,TextAnchor.MiddleCenter,.065f,.145f,.935f,.195f);
            caption.Resources = View.Label(overlay, "Reward actual resources", "",24,Palette.Mint,TextAnchor.MiddleCenter,.065f,.108f,.935f,.145f);
            caption.Multiplier = View.Label(overlay, "Reward resilience", "",28,accent,TextAnchor.MiddleCenter,.075f,.323f,.925f,.36f);
            if(e.receipt!=null && e.receipt.predictionRecorded)
            {
                View.Panel(overlay,"Prediction receipt safe area",new Color(.008f,.02f,.04f,.85f),.035f,HorizonCinemaLayout.PredictionBottom,.965f,HorizonCinemaLayout.PredictionTop,12);
                caption.Prediction=View.Label(overlay,"Reward original prediction","",22,Palette.Text,TextAnchor.MiddleCenter,.05f,HorizonCinemaLayout.PredictionBottom,.95f,HorizonCinemaLayout.PredictionTop);
            }
            if (e.kind == DomainEventKind.RealityConvergence)
                View.Label(overlay, "Convergence evidence labels", "IMAGINATION · SIMULATION · REALITY",24,Palette.Gold,TextAnchor.MiddleCenter,.035f,.665f,.965f,.72f);
            caption.Flash = View.Fill(overlay, "Cinematic impact flash", Color.clear,0,.32f,1,.845f); caption.Flash.raycastTarget = false;
            Button next = View.Button(overlay, "Continue master event", "让时间继续",finish,.075f,.054f,.925f,.103f,accent,Palette.Ink,30);
            next.interactable = false; busy = true;
            View.Button(overlay, "Skip cinematic", "跳过演出 · 保留结果", () => { world.Cinematics.Skip(); next.interactable = true; busy = false; }, .53f,.008f,.925f,.047f,Palette.Panel,Palette.Text,25);
            View.Button(overlay, "Cinematic pause", "暂停 / 继续", () => SetPaused(!VisualPreferences.Paused), .075f,.008f,.49f,.047f,Palette.Panel,Palette.Text,25);
            caption.Bind(world.Cinematics);
            world.PresentMasterEvent(e, () => { if (next == null) return; next.interactable = true; busy = false; e.acknowledged = true; PersistMasterAction(); }, true);
            View.RefreshText(overlay);
        }
        private void BindLocalCinematicCopy()
        {
            RectTransform layer = View.Rect(root, "Reward cinematic captions", .05f,.543f,.95f,.788f);
            var caption = layer.gameObject.AddComponent<CinematicCaption>();
            caption.Title = View.Label(layer, "Joined reward event", "",29,Palette.Mint,TextAnchor.MiddleCenter,0,.82f,1,1);
            caption.Story = View.Label(layer, "Cinematic meaning", "",24,Palette.Text,TextAnchor.MiddleCenter,0,0,1,.24f);
            caption.Resources = View.Label(layer, "Physical resource copy", "",25,Palette.Gold,TextAnchor.MiddleCenter,0,.24f,1,.41f);
            caption.Flash = View.Fill(layer, "Synchronous impact flash",Color.clear,0,.41f,1,.82f); caption.Flash.raycastTarget = false;
            caption.Bind(world.Cinematics);
        }
    }
}
