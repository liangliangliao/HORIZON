namespace Horizon.UI
{
    // The cinematic HUD reserves a large continuous scene, while keeping both
    // original explanation and current narration visible below the film.
    public static class HorizonCinemaLayout
    {
        public const float PhaseBottom=.905f, PhaseTop=.94f;
        public const float TitleBottom=.815f, TitleTop=.905f;
        public const float StoryBottom=.715f, StoryTop=.81f;
        public const float CopyBottom=.115f, CopyTop=.205f;
        public const float PredictionBottom=.215f, PredictionTop=.295f;
        public const float MultiplierBottom=.335f, MultiplierTop=.47f;
        public const float PlaqueViewportY=.34f;
    }
}
