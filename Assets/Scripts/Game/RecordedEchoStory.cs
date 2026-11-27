using System;

namespace Horizon.Game
{
    public enum RecordedEchoBeat { Origin, Prepare, Develop, Dock, Release, Settle }

    // The existing director owns time. This track describes one continuous
    // handoff of the original action into the reader and its result.
    public readonly struct RecordedEchoStory
    {
        public const float PrepareAt=.62f, DevelopAt=1.62f, DockAt=3.44f;
        public const float HoldAt=3.65f, ImpactAt=3.85f, RevealAt=4.08f;
        public const float SettleAt=7.52f, Duration=8.95f;
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

        public static RecordedEchoStory Sample(float seconds,bool reduced,int results=2)
        {
            float settle=SettleFor(results), duration=DurationFor(results);
            float time=Math.Max(0,Math.Min(duration,seconds));
            var beat=time<PrepareAt?RecordedEchoBeat.Origin:time<DevelopAt?RecordedEchoBeat.Prepare:
                time<DockAt?RecordedEchoBeat.Develop:time<ImpactAt?RecordedEchoBeat.Dock:
                time<settle?RecordedEchoBeat.Release:RecordedEchoBeat.Settle;
            if(reduced) return new RecordedEchoStory(beat,settle,0,1,1,0,1,1,false);
            bool held=time>=HoldAt && time<ImpactAt;
            float pose=held?HoldAt:time;
            float grip=Ease(.36f,1.02f,pose)*(1-Ease(1.75f,2.18f,pose));
            float travel=pose>=ImpactAt?1:.20f*Ease(.76f,1.35f,pose)+.40f*Ease(1.60f,2.67f,pose)+
                .26f*Ease(2.67f,3.25f,pose)+.09f*Ease(3.28f,HoldAt,pose);
            float align=.32f*Ease(2.12f,2.99f,pose)+.68f*Ease(3.14f,HoldAt,pose);
            float release=Ease(ImpactAt,ImpactAt+.11f,pose)*(1-Ease(ImpactAt+.15f,ImpactAt+.69f,pose));
            float assembly=.58f*Ease(ImpactAt+.04f,ImpactAt+.37f,pose)+.42f*Ease(ImpactAt+.37f,ImpactAt+1.12f,pose);
            return new RecordedEchoStory(beat,pose,grip,travel,align,release,assembly,Ease(settle,duration-.22f,pose),held);
        }

        public static float GuideAt(int index) => 1.78f+index*.62f;

        public static float ResultTurnAt(int slot,int count) => ImpactAt+1.56f*slot/Math.Max(1,count-1);

        public static float ResultShownAt(int slot,int count) => ResultTurnAt(slot,count)+(RevealAt-ImpactAt);

        public static float ResultContactAt(int slot,int count) =>
            ResultTurnAt(slot,count)+.91f;

        public static float ConvergenceAt(int count) => ResultContactAt(Math.Max(0,count-1),count)+.29f;

        public static float SettleFor(int count) => count>1?SettleAt:SettleAt-1.56f;

        public static float DurationFor(int count) => count>1?Duration:Duration-1.56f;

        public static float ConduitAt(int slot,int count) => ConvergenceAt(count)-.51f+.21f*slot/Math.Max(1,count-1);

        public static float CreditProgress(float time,float reveal,float contact)
        {
            if(time+.00002f>=contact) return 1;
            return .72f*Ease(reveal+.025f,reveal+.39f,time)+.19f*Ease(reveal+.39f,contact,time);
        }

        private static float Ease(float start,float end,float value) => ImmersiveMotion.Smooth((value-start)/(end-start));
    }
}
