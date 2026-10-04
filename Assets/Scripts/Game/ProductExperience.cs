using System;
using System.Linq;

namespace Horizon.Game
{
    [Serializable]
    public sealed class PlayerPreferences
    {
        public int version = 1;
        public bool sound = true;
        public bool ambience = true;
        public bool haptics = true;
        public bool reducedMotion;
        public bool batterySaver;
    }

    public static class ProductExperience
    {
        public static string Coach(GameSession life)
        {
            if (life.Energy <= 2) return "精力见底了。选一张恢复牌，给明天留点力气。";
            if (life.Deadline >= 30) return "长局 · " + CampaignContent.ActName(life.Day) + "\n" + (life.Day > 12 ? "三道门需要后半程的成长、恢复与支援各留下三次证据。" : "第12天是一段路的回望，真正截止日仍在后面。");
            if (life.RunNumber == 1 && life.Day == 1)
                return "每天选一张。第12天，用成长、状态和朋友点亮三道门。\n拖进上方金色圈，圈变亮后松手；也可以点牌。";
            if (life.RunNumber == 1 && life.Day == 2)
            {
                PendingEcho planted = life.Pending.FirstOrDefault();
                return planted == null ? "昨天的恢复已经留下。今天也可以试着积累未来。" :
                    "昨天的选择会在第" + planted.dueDay + "天回来。\n今天可以做另一件事，回声会自己抵达。";
            }
            if (life.RunNumber == 1 && life.Day == 3)
                return "积累未来需要精力。成长与休息交替，让回声在截止日前回来。";
            if (life.Day >= life.Deadline - 2) return "距离截止日还有" + (life.Deadline - life.Day) + "天 · 先留意回声是否来得及。";
            return "每天选 1 张 · 拖进金色圈，变亮后松手 / 点牌也能使用";
        }

        public static int EvidenceNeeded(GameSession life) { return life.Deadline >= 30 ? 3 : 2; }
        public static int GrowthEvidence(GameSession life)
        { return life.Actions.Count(a => (life.Deadline < 30 || a.day > 12) && a.echoed && a.later?.ability > 0); }
        public static int RecoveryEvidence(GameSession life)
        { return life.Actions.Count(a => (life.Deadline < 30 || a.day > 12) && a.kind == CardKind.Recovery); }
        public static int SupportEvidence(GameSession life)
        { return life.Actions.Count(a => (life.Deadline < 30 || a.day > 12) && a.givesSupport); }
        public static bool GateReady(GameSession life, int gate)
        {
            int need = EvidenceNeeded(life);
            if (gate == 0) return life.Ability >= 6 && (life.CatalogVersion < 2 || GrowthEvidence(life) >= need);
            if (gate == 1) return life.Energy >= 4 && life.Mood >= 4 && (life.CatalogVersion < 2 || RecoveryEvidence(life) >= need);
            return SupportEvidence(life) >= need && life.Relation >= 6 && life.Money >= 2;
        }

        public static int ReadyGates(GameSession life)
        { return Enumerable.Range(0, 3).Count(g => GateReady(life, g)); }

        public static string NextMove(GameSession life)
        {
            if (life.Day <= life.Deadline - 3 && !GateReady(life, 0)) return "成长回声需要时间，留意牌上的抵达日。";
            if (!GateReady(life, 1)) return "恢复能让你的状态和过去的努力一起抵达。";
            if (!GateReady(life, 2)) return "朋友与合作会留下支援；今天还有靠近他人的机会。";
            return "三道门目前已准备好。继续选择，照顾抵达前的自己。";
        }
    }
}
