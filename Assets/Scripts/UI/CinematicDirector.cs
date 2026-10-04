using System;
using System.Collections.Generic;
using Horizon.Game;
using UnityEngine;
using UnityEngine.Playables;

namespace Horizon.UI
{
    // One clock controls bodies, VFX, text, camera, audio and haptics. The playable
    // graph uses manual time so a pause cannot let one sensory layer run ahead.
    public sealed class CinematicDirector : MonoBehaviour
    {
        private sealed class Request
        {
            public RewardPlan plan;
            public Action completed;
            public bool fullscreen;
        }
        private readonly Queue<Request> queue = new Queue<Request>();
        private readonly HashSet<string> scheduled = new HashSet<string>();
        private Request active;
        private CinematicClock clock;
        private HorizonWorld3D world;
        private PlayableGraph graph;
        private Playable track;
        private bool paused;
        public RewardPlan Current { get { return active?.plan; } }
        public bool IsPlaying { get { return active != null; } }
        public float Elapsed { get { return clock?.Elapsed ?? 0; } }
        public event Action<CinematicCue> Cue;
        public event Action<RewardPlan> Started;
        public event Action<RewardPlan> Completed;
        public void Initialize(HorizonWorld3D target) { world = target; }
        public bool Enqueue(DomainEvent e, bool fullscreen = false, Action completed = null)
        {
            if (e == null || string.IsNullOrEmpty(e.id) || !scheduled.Add(e.id)) return false;
            queue.Enqueue(new Request { plan = RewardDirector.Direct(e), completed = completed, fullscreen = fullscreen });
            if (active == null) BeginNext(); return true;
        }
        private void BeginNext()
        {
            if (world == null || queue.Count == 0) return;
            active = queue.Dequeue(); clock = new CinematicClock(active.plan); clock.Pause(paused);
            graph = PlayableGraph.Create("HORIZON cinematic sensory timeline"); graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var scripted = ScriptPlayable<CinematicTrack>.Create(graph); scripted.GetBehaviour().World = world;
            track = scripted; track.SetDuration(active.plan.Duration);
            var output = ScriptPlayableOutput.Create(graph, "Synchronized cinematic frame"); output.SetSourcePlayable(track); graph.Play();
            world.BeginCinematic(active.plan, active.fullscreen); Started?.Invoke(active.plan);
        }
        public void SetPaused(bool value) { paused = value; clock?.Pause(value); world?.PauseCinematic(value); }
        private void Update()
        { Advance(Time.unscaledDeltaTime); }
        public void Advance(float delta)
        {
            if (active == null || paused) return;
            clock.Advance(delta, cue => { world.ApplyCinematicCue(cue); Cue?.Invoke(cue); });
            if (graph.IsValid()) { track.SetTime(clock.Elapsed); graph.Evaluate(0); }
            if (clock.Finished) Finish();
        }
        public void Skip()
        {
            if (active == null) return;
            clock.Skip(); Finish();
        }
        public void SkipPending()
        {
            var skipped = new List<Request>();
            if (active != null) skipped.Add(active);
            while (queue.Count > 0) skipped.Add(queue.Dequeue());
            active = null; clock = null; if (graph.IsValid()) graph.Destroy(); world?.EndCinematic(); scheduled.Clear();
            foreach (Request request in skipped) { Completed?.Invoke(request.plan); request.completed?.Invoke(); }
        }
        private void Finish()
        {
            Request ended = active; active = null; clock = null;
            if (graph.IsValid()) graph.Destroy();
            world.EndCinematic(); scheduled.Remove(ended.plan.Event.id);
            Completed?.Invoke(ended.plan); ended.completed?.Invoke();
            if (active == null) BeginNext();
        }
        public void CancelAll()
        {
            queue.Clear(); scheduled.Clear(); active = null; clock = null;
            if (graph.IsValid()) graph.Destroy(); world?.EndCinematic();
        }
        private void OnDestroy() { CancelAll(); }
    }
    public sealed class CinematicTrack : PlayableBehaviour
    {
        public HorizonWorld3D World;
        public override void PrepareFrame(Playable playable, FrameData info) { World.SampleCinematic((float)playable.GetTime()); }
    }
}
