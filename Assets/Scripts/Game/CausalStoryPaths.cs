using System.Collections.Generic;
using System.Linq;

namespace Horizon.Game
{
    public sealed class CausalStoryPath
    {
        public List<CausalNode> nodes;
        public string Title { get { return nodes[nodes.Count - 1].label; } }
    }

    // A readable path is a selection of actual edges, never an invented explanation.
    public static class CausalStoryPaths
    {
        public static List<CausalStoryPath> For(RunRecord run)
        {
            var graph = CausalGraph.ObservedGraph(GameSession.GraphForRun(run));
            var indexed = graph.ToDictionary(n => n.id);
            var memo = new Dictionary<string, List<CausalNode>>();
            var parents = new HashSet<string>(graph.SelectMany(CausalGraph.Parents));
            string goal = run?.master?.chapter?.outcomeNode;
            var ends = graph.Where(n => n.id == goal || n.type == CausalNodeKind.Gate || !parents.Contains(n.id))
                .OrderByDescending(n => n.id == goal).ThenByDescending(n => n.type == CausalNodeKind.Gate)
                .ThenByDescending(n => n.day);
            var result = new List<CausalStoryPath>();
            foreach (var end in ends)
            {
                var path = Path(end, indexed, memo, new HashSet<string>());
                if (path.Count < 2 || !path.Any(n => n.type == CausalNodeKind.Action || n.type == CausalNodeKind.Opportunity)) continue;
                result.Add(new CausalStoryPath { nodes = path });
                if (result.Count == 6) break;
            }
            return result;
        }

        private static List<CausalNode> Path(CausalNode node, Dictionary<string, CausalNode> graph,
            Dictionary<string, List<CausalNode>> memo, HashSet<string> visiting)
        {
            if (memo.TryGetValue(node.id, out var known)) return known;
            if (!visiting.Add(node.id)) return new List<CausalNode>();
            var best = new List<CausalNode>();
            foreach (string id in CausalGraph.Parents(node))
            {
                if (!graph.TryGetValue(id, out var parent) || visiting.Contains(id)) continue;
                var candidate = Path(parent, graph, memo, visiting);
                if (Score(candidate) > Score(best)) best = candidate;
            }
            visiting.Remove(node.id);
            var path = new List<CausalNode>(best) { node }; memo[node.id] = path; return path;
        }

        private static int Score(List<CausalNode> path)
        {
            return path.Sum(n => n.type == CausalNodeKind.Action || n.type == CausalNodeKind.Thought ? 12 :
                n.type == CausalNodeKind.Echo ? 8 : n.type == CausalNodeKind.Opportunity ? 6 : 1);
        }
    }
}
