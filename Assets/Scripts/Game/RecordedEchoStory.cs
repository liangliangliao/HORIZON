using System;

namespace Horizon.Game
{
    public enum RecordedEchoBeat { Origin, Prepare, Develop, Dock, Release, Settle }

    // The existing director owns time. This track describes one continuous
    // handoff of the original action into the reader and its result.
    public readonly struct RecordedEchoStory
    {
        public const float PrepareAt=.74f, DevelopAt=1.96f, DockAt=3.68f;
        public const float HoldAt=4.48f, ImpactAt=4.70f, RevealAt=4.93f;
        public const float SettleAt=7.10f, Duration=8.65f;
        public const int GuideCount=3;
        public const float MaterializeAfter=.14f;

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
            float grip=Ease(.36f,1.02f,pose)*(1-Ease(1.75f,2.18f,pose));
            float travel=pose>=ImpactAt?1:.20f*Ease(.89f,1.55f,pose)+.40f*Ease(1.94f,3.18f,pose)+
                .26f*Ease(3.18f,4.01f,pose)+.09f*Ease(4.02f,HoldAt,pose);
            float align=.32f*Ease(2.62f,3.43f,pose)+.68f*Ease(3.56f,HoldAt,pose);
            float release=Ease(ImpactAt,ImpactAt+.11f,pose)*(1-Ease(ImpactAt+.15f,ImpactAt+.69f,pose));
            float assembly=.58f*Ease(ImpactAt+.04f,ImpactAt+.37f,pose)+.42f*Ease(ImpactAt+.37f,ImpactAt+1.12f,pose);
            return new RecordedEchoStory(beat,pose,grip,travel,align,release,assembly,Ease(SettleAt,Duration-.22f,pose),held);
        }

        public static float GuideAt(int index) => 2.18f+index*.66f;

        public static float ResultContactAt(int slot,int count) =>
            ImpactAt+1.58f+.59f*slot/Math.Max(1,count-1);

        public static float CreditProgress(float time,float reveal,float contact)
        {
            if(time+.00002f>=contact) return 1;
            return .72f*Ease(reveal+.025f,reveal+.67f,time)+.19f*Ease(reveal+.66f,contact,time);
        }

        private static float Ease(float start,float end,float value) => ImmersiveMotion.Smooth((value-start)/(end-start));
    }
}
