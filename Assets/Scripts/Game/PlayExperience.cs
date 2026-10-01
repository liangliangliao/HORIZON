using System;
using System.Collections.Generic;

namespace Horizon.Game
{
    public enum FeedbackKind { Choice, Echoes, Deadline }

    [Serializable]
    public sealed class FeedbackBeat
    {
        public string title;
        public string source;
        public int sourceDay;
        public int destinationDay;
        public CardKind intent;
        public bool support;
        public ResourceDelta delta;
        public string meaning;
        public int chainSize;
        public int stardust;
    }

    [Serializable]
    public sealed class FeedbackRecord
    {
        public FeedbackKind kind;
        public int runNumber;
        public int day;
        public string title;
        public string description;
        public int stardust;
        public List<FeedbackBeat> beats = new List<FeedbackBeat>();
        public int page;
        public bool presented;
        public int presentedPages;
        public int preparedGates;
    }

    [Serializable]
    public sealed class RewardWallet
    {
        public int stardust;
        public int theme;
        public List<string> claimed = new List<string>();
        public List<int> ownedThemes = new List<int> { 0 };

        public void Repair()
        {
            if (claimed == null) claimed = new List<string>();
            if (ownedThemes == null) ownedThemes = new List<int>();
            if (!ownedThemes.Contains(0)) ownedThemes.Add(0);
            stardust = Math.Max(0, stardust);
            if (!ownedThemes.Contains(theme)) theme = 0;
        }

        // A durable event receipt prevents resume/replay from awarding the same
        // action twice. The currency buys scenery only.
        public int Claim(string eventId, int amount)
        {
            Repair();
            if (string.IsNullOrEmpty(eventId) || amount <= 0 || claimed.Contains(eventId)) return 0;
            claimed.Add(eventId);
            stardust += amount;
            return amount;
        }

        public static int ThemeCost(int id) { return id == 1 ? 25 : id == 2 ? 60 : 0; }

        public bool SelectTheme(int id)
        {
            Repair();
            if (id < 0 || id > 2) return false;
            if (!ownedThemes.Contains(id))
            {
                int cost = ThemeCost(id);
                if (stardust < cost) return false;
                stardust -= cost;
                ownedThemes.Add(id);
            }
            theme = id;
            return true;
        }
    }

    public static class PlayExperience
    {
        public static string DestinationLabel(CardSpec card, int day, int deadline = GameSession.LastDay)
        {
            if (card.Delay == 0) return "今天 · 立即恢复";
            int due = day + card.Delay;
            return "D" + due.ToString("00") + (due > deadline ? " · 截止日之后" : " · " + card.Delay + " 天后回来");
        }

        public static string LandingLabel(CardSpec card)
        {
            return card.Delay == 0 ? "松手 · 现在恢复" : "松手 · 送入未来";
        }

        public static bool IsDifficult(ResourceDelta delta)
        {
            return delta != null && (delta.energy < 0 || delta.mood < 0 || delta.relation < 0 || delta.money < 0);
        }

        public static string NowLabel(ResourceDelta delta)
        {
            if (delta == null) return "状态没有变化";
            string label = "";
            Add(ref label, "精力", delta.energy);
            Add(ref label, "心情", delta.mood);
            Add(ref label, "洞察", delta.insight);
            Add(ref label, "关系", delta.relation);
            Add(ref label, "金钱", delta.money);
            Add(ref label, "能力", delta.ability);
            return label.Length == 0 ? "状态保持不变" : label;
        }

        private static void Add(ref string label, string name, int value)
        {
            if (value == 0) return;
            if (label.Length > 0) label += "  ·  ";
            label += name + " " + (value > 0 ? "+" : "") + value;
        }

        public static string FutureLabel(CardSpec card, int day, int deadline = GameSession.LastDay)
        {
            if (card.Delay == 0) return "立即恢复。今天不再埋下额外回声。";
            int due = day + card.Delay;
            return due > deadline ? "回声会在第 " + due + " 天回来，超过本局截止日。" :
                "回声已送往第 " + due + " 天，" + card.Delay + " 天后会回来。";
        }

        public static string FutureMeaning(CardSpec card)
        {
            if (card.Delay == 0) return "今天立即恢复";
            if (card.Kind == CardKind.Growth) return card.Later.ability > 0 ? "能力会成长" : "带来新的机会";
            if (card.GivesSupport) return "朋友会带来回应";
            if (card.Kind == CardKind.Recovery) return card.Later.energy > 0 ? "给后续留下余力" :
                card.Later.mood > 0 ? "心情会慢慢回暖" : "理解会慢慢清晰";
            if (card.Later.energy >= 0 && card.Later.mood >= 0) return "快乐也会留下余温";
            return card.Later.energy < 0 ? "之后可能疲惫" : "快乐会慢慢褪去";
        }

        public static string BlockReason(GameSession session, CardSpec card)
        {
            if (session.Energy + card.Now.energy < 0) return "精力不足，先选一张恢复牌。";
            if (session.Money + card.Now.money < 0) return "金钱不足，换一个不花钱的行动。";
            return "此刻的状态无法支持这张牌，试试恢复。";
        }

        public static string GateReason(GameSession session, int gate)
        {
            if (gate == 0) return "能力 " + session.Ability + "/6 · " +
                (session.CatalogVersion >= 2 ? "成长回声 " + ProductExperience.GrowthEvidence(session) +
                    "/" + ProductExperience.EvidenceNeeded(session) + "，练习和作品会帮你。" : "成长牌的回声会提高能力。");
            if (gate == 1) return "精力 " + session.Energy + "、心情 " + session.Mood +
                " · 截止日各需至少 4" + (session.CatalogVersion >= 2 ? "；恢复 " +
                    ProductExperience.RecoveryEvidence(session) + "/" + ProductExperience.EvidenceNeeded(session) + "。" : "，恢复牌可以帮助你。");
            return "支援行动 " + ProductExperience.SupportEvidence(session) + "/" + ProductExperience.EvidenceNeeded(session) + " · 关系 " + session.Relation +
                "/6 · 金钱 " + session.Money + "/2，朋友与合作会留下支援。";
        }
    }
}
