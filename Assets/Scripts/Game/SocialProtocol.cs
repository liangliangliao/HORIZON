using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;

namespace Horizon.Game
{
    [Serializable] public sealed class SharedAction { public int day; public string cardId, cardName; }
    [Serializable] public sealed class SharedTimeline
    {
        public int worldSeed, runNumber = 1, catalogVersion = 10;
        public bool completed;
        public List<SharedAction> actions = new List<SharedAction>();
        public int[] resources;
        public static SharedTimeline From(GameSession life)
        {
            if (life == null || life.RunNumber != 1 || life.Deadline != 12 || life.Master.mode != RunMode.ParallelLives) throw new ArgumentException("A common starting scenario is required.");
            return new SharedTimeline { worldSeed = life.WorldSeed, completed = life.CompletedRun != null, resources = new[] { life.Energy, life.Mood, life.Insight, life.Money, life.Relation, life.Ability },
                actions = life.Actions.Select(a => new SharedAction { day = a.day, cardId = a.cardId, cardName = a.cardName }).ToList() };
        }
    }
    [Serializable] public sealed class SharedMember { public string id, name; public SharedTimeline timeline; }
    [Serializable] public sealed class SharedMessage { public string id, memberId, text; public int day; }
    [Serializable] public sealed class SharedRoom
    {
        public string code;
        public int worldSeed, catalogVersion, days, runNumber;
        public long expiresAt;
        public SharedMember[] members;
        public SharedMessage[] messages;
        public int DivergenceDay()
        {
            if (members?.Length != 2 || members[0].timeline?.actions == null || members[1].timeline?.actions == null) return 0;
            var a = members[0].timeline.actions; var b = members[1].timeline.actions;
            for (int i = 0; i < Math.Min(a.Count, b.Count); i++) if (a[i].cardId != b[i].cardId) return i + 1;
            return 0;
        }
        public void Validate()
        {
            if (members != null) foreach (SharedMember member in members)
                if (member?.timeline != null && member.timeline.worldSeed == 0 && member.timeline.resources == null && (member.timeline.actions == null || member.timeline.actions.Count == 0)) member.timeline = null;
            if (string.IsNullOrEmpty(code) || code.Length != 10 || catalogVersion != 10 || days != 12 || runNumber != 1 || members == null || members.Length < 1 || members.Length > 2 || messages == null || messages.Length > 16 ||
                members.Any(m => m == null || string.IsNullOrWhiteSpace(m.id) || (m.name ?? "").Length > 30 || m.timeline != null && m.timeline.actions != null &&
                    (m.timeline.actions.Count > 12 || m.timeline.worldSeed != worldSeed)) || messages.Any(m => m == null || (m.text ?? "").Length > 200)) throw new AIException(AIError.InvalidContent);
        }
    }
    [Serializable] public sealed class SharedMembership { public SharedRoom room; public string memberId, memberToken; }
    [Serializable] public sealed class SharedConnection
    {
        // Kept in its own private session file, never in the exportable life archive.
        public string endpoint, name = "旅行者", code, memberId, memberToken;
        public RunSnapshot life;
    }
    public sealed class SocialClient
    {
        private readonly string root; private readonly IAITransport transport;
        public SocialClient(string endpoint, IAITransport transport)
        {
            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out Uri uri) || uri.Scheme != "https" || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
                throw new AIException(AIError.Configuration);
            root = endpoint.TrimEnd('/'); this.transport = transport;
        }
        [Serializable] private sealed class Create { public int worldSeed; public string name; }
        [Serializable] private sealed class Join { public string name; }
        [Serializable] private sealed class Upload { public SharedTimeline timeline; }
        [Serializable] private sealed class Message { public string text; public int day; }
        private async Task<T> Send<T>(string path, object payload, string token, CancellationToken cancellation, bool get = false)
        {
            var headers = new Dictionary<string, string> { { "Content-Type", "application/json" } };
            if (!string.IsNullOrEmpty(token)) headers.Add("Authorization", "Bearer " + token);
            AIResponse response = await transport.Send(new AIRequest(root + path, get ? "" : JsonUtility.ToJson(payload), headers, 20, get ? "GET" : "POST"), cancellation);
            AIProtocol.CheckStatus(response); cancellation.ThrowIfCancellationRequested();
            try { return JsonUtility.FromJson<T>(response.Body); } catch { throw new AIException(AIError.InvalidContent); }
        }
        private static string Code(string code)
        { if (code == null || code.Length != 10 || code.Any(c => !Uri.IsHexDigit(c))) throw new AIException(AIError.Configuration); return code.ToUpperInvariant(); }
        public Task<SharedMembership> CreateRoom(int seed, string name, CancellationToken ct)
        { return Send<SharedMembership>("/v1/rooms", new Create { worldSeed = seed, name = AIText.Bound(name, 30) }, "", ct); }
        public Task<SharedMembership> JoinRoom(string code, string name, CancellationToken ct)
        { return Send<SharedMembership>("/v1/rooms/" + Code(code) + "/join", new Join { name = AIText.Bound(name, 30) }, "", ct); }
        public async Task<SharedRoom> ReadRoom(string code, string token, CancellationToken ct)
        { SharedRoom room = await Send<SharedRoom>("/v1/rooms/" + Code(code), null, token, ct, true); if (room == null) throw new AIException(AIError.InvalidContent); room.Validate(); return room; }
        public async Task<SharedRoom> Publish(string code, string token, SharedTimeline life, CancellationToken ct)
        { SharedRoom room = await Send<SharedRoom>("/v1/rooms/" + Code(code) + "/timeline", new Upload { timeline = life }, token, ct); if (room == null) throw new AIException(AIError.InvalidContent); room.Validate(); return room; }
        public async Task<SharedRoom> LeaveMessage(string code, string token, string text, int day, CancellationToken ct)
        { SharedRoom room = await Send<SharedRoom>("/v1/rooms/" + Code(code) + "/messages", new Message { text = AIText.Bound(text, 200), day = day }, token, ct); if (room == null) throw new AIException(AIError.InvalidContent); room.Validate(); return room; }
    }
}
