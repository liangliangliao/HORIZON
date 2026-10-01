using System;
using System.Collections.Generic;
using System.Linq;

namespace Horizon.Game
{
    public sealed class WorldEventSpec
    {
        public readonly int Day, Chance;
        public readonly string Name, ReplacementId;
        public readonly CardKind Slot;
        public readonly ResourceDelta Delta;
        public WorldEventSpec(int day, int chance, string name, string replacementId,
            CardKind slot, ResourceDelta delta = null)
        { Day = day; Chance = chance; Name = name; ReplacementId = replacementId;
            Slot = slot; Delta = delta ?? new ResourceDelta(); }
    }

    // A life's seed is persisted. Observing or reloading never rerolls its weather.
    // Planted echoes remain scheduled consequences, independent of these chances.
    public static class WorldEvents
    {
        public static readonly WorldEventSpec[] All = {
            new WorldEventSpec(6, 50, "下雨了，今天适合早点休息", "rest", CardKind.Recovery),
            new WorldEventSpec(8, 60, "朋友刚好有空，一起吃饭吧", "dinner", CardKind.Recovery),
            new WorldEventSpec(10, 35, "事情提前结束，多了半小时", "shortstudy", CardKind.Growth, new ResourceDelta(1))
        };
        public static readonly WorldEventSpec[] Season = {
            new WorldEventSpec(3, 45, "通勤提前结束，留出一小段自己的时间", "breathing", CardKind.Recovery),
            new WorldEventSpec(5, 55, "朋友推荐了一本书，你想借来看看吗？", "library", CardKind.Growth),
            All[0], All[1], All[2],
            new WorldEventSpec(11, 50, "朋友今天想找个人说说话", "listen", CardKind.Recovery)
        };
        public static WorldEventSpec[] ForCatalog(int version)
        { return version >= 6 ? CampaignContent.World : version >= 5 ? Season : All; }
        public static bool Occurs(int seed, int day, int chance)
        {
            unchecked {
                uint x = (uint)seed ^ (uint)day * 0x9e3779b9u;
                x ^= x >> 16; x *= 0x7feb352du; x ^= x >> 15; x *= 0x846ca68bu; x ^= x >> 16;
                return x % 100 < chance;
            }
        }
    }

    public static class ObservationDesign
    {
        public static int VisibleTypes(GameSession session, int chapter)
        { return new HorizonProgress(Math.Max(HorizonProgress.LifeStage(session), chapter), 0).VisibleTypes; }
        public static bool CanCompare(GameSession session, int chapter)
        { return new HorizonProgress(Math.Max(HorizonProgress.LifeStage(session), chapter), 0).Compare; }
        public static string EchoType(PendingEcho echo)
        { return echo.kind == CardKind.Growth ? "芽" : echo.kind == CardKind.Recovery ? "回应" :
                echo.delta != null && echo.delta.energy >= 0 && echo.delta.mood >= 0 ? "余兴" : "火种"; }
        public static string FocusClue(PendingEcho echo, int level, int calibrations)
        {
            return level < 2 && echo.kind != CardKind.Temptation && calibrations == 0 ?
                "尚未看清的回声" : ForecastKnowledge.Clue(echo, level, calibrations);
        }
        public static List<CausalNode> PredictionCauses(GameSession session)
        {
            PredictionRecord p = session.Prediction;
            if (p == null || !p.evaluated) return new List<CausalNode>();
            // Day 4's action happens after locking. Day 7's action is still unplayed.
            return session.CausalNodes.Where(n => n.resolved && n.effectRecorded && n.effect != null &&
                (n.type == CausalNodeKind.Action ? n.day >= p.sourceDay && n.day < p.dueDay :
                    n.day > p.sourceDay && n.day <= p.dueDay) &&
                (n.effect.energy != 0 || n.effect.mood != 0 || n.effect.insight != 0 ||
                    p.sixAxes && (n.effect.relation != 0 || n.effect.money != 0 || n.effect.ability != 0)))
                .OrderBy(n => n.day).ToList();
        }
    }
}
