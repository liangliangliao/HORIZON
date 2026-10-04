using System;
using System.Linq;
using Horizon.Game;
using Horizon.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Horizon
{
    public sealed partial class HorizonApp
    {
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
            Color accent = e.tier == RewardTier.Mythic ? Palette.Gold : Palette.Mint;
            View.Panel(overlay, "Movie title safe area", new Color(.008f,.02f,.04f,.86f), .025f,.735f,.975f,.865f,20);
            View.Panel(overlay, "Movie copy safe area", new Color(.008f,.02f,.04f,.94f), .035f,.195f,.965f,.39f,24);
            var caption = overlay.gameObject.AddComponent<CinematicCaption>(); caption.EventId = e.id;
            caption.Title = View.Label(overlay, "Reward event title", e.title, e.tier == RewardTier.Mythic ? 62 : 46, accent, TextAnchor.MiddleCenter, .045f,.75f,.955f,.855f);
            caption.Story = View.Label(overlay, "Reward story", e.detail,30,Palette.Text,TextAnchor.MiddleCenter,.075f,.25f,.925f,.375f);
            caption.Resources = View.Label(overlay, "Reward actual resources", "",27,Palette.Mint,TextAnchor.MiddleCenter,.075f,.202f,.925f,.25f);
            caption.Multiplier = View.Label(overlay, "Reward resilience", "",28,accent,TextAnchor.MiddleCenter,.075f,.40f,.925f,.455f);
            if (e.kind == DomainEventKind.RealityConvergence)
                View.Label(overlay, "Convergence evidence labels", "IMAGINATION · SIMULATION · REALITY",24,Palette.Gold,TextAnchor.MiddleCenter,.035f,.665f,.965f,.72f);
            caption.Flash = View.Fill(overlay, "Cinematic impact flash", Color.clear,0,.39f,1,.735f); caption.Flash.raycastTarget = false;
            Button next = View.Button(overlay, "Continue master event", "让时间继续",finish,.075f,.115f,.925f,.18f,accent,Palette.Ink,30);
            next.interactable = false; busy = true;
            View.Button(overlay, "Skip cinematic", "跳过演出 · 保留结果", () => { world.Cinematics.Skip(); next.interactable = true; busy = false; }, .53f,.035f,.925f,.092f,Palette.Panel,Palette.Text,25);
            View.Button(overlay, "Cinematic pause", "暂停 / 继续", () => SetPaused(!VisualPreferences.Paused), .075f,.035f,.49f,.092f,Palette.Panel,Palette.Text,25);
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
