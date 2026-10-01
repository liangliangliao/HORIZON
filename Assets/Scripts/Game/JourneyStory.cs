using System.Linq;

namespace Horizon.Game
{
    public static class JourneyStory
    {
        private static readonly string[][] Lines = {
            new[] { "「今天，不必把整个人生想明白。」", "「先做一件事，看看它在你身上留下什么。」", "「此刻的你，也值得照顾。」" },
            new[] { "「昨天已经过去，但它留下的东西还在。」", "「一点余力，一条回信，都能走进新的一天。」", "「行为会回来。你可以给它留一个位置。」" },
            new[] { "「有些光，需要走三天才到你面前。」", "「等待时，你仍然可以生活、休息、靠近别人。」", "「回声在路上，今天的选择仍在手里。」" },
            new[] { "「疲惫有时带走的，不只是精力。」", "「它可能让你错过邀约。努力也可能打开新的机会。」", "「一件事，会改变后来可以做的事。」" },
            new[] { "「我也不知道，每一场雨会在哪一天落下。」", "「你种下的回声会回来；天气和别人的时间仍有变化。」", "「给可能发生的事留空间，也是一种远见。」" },
            new[] { "「如果那天的选择不同，我会是谁？」", "「从同一个起点，看看两种走法。没有一条是唯一答案。」", "「理解另一条路，不需要抹掉已经走过的人生。」" },
            new[] { "「你已经不只看见明天了。」", "「三十天以后，努力、快乐、朋友与休息会怎样连在一起？」", "「现在，你可以亲手走完这三十天。我们在远处再见。」" }
        };
        public static string Voice(int chapter, int beat) { return Lines[chapter - 1][beat]; }
        public static string Memory(Horizon.ArchiveData archive, int chapter)
        {
            var actions = archive.active?.actions ?? archive.runs.LastOrDefault()?.actions;
            var action = actions?.LastOrDefault(a => chapter == 4 ? a.secondaryResolved : chapter == 3 ? a.echoed : true)
                ?? actions?.LastOrDefault();
            return action == null ? "你自己的选择，会成为这里的记忆。" :
                "你留下过：D" + action.day + "「" + action.cardName + "」\n" +
                (action.echoed ? "它已经留下「" + action.echoName + "」。" : action.echoDay > 0 ?
                    "它的回声走向了D" + action.echoDay + "。" : "那天，你给自己留出了空间。");
        }
    }
}
