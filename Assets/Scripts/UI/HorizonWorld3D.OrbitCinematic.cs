using Horizon.Game;
using System.Linq;
using UnityEngine;

namespace Horizon.UI
{
    public sealed partial class HorizonWorld3D
    {
        private Transform orbitCinematicStage;
        private readonly Transform[] orbitSubjects = new Transform[8];
        private readonly Renderer[][] orbitSubjectRenderers = new Renderer[8][];
        private readonly AudioClip[] orbitSounds = new AudioClip[8];
        private Renderer[] orbitOverviewRenderers;
        private Transform orbitLeftTrack, orbitRightTrack, orbitLens, orbitReserveLid;
        private LineRenderer orbitAbilityBeam, orbitConnection;

        private static Color OrbitTint(int direction)
        {
            switch (direction)
            {
                case OrbitCinematic.Ability: return new Color(.28f, .72f, 1);
                case OrbitCinematic.Wealth: return new Color(1, .68f, .20f);
                case OrbitCinematic.Relationship: return new Color(.88f, .39f, .72f);
                default: return Palette.Mint;
            }
        }

        private void PrepareOrbitCinematic()
        {
            if (moviePlan.Orbit == null) return;
            if (orbitCinematicStage == null)
            {
                orbitCinematicStage = Group("Recorded future orbit installations", cinematicStage);
                Transform ability = orbitSubjects[OrbitCinematic.Ability] = Group("Ability optical bench", orbitCinematicStage);
                Box(ability, "Optical bench foot", new Vector3(0, -.51f, 0), new Vector3(1.08f, .16f, 1.02f), showcaseEnamel);
                for (int i = -1; i <= 1; i++)
                    Ring(ability, "Optical calibration rim", new Vector3(0, -.03f, i * .26f), .39f + .07f * i, gold, true);
                orbitLens = Shape(ability, "Blue focusing lens", PrimitiveType.Sphere, Vector3.zero, new Vector3(.67f, .67f, .14f), rewardCrystal);
                orbitAbilityBeam = CausalLine(ability, "Resolved optical axis", Vector3.back, Vector3.forward, portalLight, .04f);
                orbitAbilityBeam.positionCount = 2;

                Transform wealth = orbitSubjects[OrbitCinematic.Wealth] = Group("Wealth reserve chest", orbitCinematicStage);
                Box(wealth, "Reserve plinth", Vector3.down * .38f, new Vector3(1.22f, .15f, .69f), dark);
                Box(wealth, "Closed reserve", Vector3.down * .07f, new Vector3(1.03f, .47f, .58f), showcaseEnamel);
                Box(wealth, "Reserve brass fascia", new Vector3(0, -.02f, -.294f), new Vector3(.96f, .10f, .035f), gold);
                orbitReserveLid = Group("Reserve hinge", wealth); orbitReserveLid.localPosition = new Vector3(0, .21f, .26f);
                Box(orbitReserveLid, "Reserve door", Vector3.back * .27f, new Vector3(1.09f, .095f, .56f), gold);
                for (int i = 0; i < 3; i++)
                    Box(wealth, "Stored brass ingot", new Vector3((i - 1) * .275f, .16f + i * .035f, -.015f),
                        new Vector3(.22f, .11f, .28f), gold);

                Transform relation = orbitSubjects[OrbitCinematic.Relationship] = Group("Relationship bridge", orbitCinematicStage);
                Box(relation, "Bridge ground", Vector3.down * .38f, new Vector3(1.8f, .13f, .67f), dark);
                orbitLeftTrack = Group("Left relationship rail", relation);
                orbitRightTrack = Group("Right relationship rail", relation);
                for (int side = -1; side <= 1; side += 2)
                {
                    Transform rail = side < 0 ? orbitLeftTrack : orbitRightTrack;
                    Box(rail, "Separate bridge deck", new Vector3(0, -.19f, 0), new Vector3(.58f, .10f, .48f), side < 0 ? teal : pink);
                    Ring(rail, "Bridge arch", Vector3.up * .05f, .31f, side < 0 ? teal : gold, true);
                }
                orbitConnection = CausalLine(relation, "Joined relationship rails", Vector3.left, Vector3.right, portalLight, .07f);

                for (int i = 0; i < orbitSubjects.Length; i++)
                {
                    if (orbitSubjects[i] == null)
                    {
                        orbitSubjects[i] = Group("Recorded orbit orientation " + i, orbitCinematicStage);
                        Box(orbitSubjects[i], "Orientation foot", Vector3.down * .29f, new Vector3(.65f, .14f, .46f), dark);
                        Shape(orbitSubjects[i], "Orientation crystal", PrimitiveType.Sphere, Vector3.zero,
                            new Vector3(.25f + i * .025f, .51f, .25f), rewardCrystal);
                    }
                    orbitSubjectRenderers[i] = orbitSubjects[i].GetComponentsInChildren<Renderer>(true);
                }
                orbitOverviewRenderers=movieOrbit.SelectMany(node=>node.GetComponentsInChildren<Renderer>(true))
                    .Concat(movieFuture.GetComponentsInChildren<Renderer>(true)).ToArray();
            }
            orbitCinematicStage.gameObject.SetActive(true);
        }

        private void SampleOrbitCinematic(float time, ref Vector3 camera, ref Vector3 look)
        {
            OrbitCinematic orbit = moviePlan.Orbit;
            if (orbit == null) return;
            bool still = preferences.reducedMotion;
            OrbitCinematicBeat beat = orbit.BeatAt(time);
            bool overview = cinematicOverviewRequested || time >= consequenceTime;
            int bits = orbit.BitsAt(time);
            float reveal = still ? ((bits & (1 << beat.Direction)) != 0 ? 1 : 0) : Smooth(beat.Time, beat.Time + .68f, time);
            Color color = OrbitTint(beat.Direction);
            jackpotStage.gameObject.SetActive(false);
            showcaseCameraWeight = 0;
            moviePlayer.gameObject.SetActive(false);
            movieFriend.gameObject.SetActive(false);
            movieFuture.gameObject.SetActive(overview);
            movieFuture.localPosition = new Vector3(0, 0, 4.2f);
            foreach (Transform item in movieObjects) item.gameObject.SetActive(false);
            for (int i = 0; i < movieOrbit.Count; i++)
            {
                movieOrbit[i].gameObject.SetActive(true);
                SetMovieColor(movieOrbit[i], (bits & (1 << i)) != 0 ? OrbitTint(i) : Palette.Muted * .20f);
                movieOrbit[i].localScale = Vector3.one * (i == beat.Direction && !overview ? .33f : .21f);
                orbitSubjects[i].gameObject.SetActive(!overview && i == beat.Direction);
            }

            Vector3 at = movieOrbit[beat.Direction].localPosition + new Vector3(0, -.18f, -.62f);
            Transform subject = orbitSubjects[beat.Direction];
            subject.localPosition = at;
            orbitLens.localPosition = new Vector3(Mathf.Lerp(-.24f, 0, reveal), 0, Mathf.Lerp(-.25f, .10f, reveal));
            orbitLens.localRotation = Quaternion.Euler(0, 52 * (1 - reveal), 0);
            orbitAbilityBeam.gameObject.SetActive(reveal > .01f);
            orbitAbilityBeam.SetPosition(0, new Vector3(0, 0, -.58f));
            orbitAbilityBeam.SetPosition(1, new Vector3(0, 0, .56f * reveal));
            orbitReserveLid.localRotation = Quaternion.Euler(-106 * reveal, 0, 0);
            orbitLeftTrack.localPosition = Vector3.left * Mathf.Lerp(.67f, .31f, reveal);
            orbitRightTrack.localPosition = Vector3.right * Mathf.Lerp(.67f, .31f, reveal);
            orbitConnection.gameObject.SetActive(reveal > .82f);
            MovieLine(orbitConnection, orbitLeftTrack.localPosition, orbitRightTrack.localPosition);

            movieLight.transform.localPosition = overview ? new Vector3(0, 3, 3) : at + new Vector3(-.2f, .48f, -.38f);
            movieLight.color = color;
            movieLight.intensity = overview ? .62f : .5f + reveal * 1.23f;
            immersionFocus = at;
            optics.Clear();
            CurrentCinematicShot = overview ? "Recorded orbit overview" : subject.name;
            CurrentCinematicFraming = overview ? CinematicFraming.ExtremeWide : CinematicFraming.ObjectDetail;
            if (overview)
            {
                camera=new Vector3(1.8f, 3.5f, -5.8f); look=new Vector3(0, 1.35f, 4.2f);
                FitPortraitSubject(orbitOverviewRenderers,HorizonCinemaLayout.SubjectAperture,CinematicFieldOfView(time),ref camera,ref look);
                return;
            }
            Vector3 offset;
            switch (beat.Direction)
            {
                case OrbitCinematic.Ability: offset = Vector3.Lerp(new Vector3(-2, .7f, -3.1f), new Vector3(-.64f, .42f, -3.8f), reveal); break;
                case OrbitCinematic.Wealth: offset = Vector3.Lerp(new Vector3(2.2f, 2.4f, -3.1f), new Vector3(1.1f, 1.25f, -3.55f), reveal); break;
                case OrbitCinematic.Relationship: offset = Vector3.Lerp(new Vector3(.05f, 1.75f, -4.7f), new Vector3(.05f, 1.1f, -4.85f), reveal); break;
                default: offset = new Vector3(1.2f, .9f, -3.9f); break;
            }
            camera = at + (still ? new Vector3(.55f, 1.05f, -4.4f) : offset);
            look = at;
            FitPortraitSubject(orbitSubjectRenderers[beat.Direction], HorizonCinemaLayout.SubjectAperture,
                CinematicFieldOfView(time), ref camera, ref look);
        }

        public void PlayOrbitCinematicCue(float previous, float now)
        {
            if (moviePlan?.Orbit == null || paused) return;
            foreach (OrbitCinematicBeat beat in moviePlan.Orbit.Beats)
            {
                if (previous >= beat.Time || now < beat.Time || now - beat.Time > .2f) continue;
                int direction = beat.Direction;
                float frequency = direction == OrbitCinematic.Ability ? 576 : direction == OrbitCinematic.Relationship ? 392 :
                    direction == OrbitCinematic.Wealth ? 246 : 324;
                if (orbitSounds[direction] == null) orbitSounds[direction] = ShortCueTone("Orbit contact " + direction, frequency, .28f);
#if UNITY_EDITOR
                recordedSoundTime = beat.Time;
#endif
                CueSound(orbitSounds[direction], .47f);
            }
        }
    }
}
