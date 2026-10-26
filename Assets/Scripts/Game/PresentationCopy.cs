using System.Text.RegularExpressions;

namespace Horizon.Game
{
    // Presentation-only migration of legacy bilingual saves. Identifiers and
    // recorded simulation evidence are never rewritten.
    public static class PresentationCopy
    {
        private static readonly string[,] Words = {
            {"KNOW · 我知道","知道这个方法"},{"RECOGNIZE · 识别情境","识别情境"},
            {"SIMULATE · 游戏中用过","在游戏中演练"},{"EXECUTE · 现实中行动","在现实中行动"},{"EXPERIENCE · 形成经验","形成经验"},
            {"YOU HAVE SEEN\nTHIS BEFORE","这一步，你曾预演过"},{"PATTERN\nBROKEN","模式突破"},
            {"OPPORTUNITY WINDOW","机会窗口"},{"DECISION LOCK","决定已锁定"},{"EXECUTION MODE","执行模式"},{"EXECUTION","执行步骤"},
            {"BUILD THE PATH","准备这条路"},{"CHANGED STRATEGY","调整方法"},{"IMAGINED VICTORY","想象中的抵达"},
            {"FINAL PUSH","再次向前"},{"DIFFICULTY","遇到困难"},{"FAILURE","这次受挫"},
            {"RECOVERY","恢复"},{"REWIND","回到今天"},{"ACTION","开始行动"},
            {"MOMENTUM","行动势头"},{"LOCK ·","已决定 ·"},{"FAIL ·","遇到挫折 ·"},
            {"VictoryAnchor","目标画面"},{"Preparation","准备"},{"Effort","努力"},{"Failure","受挫"},
            {"Recover","恢复"},{"Retry","再次行动"},{"Adjust","调整方法"},{"Complete","预演完成"},{"Victory","抵达"},
            {"EXECUTION MODE","执行模式"},{"LONG RUN","长程人生"},{"PARALLEL LIVES","平行人生"},
            {"IMAGINATION RUN","想象演练"},{"EXPERIMENT RUN","对照实验"},{"MIRROR RUN","镜像人生"},{"CHAOS RUN","变化人生"},
            {"CAUSAL SINGULARITY","因果汇聚"},{"REALITY CONVERGENCE","现实汇流"},
            {"LOCK PREDICTION","封存预测"},{"PATTERN REINFORCED","模式再次出现"},
            {"PATTERN BROKEN","模式突破"},{"FUTURE STATION","未来站"},
            {"FUTURE ORBIT","未来轨道"},{"FUTURE MEMORY","未来记忆"},
            {"FUTURE SELF","未来的自己"},{"LIVE THE FUTURE","走进未来"},
            {"TIME ECHO","时间回声"},{"CHAIN FOUND","发现真实来路"},
            {"ALL LINKED","未来方向已连接"},{"FAIL & AGAIN","失败之后，再次开始"},
            {"GHOST TIMELINE","另一条可能的时间线"},{"BREAKTHROUGH","积累兑现"},
            {"SYNCHRONIZED","预测与实际吻合"},{"SURPRISE","发现预料之外的变化"},
            {"PREDICTION","原先的预测"},{"IMAGINATION","想象"},{"SIMULATION","游戏预演"},
            {"REALITY NODE","现实节点"},{"REALITY","现实"},{"CASCADE","因果连锁"},
            {"COMEBACK","再次行动"},{"OVERDRIVE","视野展开"},{"EXECUTE","执行"},
            {"VICTORY ANCHOR","目标画面"},{"Victory Anchor","目标画面"},
            {"Reality Node","现实节点"},{"Future Memory","未来记忆"},{"Future Message","未来来信"},
            {"COUNCIL","内在议会"},{"IMAGINE","想象演练"},{"FORGE","知识熔炉"},
            {"ORBIT","未来轨道"},{"Tal Ben-Shahar","塔尔·本-沙哈尔"},
            {"KNOW","知道"},{"RECOGNIZE","识别"},{"SIMULATE","游戏预演"},{"EXPERIENCE","形成经验"},
            {"IF ·","如果 ·"},{"THEN ·","那么 ·"},{"LOCK","锁定"},{"Trigger","行动提示"},
            {"ACT","接纳与承诺疗法"},{"CBT","认知行为疗法"},{"Stoicism","斯多葛主义"},{"James","威廉·詹姆斯"},
            {"Nietzsche","尼采"},{"Schopenhauer","叔本华"},{"Existentialism","存在主义"},
            {"ChangeMethod","调整方法"},{"AI ·","智能对话 ·"},
            {"growth","成长"},{"career","工作"},{"health","健康"},{"relationship","关系"},{"AGAIN","再行动一次"}
        };
        private static readonly Regex Dates = new Regex(@"(?<![A-Za-z0-9])(?:DAY|Day|D)\s*(\d+)(?!\d)");
        private static readonly Regex Runs = new Regex(@"\bRUN\s*(\d+)\b");
        private static readonly Regex Delay = new Regex(@"\+(\d+)D\b");
        private static readonly Regex FutureDay = new Regex(@"\bDay\s*\+(\d+)\b");
        private static readonly Regex HorizonLevel = new Regex(@"\bHORIZON\s+(VIII|VII|VI|IV|V|III|II|I)\b");
        private static readonly Regex Now = new Regex(@"\bNOW\b");
        public static string Chinese(string value)
        {
            if(string.IsNullOrEmpty(value)) return value ?? "";
            // Common spaced-out headings predate the cinematic text system.
            string result=value.Replace("SEE FARTHER. IMAGINE DEEPER.","看得更远，想得更深。")
                .Replace("HORIZON ME · 想象与现实","远见 · 想象与现实")
                .Replace("C H A I N   F O U N D","发现真实来路")
                .Replace("F U T U R E   S T A T I O N","未来站")
                .Replace("T I M E   E C H O","时间回声").Replace("T W O   F U T U R E S","两条可能的未来")
                .Replace("F O C U S   M O D E","聚焦眼前的选择")
                .Replace("I N N E R  C O U N C I L","内在议会").Replace("T R I G G E R","行动提示")
                .Replace("K N O W L E D G E  F O R G E","知识熔炉").Replace("F U T U R E  O R B I T","未来轨道")
                .Replace("R E A L I T Y  B R I D G E","现实桥梁").Replace("R E A L I T Y  C O N S T E L L A T I O N","现实星座")
                .Replace("P A R A L L E L  L I V E S","平行人生").Replace("F U T U R E  M E S S A G E","未来来信")
                .Replace("F U T U R E  S E L F","未来的自己");
            result=HorizonLevel.Replace(result,m=>"远见 · 第 "+RomanLevel(m.Groups[1].Value)+" 层")
                .Replace("H O R I Z O N","远 见").Replace("HORIZON","远见");
            for(int i=0;i<Words.GetLength(0);i++) result=result.Replace(Words[i,0],Words[i,1]);
            result=Dates.Replace(result,m=>"第 "+m.Groups[1].Value.TrimStart('0').PadLeft(1,'0')+" 天");
            result=Runs.Replace(result,m=>"第 "+m.Groups[1].Value.TrimStart('0').PadLeft(1,'0')+" 段人生").Replace(" DAYS"," 天").Replace("ME ·","我 ·");
            result=FutureDay.Replace(result,m=>m.Groups[1].Value+" 天后");
            result=Now.Replace(Delay.Replace(result,m=>m.Groups[1].Value+" 天后"),"当下");
            result=result.Replace("想象演练 · 想象演练","想象演练").Replace("内在议会 · 内在议会","内在议会")
                .Replace("知识熔炉 · 知识熔炉","知识熔炉").Replace("未来轨道 · 未来轨道","未来轨道");
            result=result.Replace("远 见  M E","远见 · 我");
            return result;
        }

        private static int RomanLevel(string value)
        {
            switch(value)
            {
                case "I":return 1; case "II":return 2; case "III":return 3; case "IV":return 4;
                case "V":return 5; case "VI":return 6; case "VII":return 7; default:return 8;
            }
        }
    }
}
