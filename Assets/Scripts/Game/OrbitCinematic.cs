using System;
using System.Collections.Generic;
using System.Linq;

namespace Horizon.Game
{
    public sealed class OrbitCinematic
    {
        public const int Ability = 1, Wealth = 2, Relationship = 3;
        public readonly IReadOnlyList<OrbitCinematicBeat> Beats;
        public readonly int PreviousBits, FinalBits;
        public readonly string ReceiptCopy;

        public static bool HasRecordedNode(DomainEvent source)
        {
            int index = source?.receipt?.orbitIndex ?? -1;
            return source?.kind == DomainEventKind.OrbitActivated && index >= 0 && index < 8 &&
                (source.receipt.orbitBits & (1 << index)) != 0;
        }

        public OrbitCinematic(IReadOnlyList<DomainEvent> events, float impact, float nextChapter)
        {
            var beats = new List<OrbitCinematicBeat>();
            var seed = events[0];
            FinalBits = events[events.Count - 1].receipt.orbitBits & 255;
            int accumulated = seed.receipt.orbitBits & ~(1 << seed.receipt.orbitIndex) & 255;
            PreviousBits = accumulated;
            float gap = Math.Min(1.42f, (nextChapter - impact) / events.Count);
            foreach (DomainEvent source in events)
            {
                int direction = source.receipt.orbitIndex;
                int bit = 1 << direction;
                if ((accumulated & bit) != 0) continue;
                beats.Add(new OrbitCinematicBeat(direction, impact + beats.Count * gap, source.id));
                accumulated |= bit;
            }
            Beats = beats.AsReadOnly();
            ReceiptCopy = string.Join(" · ", beats.Select(beat => MasterSpecification.OrbitNames[beat.Direction])) + " 已点亮";
        }

        public int BitsAt(float time)
        {
            int bits = PreviousBits;
            foreach (OrbitCinematicBeat beat in Beats)
                if (time >= beat.Time) bits |= 1 << beat.Direction;
            return bits;
        }

        public OrbitCinematicBeat BeatAt(float time)
        {
            OrbitCinematicBeat current = Beats[0];
            foreach (OrbitCinematicBeat beat in Beats)
            {
                if (time < beat.Time) break;
                current = beat;
            }
            return current;
        }
    }

    public readonly struct OrbitCinematicBeat
    {
        public readonly int Direction;
        public readonly float Time;
        public readonly string EventId;

        public OrbitCinematicBeat(int direction, float time, string eventId)
        { Direction = direction; Time = time; EventId = eventId; }
    }
}
