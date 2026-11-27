using System;

namespace Horizon.Game
{
    public enum RecordedEchoBeat { Origin, Prepare, Develop, Dock, Release, Settle }

    // The existing director owns time. This track describes one continuous
    // handoff of the original action into the reader and its result.
    public readonly struct RecordedEchoStory
    {
        public const float PrepareAt=.48f, DevelopAt=1.34f, DockAt=2.39f;
        public const float HoldAt=2.72f, ImpactAt=2.90f, RevealAt=3.13f;
        public const float SettleAt=4.27f, Duration=5.55f;

        public readonly RecordedEchoBeat Beat;
        public readonly float PoseTime, Grip, Progress, Alignment, Release, Assembly, Rest;
        public readonly bool Held;

        private RecordedEchoStory(RecordedEchoBeat beat,float pose,float grip,float travel,float alignment,
            float release,float assembly,float rest,bool held)
        {
            Beat=beat; PoseTime=pose; Grip=grip; Progress=travel; Alignment=alignment;
            Release=release; Assembly=assembly; Rest=rest; Held=held;
        }

        public static RecordedEchoStory Sample(float seconds,bool reduced)
        {
            float time=Math.Max(0,Math.Min(Duration,seconds));
            var beat=time<PrepareAt?RecordedEchoBeat.Origin:time<DevelopAt?RecordedEchoBeat.Prepare:
                time<DockAt?RecordedEchoBeat.Develop:time<ImpactAt?RecordedEchoBeat.Dock:
                time<SettleAt?RecordedEchoBeat.Release:RecordedEchoBeat.Settle;
            if(reduced) return new RecordedEchoStory(beat,SettleAt,0,1,1,0,1,1,false);
            bool held=time>=HoldAt && time<ImpactAt;
            float pose=held?HoldAt:time;
            float grip=Ease(.16f,.55f,pose)*(1-Ease(.93f,1.31f,pose));
            float travel=pose>=ImpactAt?1:.18f*Ease(.48f,1.26f,pose)+.62f*Ease(1.22f,2.26f,pose)+
                .15f*Ease(2.21f,HoldAt,pose);
            float align=Ease(1.78f,HoldAt,pose);
            float release=Ease(ImpactAt,ImpactAt+.115f,pose)*(1-Ease(ImpactAt+.13f,ImpactAt+.44f,pose));
            float assembly=.53f*Ease(ImpactAt+.035f,ImpactAt+.27f,pose)+.47f*Ease(ImpactAt+.26f,4.24f,pose);
            return new RecordedEchoStory(beat,pose,grip,travel,align,release,assembly,Ease(SettleAt,Duration-.22f,pose),held);
        }

        private static float Ease(float start,float end,float value) => ImmersiveMotion.Smooth((value-start)/(end-start));
    }
}
