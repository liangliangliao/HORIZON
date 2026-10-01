using Horizon.Game;
using Horizon.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Horizon
{
    public sealed partial class HorizonApp
    {
        private void ShowObservationChapter(int chapter)
        {
            if (chapter < 1 || chapter > archive.journey.Chapter) return;
            if (archive.journey.readChapters.Contains(chapter)) { ShowChapterExercise(chapter); return; }
            int beat = archive.journey.storyChapter == chapter ? archive.journey.storyBeat : 0;
            ShowJourneyStory(chapter, Mathf.Clamp(beat, 0, 2));
        }
        private void ShowJourneyStory(int chapter, int beat)
        {
            if (chapter > archive.journey.Chapter) return;
            archive.journey.storyChapter = chapter; archive.journey.storyBeat = beat; Save();
            DismissStoryPage();
            overlay = View.Rect(root, "Journey story", 0, 0, 1, 1);
            RectTransform page = overlay;
            world.ShowStation(beat, chapter == 7 || session?.HorizonLevel >= 3);
            View.Fill(overlay, "Story atmosphere", new Color(0.008f, 0.023f, 0.042f, 0.43f), 0, 0, 1, 1, true);
            View.Fill(overlay, "Story header shade", Palette.Ink, 0, 0.755f, 1, 1, true);
            View.Fill(overlay, "Story footer shade", Palette.Ink, 0, 0, 1, 0.445f, true);
            View.Label(overlay, "Story chapter", "第" + chapter + "次回望 · " + JourneyProgress.Name(chapter), 37, Palette.Mint,
                TextAnchor.MiddleCenter, 0.05f, 0.838f, 0.95f, 0.948f);
            View.Label(overlay, "Story page", (beat + 1) + " / 3", 24, Palette.Muted,
                TextAnchor.MiddleCenter, 0.08f, 0.78f, 0.92f, 0.84f);
            View.Panel(overlay, "Story readable plate", Palette.Panel, 0.045f, 0.13f, 0.955f, 0.42f, 34);
            View.Label(overlay, "Story voice", JourneyStory.Voice(chapter, beat), 34, Palette.Text,
                TextAnchor.MiddleCenter, 0.085f, 0.277f, 0.915f, 0.4f);
            View.Label(overlay, "Story memory", JourneyStory.Memory(archive, chapter), 27, Palette.Muted,
                TextAnchor.MiddleCenter, 0.085f, 0.146f, 0.915f, 0.281f);
            View.Button(overlay, "Next story beat", beat < 2 ? "听他继续说" : chapter == 7 ? "走向三十天" : "亲手试试看", () => {
                if (overlay != page || !page.gameObject.activeSelf) return;
                if (beat < 2) { ShowJourneyStory(chapter, beat + 1); return; }
                if (!archive.journey.readChapters.Contains(chapter)) archive.journey.readChapters.Add(chapter);
                archive.journey.storyChapter = archive.journey.storyBeat = 0; Save();
                if (chapter == 7) {
                    DismissStoryPage(); world.ShowBoard(); ShowJourney();
                } else { world.ShowBoard(); ShowChapterExercise(chapter); }
            }, 0.11f, 0.062f, 0.89f, 0.125f, Palette.Mint, Palette.Ink, 29);
            View.Button(overlay, "Leave story", "留到下次，再回望", () => {
                if (overlay != page || !page.gameObject.activeSelf) return;
                DismissStoryPage(); world.ShowBoard(); ShowJourney();
            }, 0.18f, 0.01f, 0.82f, 0.053f, Palette.Deep, Palette.Muted, 23);
            View.RefreshText(overlay);
        }

        private void DismissStoryPage()
        {
            if (overlay == null) return;
            // Unity defers destruction until the end of the frame. Retire input
            // immediately so a second tap cannot operate the previous page.
            overlay.gameObject.SetActive(false);
            Destroy(overlay.gameObject);
            overlay = null;
        }
    }
}
