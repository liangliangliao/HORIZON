using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Horizon.Game;
using Horizon.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Horizon
{
    public sealed partial class HorizonApp
    {
        private SharedConnection social;
        private SharedRoom socialRoom;
        private bool parallelPlaying, socialBusy;
        private IAITransport socialTransport;
        private string SocialSavePath { get { return Path.Combine(Application.persistentDataPath, "HORIZON.parallel-session.json"); } }
        private SocialClient SocialService { get { return new SocialClient(social.endpoint, socialTransport ?? (socialTransport = new UnityAITransport())); } }
        private void SaveParallelSession()
        {
            if (social == null) return;
            if (parallelPlaying && session != null) social.life = session.CompletedRun == null ? session.Snapshot() : null;
            File.WriteAllText(SocialSavePath + ".tmp", JsonUtility.ToJson(social));
            if (File.Exists(SocialSavePath)) File.Delete(SocialSavePath);
            File.Move(SocialSavePath + ".tmp", SocialSavePath);
        }
        private void ShowOnlineLives()
        {
            if (social == null)
            { try { social = JsonUtility.FromJson<SharedConnection>(File.ReadAllText(SocialSavePath)); } catch { social = new SharedConnection(); } }
            social = social ?? new SharedConnection();
            if (social.life != null && social.life.runNumber == 0) social.life = null;
            MasterPage("Online parallel lives", "同一个今天，两条人生", ShowModes);
            View.Label(overlay, "Online common start", "邀请一个人，从相同起点各自生活。\n只共享卡牌、资源和主动留下的经验。", 31, Palette.Text, TextAnchor.MiddleLeft, 0.075f, 0.70f, 0.925f, 0.845f);
            MasterInput("Social endpoint", social.endpoint ?? "", value => social.endpoint = value, 0.605f, 500);
            View.Label(overlay, "Social endpoint label", "在线服务 HTTPS 地址", 23, Palette.Muted, TextAnchor.MiddleLeft, 0.075f, 0.662f, 0.925f, 0.70f);
            MasterInput("Social name", social.name, value => social.name = AIText.Bound(value, 30), 0.505f, 30);
            MasterInput("Social invitation", social.code ?? "", value => social.code = value.Trim().ToUpperInvariant(), 0.405f, 10);
            Text notice = View.Label(overlay, "Social status", string.IsNullOrEmpty(social.memberToken) ? "创建邀请，或填入朋友给你的邀请码。" : "邀请码：" + social.code + " · 可复制给朋友", 26,
                Palette.Gold, TextAnchor.MiddleLeft, 0.075f, 0.345f, 0.925f, 0.405f);
            View.Button(overlay, "Create online life", "创建邀请", () => SocialOperation(notice, async () => {
                SharedMembership membership = await SocialService.CreateRoom(Guid.NewGuid().GetHashCode(), social.name, CancellationToken.None); AcceptMembership(membership);
            }), 0.075f, 0.265f, 0.49f, 0.335f, Palette.Mint, Palette.Ink, 27);
            View.Button(overlay, "Join online life", "加入邀请", () => SocialOperation(notice, async () => {
                SharedMembership membership = await SocialService.JoinRoom(social.code, social.name, CancellationToken.None); AcceptMembership(membership);
            }), 0.51f, 0.265f, 0.925f, 0.335f, Palette.Panel, Palette.Text, 27);
            if (!string.IsNullOrEmpty(social.memberToken))
            {
                View.Button(overlay, "Read online room", "同步与比较两条人生", () => SocialOperation(notice, async () => {
                    socialRoom = parallelPlaying && session != null ? await SocialService.Publish(social.code, social.memberToken, SharedTimeline.From(session), CancellationToken.None) :
                        await SocialService.ReadRoom(social.code, social.memberToken, CancellationToken.None); ShowOnlineComparison();
                }), 0.075f, 0.18f, 0.925f, 0.248f, Palette.Panel, Palette.Mint, 28);
                View.Button(overlay, "Begin online life", parallelPlaying ? "回到这条平行人生" : "进入共同起点", () => {
                    if (parallelPlaying) { CloseMasterPage(); BuildBoard(); return; }
                    SocialOperation(notice, async () => { socialRoom = await SocialService.ReadRoom(social.code, social.memberToken, CancellationToken.None); StartOnlineLife(); });
                }, 0.075f, 0.105f, 0.49f, 0.17f, Palette.Mint, Palette.Ink, 27);
                View.Button(overlay, "Copy online invitation", "复制邀请码", () => GUIUtility.systemCopyBuffer = social.code, 0.51f, 0.105f, 0.925f, 0.17f, Palette.Panel, Palette.Text, 27);
            }
        }
        private void AcceptMembership(SharedMembership member)
        {
            if (member?.room == null || string.IsNullOrEmpty(member.memberToken) || member.memberToken.Length != 48 || !member.memberToken.All(Uri.IsHexDigit)) throw new AIException(AIError.InvalidContent);
            member.room.Validate(); socialRoom = member.room; social.code = member.room.code; social.memberId = member.memberId; social.memberToken = member.memberToken; social.life = null;
            SaveParallelSession(); ShowOnlineLives();
        }
        private async void SocialOperation(Text notice, Func<Task> operation)
        {
            if (socialBusy) return; socialBusy = true; if (notice != null) notice.text = "正在连接这条共同时间线…";
            try { await operation(); }
            catch (Exception e) { if (notice != null) notice.text = e is AIException ai ? ai.Message : "连接暂时不可用。人生进度已经保留，可稍后同步。"; }
            finally { socialBusy = false; }
        }
        private void StartOnlineLife()
        {
            if (IsPractice) ExitPractice(); StartPractice();
            archive.me = new PlayerBehavioralModel(); archive.futureMemories.Clear(); archive.worldview.Clear();
            session = social.life != null ? GameSession.Restore(social.life) : GameSession.StartMasterLife(1, socialRoom.worldSeed, RunMode.ParallelLives);
            parallelPlaying = true; ImportOnlineMessages(); archive.active = session.Snapshot(); SaveParallelSession(); CloseMasterPage(); BuildBoard();
        }
        private void ImportOnlineMessages()
        {
            if (!parallelPlaying || socialRoom?.messages == null || session == null) return;
            foreach (SharedMessage message in socialRoom.messages.Where(m => m.memberId != social.memberId))
                session.ReceiveFutureMessage(socialRoom.code + ":" + message.id, message.text, message.day);
            PersistMasterAction();
        }
        private void ShowOnlineComparison()
        {
            SharedRoom room = socialRoom; if (room == null) return;
            ImportOnlineMessages();
            MasterPage("Online life comparison", "同一个起点 · 不同的来路", ShowOnlineLives);
            int divergence = room.DivergenceDay();
            View.Label(overlay, "Online divergence", room.members.Length == 1 ? "朋友还没有加入。邀请仍然保留着。" : divergence == 0 ? "目前观察到的选择相同；继续看未来怎样展开。" : "D" + divergence + " · 选择第一次分开", 33,
                Palette.Gold, TextAnchor.MiddleCenter, 0.075f, 0.73f, 0.925f, 0.85f);
            for (int i = 0; i < room.members.Length; i++)
            { SharedMember member = room.members[i]; float x = i == 0 ? 0.075f : 0.525f;
                string actions = string.Join("\n", (member.timeline?.actions ?? new System.Collections.Generic.List<SharedAction>()).TakeLastPortable(5).Select(a => "D" + a.day + " · " + a.cardName));
                View.Label(overlay, "Online member " + i, member.name + "\n" + (member.timeline?.completed == true ? "人生已完成" : "继续生活中") + "\n\n" + actions, 29, Palette.Text, TextAnchor.UpperLeft,
                    x, 0.40f, x + 0.4f, 0.72f).supportRichText = false; }
            string text = "失败以后，我先恢复，再重新开始。";
            MasterInput("Online future message", text, value => text = AIText.Bound(value, 200), 0.29f, 200);
            Text status = View.Label(overlay, "Online message status", room.messages.LastOrDefault()?.text ?? "把一段经验留在对方的时间线上。", 27, Palette.Muted, TextAnchor.MiddleLeft, 0.075f, 0.19f, 0.925f, 0.285f);
            status.supportRichText = false;
            View.Button(overlay, "Send online future message", "留给这条时间线一段经验", () => SocialOperation(status, async () => {
                socialRoom = await SocialService.LeaveMessage(social.code, social.memberToken, text, Math.Min(12, session?.Day ?? 1), CancellationToken.None); ShowOnlineComparison();
            }), 0.075f, 0.11f, 0.925f, 0.18f, Palette.Panel, Palette.Mint, 27);
        }
    }
}
