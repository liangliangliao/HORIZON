using System;
using System.Collections.Generic;
using System.Linq;

namespace Horizon.Game
{
    public static class MasterSpecification
    {
        public const string Version = "0.4.2";
        public const int SchemaVersion = 1;
        public const int MythicLimit = 2;
        public static readonly string[] OrbitNames = { "工作", "能力", "财富", "关系", "健康", "自由", "意义", "身份" };
        public static readonly string[] HorizonNames = { "现在", "一天", "三天", "概率", "二阶影响", "平行未来", "未来自己", "隐藏变量" };
    }

    public enum RunMode { Quick, ThirtyDays, LongRun, ParallelLives, ImaginationRun, ExperimentRun, MirrorRun, ChaosRun }
    [Flags]
    public enum CardFamily { None = 0, Stimulus = 1, Investment = 2, Rest = 4, Relationship = 8, Income = 16,
        Exploration = 32, Commitment = 64, Trigger = 128, Knowledge = 256, Worldview = 512,
        Risk = 1024, Recovery = 2048, Imagination = 4096, Decision = 8192 }
    public enum EchoType { Cost, Growth, Recovery, Connection, Opportunity }
    public enum RewardTier { Micro = 1, Local = 2, Combo = 3, Major = 4, Epic = 5, Mythic = 6 }
    public enum DomainEventKind { ActionTaken, TimeEcho, PredictionLocked, Synchronized, Surprise, DecisionLocked,
        DecisionReopened, ExecutionStep, TriggerEquipped, FailAndAgain, Comeback, VictoryAnchor, FutureMemory,
        DejaVu, PatternReinforced, PatternBroken, Cascade, CausalSingularity, Overdrive, OrbitActivated,
        AllLinked, Breakthrough, RealityNode, RealityConvergence, OpportunityExpired, MomentumExpired, Victory }

    [Serializable]
    public sealed class SecondOrderEffect
    {
        public string condition, outcome;
        public int delay;
    }

    [Serializable]
    public sealed class CardTraits
    {
        public CardFamily families;
        public EchoType echoType;
        public List<string> causeTags = new List<string>();
        public List<string> motivationTags = new List<string>();
        public int risk, commitment, visibility = 1;
        public string trigger, aiContext;
        public List<SecondOrderEffect> secondOrderEffects = new List<SecondOrderEffect>();

        public static CardTraits For(string id, CardKind kind, bool support)
        {
            var t = new CardTraits { families = kind == CardKind.Growth ? CardFamily.Investment :
                kind == CardKind.Recovery ? CardFamily.Rest : CardFamily.Stimulus,
                echoType = kind == CardKind.Growth ? EchoType.Growth : kind == CardKind.Recovery ? EchoType.Recovery : EchoType.Cost };
            if (support) { t.families |= CardFamily.Relationship; t.echoType = EchoType.Connection; }
            if (new[] { "smalljob", "rushjob", "budget", "project", "publish" }.Contains(id)) t.families |= CardFamily.Income;
            if (new[] { "study", "library", "review", "mentor", "forge" }.Contains(id)) t.families |= CardFamily.Knowledge;
            if (id == "trigger") { t.families |= CardFamily.Trigger; t.trigger = "alarm"; }
            if (id == "commit") { t.families |= CardFamily.Commitment | CardFamily.Decision; t.commitment = 2; }
            if (id == "imagine") t.families |= CardFamily.Imagination;
            if (id == "perspective") t.families |= CardFamily.Worldview;
            if (id == "explore") t.families |= CardFamily.Exploration;
            if (id == "risk") { t.families |= CardFamily.Risk; t.risk = 2; }
            if (id == "again" || id == "resilience") t.families |= CardFamily.Recovery;
            if (id == "plan") t.families |= CardFamily.Decision;
            t.causeTags.Add(kind == CardKind.Growth ? "investment" : kind == CardKind.Recovery ? "restoration" : "short-reward");
            t.motivationTags.Add(support ? "connection" : kind == CardKind.Growth ? "growth" : kind == CardKind.Recovery ? "care" : "comfort");
            t.secondOrderEffects.Add(new SecondOrderEffect { condition = kind == CardKind.Growth ? "能力、状态与机会同时成熟" :
                kind == CardKind.Recovery ? "精力恢复并再次行动" : "疲惫叠加且精力不足", outcome = kind == CardKind.Growth ?
                "积累可以打开新路线" : kind == CardKind.Recovery ? "为重启创造空间" : "可能错过支援窗口", delay = 1 });
            t.aiContext = "行为 " + id + "；动机是最近的情境线索，不能作为人格判断。结算仅由规则决定。";
            return t;
        }
    }

    public static class MasterContent
    {
        public static readonly CardSpec[] Actions = {
            new CardSpec("trigger", "准备行动环境", CardKind.Growth, new ResourceDelta(-1), new ResourceDelta(0, 0, 1, 0, 0, 1), 1, "明天 · 环境支持", "准备让下一步更容易开始"),
            new CardSpec("commit", "作出一个承诺", CardKind.Growth, new ResourceDelta(-1, 0, 0, 1), new ResourceDelta(0, 0, 1, 1, 0, 1), 2, "2日后 · 承诺", "承诺连接了准备与行动", true),
            new CardSpec("forge", "把知识变成动作", CardKind.Growth, new ResourceDelta(-2, 0, 1), new ResourceDelta(0, 0, 1, 0, 0, 2), 3, "3日后 · 经验", "你用过的知识成为了能力"),
            new CardSpec("imagine", "预演失败后的明天", CardKind.Growth, new ResourceDelta(-1, 0, 1), new ResourceDelta(0, 1, 1, 0, 0, 1), 2, "2日后 · 未来记忆", "曾经预演的路又出现了"),
            new CardSpec("perspective", "换一个思考角度", CardKind.Growth, new ResourceDelta(-1), new ResourceDelta(0, 1, 2), 1, "明天 · 视角", "一种思想为问题打开了另一扇窗"),
            new CardSpec("explore", "试一条陌生路线", CardKind.Growth, new ResourceDelta(-2), new ResourceDelta(0, 1, 1, 1, 0, 1), 2, "2日后 · 探索", "一次小探索扩大了选择空间"),
            new CardSpec("risk", "承担一次小风险", CardKind.Growth, new ResourceDelta(-2, -1, 0, 0, -1), new ResourceDelta(0, 0, 1, 0, 2, 2), 3, "3日后 · 尝试", "承担过的风险留下了经验"),
            new CardSpec("again", "恢复以后再开始", CardKind.Recovery, new ResourceDelta(3, 1), new ResourceDelta(0, 1, 1), 1, "明天 · 再战", "失败后的你仍然向前走了一步")
        };
        public static CardSpec[] Hand(CardSpec[] hand, int day, RunMode mode)
        {
            // The onboarding deck stays small; experimental modes introduce the new families.
            if (mode == RunMode.Quick || mode == RunMode.ThirtyDays || mode == RunMode.LongRun || mode == RunMode.ParallelLives || day == 1) return hand;
            var result = (CardSpec[])hand.Clone();
            if (hand[1].Id != "opportunity" && hand[1].Id != "together") result[1] = Actions[(day - 2) % 7];
            if (day % 4 == 0) result[2] = Actions[7];
            return result;
        }
    }

    [Serializable]
    public sealed class DomainEvent
    {
        public string id, nodeId, title, detail;
        public int day, chainSize, multiplier = 1;
        public DomainEventKind kind;
        public RewardTier tier;
        public bool acknowledged;
        public DomainEvent Copy() { return (DomainEvent)MemberwiseClone(); }
    }

    [Serializable]
    public sealed class MasterCommand
    {
        public int day, value;
        public string operation, argument;
        public ImagineRun imagination;
        public MasterCommand Copy() { var c = (MasterCommand)MemberwiseClone(); c.imagination = imagination?.Copy(); return c; }
    }

    [Serializable]
    public sealed class ResourceSample
    {
        public int day;
        public ResourceDelta values;
    }

    public sealed class ResourceTrend
    {
        public int current;
        public double trend, volatility;
        public string constraint;
    }

    [Serializable]
    public sealed class OpportunityWindow
    {
        public string id, nodeId, cardId;
        public int openedDay, expiresDay, momentumUntilDay;
        public bool taken, expired;
        public OpportunityWindow Copy() { return (OpportunityWindow)MemberwiseClone(); }
    }

    [Serializable]
    public sealed class MasterRunState
    {
        public int schemaVersion = 1;
        public RunMode mode;
        public int insightPoints, overdriveEnergy, overdriveUntilDay, mythicCount, orbitBits, reservoir,
            resilienceChain = 1, initialFailures, initialInsightPoints, overdriveGainDay, overdriveGain;
        public bool awaitingComeback, allLinked;
        public string lastFailureNode;
        public DecisionRecord decision;
        public ActionEngineState engine = new ActionEngineState();
        public List<string> triggers = new List<string>();
        public List<DomainEvent> events = new List<DomainEvent>();
        public List<MasterCommand> commands = new List<MasterCommand>();
        public List<OpportunityWindow> windows = new List<OpportunityWindow>();
        public List<ResourceSample> resources = new List<ResourceSample>();
        public List<BehaviorObservation> observations = new List<BehaviorObservation>();
        public List<PatternRecord> initialPatterns = new List<PatternRecord>();
        public List<PatternRecord> patterns = new List<PatternRecord>();
        public List<FutureMemory> initialMemories = new List<FutureMemory>();
        public List<FutureMemory> memories = new List<FutureMemory>();
        public List<KnowledgeSkill> knowledge = new List<KnowledgeSkill>();
        public List<KnowledgeSkill> initialKnowledge = new List<KnowledgeSkill>();
        public List<string> reservoirSources = new List<string>();

        public MasterRunState Copy()
        {
            var c = (MasterRunState)MemberwiseClone();
            c.decision = decision?.Copy(); c.engine = engine.Copy(); c.triggers = new List<string>(triggers);
            c.events = events.Select(e => e.Copy()).ToList(); c.commands = commands.Select(x => x.Copy()).ToList();
            c.windows = windows.Select(w => w.Copy()).ToList();
            c.resources = resources.Select(s => new ResourceSample { day = s.day, values = ResourceMath.Copy(s.values) }).ToList();
            c.observations = observations.Select(o => o.Copy()).ToList(); c.initialPatterns = initialPatterns.Select(p => p.Copy()).ToList();
            c.patterns = patterns.Select(p => p.Copy()).ToList(); c.initialMemories = initialMemories.Select(m => m.Copy()).ToList();
            c.memories = memories.Select(m => m.Copy()).ToList(); c.knowledge = knowledge.Select(k => k.Copy()).ToList();
            c.initialKnowledge = initialKnowledge.Select(k => k.Copy()).ToList();
            c.reservoirSources = new List<string>(reservoirSources); return c;
        }
        public void Validate(int today, int deadline)
        {
            if (schemaVersion != 1 || !Enum.IsDefined(typeof(RunMode), mode) || resilienceChain < 1 || resilienceChain > 10 ||
                overdriveEnergy < 0 || overdriveEnergy > 100 || mythicCount < 0 || mythicCount > MasterSpecification.MythicLimit ||
                orbitBits < 0 || orbitBits > 255 || reservoir < 0 || insightPoints < 0 || initialInsightPoints < 0 || events == null || commands == null || engine == null ||
                triggers == null || triggers.Count > 3 || triggers.Distinct().Count() != triggers.Count || triggers.Any(id => !TriggerEquipment.Ids.Contains(id)) ||
                windows == null || resources == null || observations == null || initialPatterns == null || patterns == null || initialMemories == null || memories == null || knowledge == null || knowledge.Count != 1 || initialKnowledge == null || initialKnowledge.Count != 1 || reservoirSources == null ||
                events.Count > 2048 || commands.Count > 2048 || events.Any(e => e == null || string.IsNullOrEmpty(e.id) || e.day < 1 || e.day > deadline) ||
                events.Select(e => e.id).Distinct().Count() != events.Count) throw new ArgumentException("Invalid v0.4.2 state.");
            if (decision != null) { decision.Validate(today); if (CardCatalog.FindById(decision.cardId) == null) throw new ArgumentException("Unknown locked action."); }
        }
    }

    public static class ResourceMath
    {
        public static ResourceDelta Copy(ResourceDelta d) { return d == null ? new ResourceDelta() : new ResourceDelta(d.energy, d.mood, d.insight, d.relation, d.money, d.ability); }
        public static int[] Axes(ResourceDelta d) { return new[] { d.energy, d.mood, d.insight, d.money, d.relation, d.ability }; }
        public static ResourceTrend Trend(List<ResourceSample> samples, int axis)
        {
            if (axis < 0 || axis > 5) throw new ArgumentOutOfRangeException("axis");
            int[] values = samples.Skip(Math.Max(0, samples.Count - 7)).Select(s => Axes(s.values)[axis]).ToArray();
            var t = new ResourceTrend { current = values.Length == 0 ? 0 : values.Last() };
            if (values.Length > 1) t.trend = (double)(values.Last() - values.First()) / (values.Length - 1);
            if (values.Length > 0) { double avg = values.Average(); t.volatility = Math.Sqrt(values.Average(v => (v - avg) * (v - avg))); }
            t.constraint = t.current <= 2 ? "低资源正在缩小可行动的范围" : t.trend < -0.5 ? "近期下降，留意后续机会窗口" : "仍有调整空间"; return t;
        }
    }
}
