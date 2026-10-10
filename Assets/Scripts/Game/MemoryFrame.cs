using System;
using System.Collections.Generic;
using System.Linq;

namespace Horizon.Game
{
    // Small, portable scene thumbnails travel with receipts and saves. No paths,
    // live Unity objects or mutable world state are stored in a reward.
    [Serializable]
    public sealed class MemoryFrame
    {
        public const int MaximumFrames = 5, MaximumEncodedLength = 12288;
        public string runId, actionKey, jpeg;
        public int beatIndex, width, height;
        public ImaginePhase phase;
        public MemoryFrame Copy() { return (MemoryFrame)MemberwiseClone(); }
        public bool IsValid => !string.IsNullOrEmpty(runId) && runId.Length<=160 && beatIndex>=0 && beatIndex<64 &&
            Enum.IsDefined(typeof(ImaginePhase),phase) && width>0 && width<=192 && height>0 && height<=128 &&
            !string.IsNullOrEmpty(jpeg) && jpeg.Length<=MaximumEncodedLength;
        public static List<MemoryFrame> CopyFrames(IEnumerable<MemoryFrame> frames)
        { return (frames ?? Enumerable.Empty<MemoryFrame>()).Where(f=>f!=null && f.IsValid).Take(MaximumFrames).Select(f=>f.Copy()).ToList(); }
        public static void Record(ImagineRun run, MemoryFrame frame)
        {
            if(run==null || frame==null || !frame.IsValid || frame.runId!=run.id ||
                frame.beatIndex>=run.timeline.Count || run.timeline[frame.beatIndex].phase!=frame.phase) return;
            run.frames=CopyFrames(run.frames);
            run.frames.RemoveAll(f=>f.phase==frame.phase);
            run.frames.Add(frame.Copy());
            run.frames=run.frames.OrderBy(f=>f.beatIndex).TakeLastPortable(MaximumFrames).ToList();
            if(frame.phase==ImaginePhase.Adjust && run.memories.Count>0)
                run.memories[run.memories.Count-1].frame=frame.Copy();
        }
    }
}
