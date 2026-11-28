using System;

namespace Horizon.Game
{
    public enum RecordedEchoBeat { Origin, Prepare, Develop, Dock, Release, Prize, Settle }

    // The existing director owns time. This track describes one continuous
    // handoff of the original action into the reader and its result.
    public readonly struct RecordedEchoStory
    {
        public const float PrepareAt=.72f, DevelopAt=1.55f, DockAt=4.94f;
        public const float HoldAt=5.35f, ImpactAt=5.60f, PrizeAt=ImpactAt+EnergyRelease.PrizeDelay;
        public const float RevealAt=PrizeAt+.44f;
        public const float SettleAt=12.75f, Duration=14.55f;
        public const int GuideCount=3;
        public const float MaterializeAfter=.20f;
        private const float ResultSpacing=2.56f;

        public readonly RecordedEchoBeat Beat;
        public readonly float PoseTime, Grip, Progress, Alignment, Release, Assembly, Rest;
        public readonly float ReaderLoad;
        public readonly bool Held;

        private RecordedEchoStory(RecordedEchoBeat beat,float pose,float grip,float travel,float alignment,
            float release,float assembly,float rest,float readerLoad,bool held)
        {
            Beat=beat; PoseTime=pose; Grip=grip; Progress=travel; Alignment=alignment;
            Release=release; Assembly=assembly; Rest=rest; ReaderLoad=readerLoad; Held=held;
        }

        public static RecordedEchoStory Sample(float seconds,bool reduced,int results=2)
        {
            float settle=SettleFor(results), duration=DurationFor(results);
            float time=Math.Max(0,Math.Min(duration,seconds));
            var beat=time<PrepareAt?RecordedEchoBeat.Origin:time<DevelopAt?RecordedEchoBeat.Prepare:
                time<DockAt?RecordedEchoBeat.Develop:time<ImpactAt?RecordedEchoBeat.Dock:
                time<PrizeAt?RecordedEchoBeat.Release:time<settle?RecordedEchoBeat.Prize:RecordedEchoBeat.Settle;
            if(reduced) return new RecordedEchoStory(beat,settle,0,1,1,0,1,1,0,false);
            bool held=time>=HoldAt && time<ImpactAt;
            float pose=held?HoldAt:time;
            float grip=Ease(.36f,1.02f,pose)*(1-Ease(1.75f,2.18f,pose));
            float travel=pose>=ImpactAt?1:.20f*Ease(.84f,1.48f,pose)+.40f*Ease(1.55f,3.38f,pose)+
                .26f*Ease(3.38f,4.57f,pose)+.09f*Ease(4.57f,HoldAt,pose);
            float align=.32f*Ease(2.12f,4.19f,pose)+.68f*Ease(4.21f,HoldAt,pose);
            float release=Ease(ImpactAt,ImpactAt+.11f,pose)*(1-Ease(ImpactAt+.15f,ImpactAt+.69f,pose));
            float assembly=.58f*Ease(ImpactAt+.04f,ImpactAt+.37f,pose)+.42f*Ease(ImpactAt+.37f,ImpactAt+1.12f,pose);
            return new RecordedEchoStory(beat,pose,grip,travel,align,release,assembly,Ease(settle,duration-.22f,pose),
                ReaderAt(pose).Load,held);
        }

        public static float GuideAt(int index) => 1.81f+index*1.17f;

        internal static (float Load, float Charge) ReaderAt(float time)
        {
            const float braced = .76f;
            if(time>=ImpactAt) return (braced*(1-Ease(ImpactAt,ImpactAt+.15f,time)),0);

            float pose=Math.Min(time,HoldAt);
            float brace=Ease(GuideAt(GuideCount-1)+.59f,HoldAt,pose);
            float load=braced*brace;
            float charge=.025f*Ease(.54f,1.14f,pose)+.29f*brace;
            for(int i=0;i<GuideCount;i++)
            {
                float contact=GuideAt(i);
                load+=(.28f+.265f*i)*Ease(contact-.30f,contact+.09f,pose)*
                    (1-Ease(contact+.27f,contact+.59f,pose));
                // The rim responds after the reader takes the load. Each contact
                // leaves a small amount of light after the leaf springs back.
                charge+=(.062f+.025f*i)*Ease(contact,contact+.54f,pose)+(.105f+.185f*i)*
                    Ease(contact-.05f,contact+.17f,pose)*(1-Ease(contact+.37f,contact+.71f,pose));
            }
            return (load,charge);
        }

        public static float ResultTurnAt(int slot,int count) => PrizeAt+ResultSpacing*Math.Min(slot,Math.Max(0,count-1));

        public static float ResultShownAt(int slot,int count) => ResultTurnAt(slot,count)+(RevealAt-PrizeAt);

        public static float ResultContactAt(int slot,int count) =>
            ResultTurnAt(slot,count)+1.57f;

        public static float ConvergenceAt(int count) => ResultContactAt(Math.Max(0,count-1),count)+.38f;

        public static float SettleFor(int count) => SettleAt+SlotsOffset(count);

        public static float DurationFor(int count) => Duration+SlotsOffset(count);

        private static float SlotsOffset(int count) => (Math.Max(1,count)-2)*ResultSpacing;

        public static float ConduitAt(int slot,int count) => ConvergenceAt(count)-.51f+.21f*slot/Math.Max(1,count-1);

        public static float CreditProgress(float time,float reveal,float contact)
        {
            if(time+.00002f>=contact) return 1;
            return .72f*Ease(reveal+.025f,reveal+.39f,time)+.19f*Ease(reveal+.39f,contact,time);
        }

        private static float Ease(float start,float end,float value) => ImmersiveMotion.Smooth((value-start)/(end-start));
    }
}
