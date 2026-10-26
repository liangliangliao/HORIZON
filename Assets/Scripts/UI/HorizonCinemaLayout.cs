namespace Horizon.UI
{
    // The cinematic HUD reserves a large continuous scene, while keeping both
    // original explanation and current narration visible below the film.
    public static class HorizonCinemaLayout
    {
        public const float PhaseBottom=.93f, PhaseTop=.955f;
        public const float TitleBottom=.865f, TitleTop=.925f;
        public const float StoryBottom=.80f, StoryTop=.86f;
        public static readonly UnityEngine.Rect SubjectAperture=new UnityEngine.Rect(.08f,.42f,.84f,.35f);
        public static readonly UnityEngine.Rect ImaginationAperture=new UnityEngine.Rect(.09f,.49f,.82f,.26f);
        public const float CopyBottom=.115f, CopyTop=.205f;
        public const float PredictionBottom=.215f, PredictionTop=.295f;
        public const float MultiplierBottom=.23f, MultiplierTop=.35f;
        public const float PlaqueViewportY=.34f;
    }
}
