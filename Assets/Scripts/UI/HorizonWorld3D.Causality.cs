using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Horizon.Game;
using UnityEngine;

namespace Horizon.UI
{
    public sealed partial class HorizonWorld3D
    {
        private readonly List<Dictionary<string, Transform>> causalMemories = new List<Dictionary<string, Transform>>();
        private Transform ghostGroup;
        private int causalAnimation;

        public void ShowCausalMemories(List<MemoryChain> chains)
        {
            touchGeneration++; causalAnimation++;
            if (memoryGroup != null) { memoryGroup.gameObject.SetActive(false); Dispose(memoryGroup.gameObject); }
            memoryGroup = Group("Your actual causal memories");
            memoryOrbs.Clear(); causalMemories.Clear();
            for (int i = 0; i < chains.Count; i++)
            {
                MemoryChain chain = chains[i];
                Transform branch = Group("Chain from D" + chain.Origin.day, memoryGroup);
                branch.localPosition = new Vector3((i - (chains.Count - 1) * 0.5f) * 2.35f, 1.05f, 5.15f);
                var positions = new Dictionary<string, Transform>(); causalMemories.Add(positions);
                int first = chain.Nodes.Select(n => n.day).DefaultIfEmpty(chain.Origin.day).Min();
                int last = chain.Nodes.Select(n => n.day).DefaultIfEmpty(first).Max();
                foreach (CausalNode node in chain.Nodes)
                {
                    int rank = chain.Nodes.Where(n => n.day == node.day).ToList().IndexOf(node);
                    int count = chain.Nodes.Count(n => n.day == node.day);
                    Vector3 at = new Vector3((rank - (count - 1) * 0.5f) * 0.48f,
                        last == first ? 0 : (node.day - first) / (float)(last - first) * 1.8f, rank * 0.12f);
                    CardSpec card = CardCatalog.FindById(node.cardId);
                    Transform token = IntentionSymbol(card?.Kind ?? chain.Origin.kind, Vector3.zero, card?.GivesSupport ?? false,
                        node.type == CausalNodeKind.Echo && node.resolved);
                    token.SetParent(branch, false); token.localPosition = at;
                    token.name = "Memory node " + node.id + " D" + node.day;
                    token.localScale = Vector3.one * (node.resolved ? 0.22f : 0.16f);
                    positions[node.id] = token;
                    Ring(branch, node.resolved ? "Arrived" : "Still travelling", at, node.resolved ? 0.22f : 0.16f,
                        node.resolved ? portalLight : glass, true);
                }
                if (positions.TryGetValue(chain.Origin.nodeId, out Transform origin)) memoryOrbs.Add(origin);
                else memoryOrbs.Add(branch);
                foreach (CausalNode node in chain.Nodes)
                    foreach (string parent in CausalGraph.ObservedParents(node))
                        if (positions.ContainsKey(parent))
                            CausalLine(branch, "Actual cause " + parent + " to " + node.id,
                                positions[parent].localPosition, positions[node.id].localPosition,
                                node.resolved ? portalLight : glass, node.resolved ? 0.025f : 0.012f);
            }
        }

        private LineRenderer CausalLine(Transform parent, string name, Vector3 from, Vector3 to, Material material, float width)
        {
            var line = new GameObject(name, typeof(LineRenderer)).GetComponent<LineRenderer>();
            line.transform.SetParent(parent, false); line.useWorldSpace = false; line.sharedMaterial = material;
            line.widthMultiplier = width; line.positionCount = 14;
            for (int point = 0; point < 14; point++)
            { float t = point / 13f; line.SetPosition(point, Vector3.Lerp(from, to, t) + Vector3.forward * Mathf.Sin(t * Mathf.PI) * 0.14f); }
            return line;
        }

        public void RevealMemoryNode(int chain, CausalNode node)
        {
            if (!station || chain < 0 || chain >= causalMemories.Count ||
                !causalMemories[chain].TryGetValue(node.id, out Transform token)) return;
            ++causalAnimation;
            foreach (Transform other in causalMemories[chain].Values) other.localScale = Vector3.one * 0.16f;
            token.localScale = Vector3.one * 0.3f;
            Tick(Mathf.Min(4, node.depth));
            if (!preferences.reducedMotion)
            { WorldRipple(token.position, portalLight, 0.3f); Burst(token.position, Palette.Mint, 8); }
            futureSelf.GetComponent<HorizonActor>().Pointing = true;
        }

        public void BeginGhostStory(GhostStory story)
        {
            EndCausalPresentation();
            ShowGhost(story.Beats[0].Day);
            ghostGroup = Group("A genuinely replayed alternative");
            for (int day = story.Beats[0].Day; day <= GameSession.RunLength(story.Original); day++)
            {
                Vector3 point = DayPoint(day) + Vector3.up * 0.65f;
                ActionRecord action = story.Alternative.actions[day - 1];
                Transform token = IntentionSymbol(action.kind, Vector3.zero, action.givesSupport, false);
                token.SetParent(ghostGroup, false); token.localPosition = point; token.localScale = Vector3.one * 0.14f;
                token.name = "Ghost day " + day;
                if (day > story.Beats[0].Day) CausalLine(ghostGroup, "Ghost route to D" + day,
                    DayPoint(day - 1) + Vector3.up * 0.65f, point, glass, 0.018f);
            }
            WorldCamera.rect = new Rect(0, 0.48f, 1, 0.36f);
        }

        public void GhostTravels(int from, int to)
        { StartCoroutine(TravelGhost(from, to, ++causalAnimation)); }

        private IEnumerator TravelGhost(int from, int to, int generation)
        {
            var actor = futureSelf.GetComponent<HorizonActor>();
            actor.Walking = !preferences.reducedMotion; actor.Pointing = false;
            float duration = preferences.reducedMotion ? 0 : Mathf.Min(1.2f, 0.35f + Mathf.Abs(to - from) * 0.09f);
            for (float age = 0; age < duration; age += Mathf.Min(Time.unscaledDeltaTime, 0.1f))
            {
                while (paused && generation == causalAnimation) yield return null;
                if (generation != causalAnimation || ghostGroup == null) yield break;
                float day = Mathf.Lerp(from, to, Mathf.SmoothStep(0, 1, age / duration));
                Vector3 at = Vector3.Lerp(DayPoint(Mathf.FloorToInt(day)), DayPoint(Mathf.CeilToInt(day)), day - Mathf.Floor(day));
                futureSelf.position = at + new Vector3(0.45f, -0.09f, 0.3f);
                cameraPosition = futureSelf.position + new Vector3(3.1f, 2.5f, -5.6f);
                cameraLook = at + Vector3.up;
                yield return null;
            }
            if (generation != causalAnimation || ghostGroup == null) yield break;
            futureSelf.position = DayPoint(to) + new Vector3(0.45f, -0.09f, 0.3f);
            cameraPosition = futureSelf.position + new Vector3(3.1f, 2.5f, -5.6f);
            cameraLook = futureSelf.position + Vector3.up;
            actor.Walking = false; actor.Pointing = true;
            if (!preferences.reducedMotion) WorldRipple(DayPoint(to), portalLight, 0.65f);
            Tick(2);
        }

        public void EndCausalPresentation()
        {
            ++causalAnimation;
            if (ghostGroup != null) { ghostGroup.gameObject.SetActive(false); Dispose(ghostGroup.gameObject); ghostGroup = null; }
        }
    }
}
