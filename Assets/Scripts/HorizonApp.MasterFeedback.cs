using System;
using System.Collections;
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
            DomainEvent e = state.events.FirstOrDefault(x => !x.acknowledged && (x.tier >= RewardTier.Epic ||
                x.kind == DomainEventKind.Comeback || x.kind == DomainEventKind.DejaVu));
            if (e == null) return false;
            PlayMasterSpectacle(e, () => { if (!TryMasterSpectacle(resume)) resume(); }); return true;
        }
        private void PlayMasterSpectacle(DomainEvent e, Action resume)
        {
            bool finished = false;
            Action finish = () => {
                if (finished) return; finished = true; e.acknowledged = true; busy = false;
                PersistMasterAction(); CloseMasterPage(); resume();
            };
            MasterPage("Master reward spectacle", "H O R I Z O N", finish);
            busy = true;
            Color accent = e.tier == RewardTier.Mythic ? Palette.Gold : Palette.Mint;
            var art = View.Rect(overlay, "Causal spectacle", 0.02f, 0.18f, 0.98f, 0.84f).gameObject.AddComponent<MasterSpectacleGraphic>();
            art.Kind = e.kind; art.color = accent; art.raycastTarget = false;
            View.Label(overlay, "Reward event title", e.title, e.tier == RewardTier.Mythic ? 73 : 52, accent,
                TextAnchor.MiddleCenter, 0.045f, 0.48f, 0.955f, 0.75f);
            View.Label(overlay, "Reward story", e.detail, 31, Palette.Text, TextAnchor.MiddleCenter, 0.075f, 0.275f, 0.925f, 0.465f);
            View.Label(overlay, "Reward resilience", "RESILIENCE CHAIN ×" + e.multiplier, 25, Palette.Muted, TextAnchor.MiddleCenter, 0.075f, 0.195f, 0.925f, 0.26f);
            Button next = View.Button(overlay, "Continue master event", "让时间继续", finish, 0.075f, 0.115f, 0.925f, 0.18f, accent, Palette.Ink, 30);
            next.interactable = false;
            world.PresentMasterEvent(e);
            StartCoroutine(EnableMasterEvent(next, viewGeneration));
        }
        private IEnumerator EnableMasterEvent(Button button, int generation)
        {
            yield return new WaitForSecondsRealtime(archive.preferences.reducedMotion ? 0.1f : 1.4f);
            if (button != null && generation == viewGeneration) { busy = false; button.interactable = true; }
        }
    }
}
