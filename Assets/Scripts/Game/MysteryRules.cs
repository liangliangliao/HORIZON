using System;
using System.Collections.Generic;

namespace Horizon.Game
{
    [Serializable]
    public sealed class MysteryRecord
    {
        public int day, sourceDay, revealDay;
        public string sourceCardId, causeNodeId, consequenceNodeId;
        public bool revealed;
        public ResourceDelta delta;
    }

    public sealed partial class GameSession
    {
        // The result is experienced before its provenance becomes visible. The
        // cause already exists in the past; no choice retroactively changes a day.
        public CausalNode ApplyMystery(int sourceDay = 0, string sourceCardId = null,
            ResourceDelta recordedDelta = null, bool legacyReceipt = false)
        {
            MysteryRecord existing = Mysteries.Find(m => m.day == Day);
            if (existing != null) return CausalNodes.Find(n => n.id == existing.consequenceNodeId);
            if (CatalogVersion < 4 || Day != 6 || HasChosen || CompletedRun != null) return null;
            ActionRecord source = sourceDay > 0 ? Actions.Find(a => a.day == sourceDay && a.cardId == sourceCardId) :
                Actions.FindLast(a => a.day < Day && a.echoed) ?? Actions.Find(a => a.day < Day);
            if (source == null || source.day >= Day) return null;
            CausalNode cause = CausalNodes.Find(n => n.id == source.nodeId);
            if (cause == null) return null;
            ResourceDelta delta = recordedDelta ?? (source.kind == CardKind.Growth ? new ResourceDelta(insight: 1) :
                source.kind == CardKind.Recovery ? new ResourceDelta(1, 1) : new ResourceDelta(mood: 1));
            if (recordedDelta == null && !legacyReceipt && !HasRoomFor(delta))
            {
                // A recovery-heavy life often has full energy and mood. Let the
                // forgotten experience become understanding instead of a blank result.
                delta = Insight < ResourceCap ? new ResourceDelta(insight: 1) :
                    Energy < ResourceCap ? new ResourceDelta(energy: 1) :
                    Mood < ResourceCap ? new ResourceDelta(mood: 1) : new ResourceDelta(ability: 1);
            }
            ResourceDelta before = Values();
            Apply(delta);
            CausalNode result = AddNode(CausalNodes, cause.id, CausalNodeKind.Mystery, Day,
                "一份还没认出来源的余力", "", true);
            result.originHidden = true;
            result.effect = Difference(before); result.effectRecorded = true;
            Mysteries.Add(new MysteryRecord { day = Day, sourceDay = source.day, sourceCardId = source.cardId,
                revealDay = 9, causeNodeId = cause.id, consequenceNodeId = result.id, delta = delta });
            return result;
        }

        private bool HasRoomFor(ResourceDelta delta)
        {
            return delta.energy > 0 && Energy < ResourceCap || delta.mood > 0 && Mood < ResourceCap ||
                delta.insight > 0 && Insight < ResourceCap || delta.ability > 0 && Ability < ResourceCap;
        }

        private void RevealMysteryOrigins()
        {
            foreach (MysteryRecord mystery in Mysteries)
            {
                if (Day < mystery.revealDay) continue;
                CausalNode node = CausalNodes.Find(n => n.id == mystery.consequenceNodeId);
                if (node != null) node.originHidden = false;
                mystery.revealed = true;
            }
        }
    }
}
