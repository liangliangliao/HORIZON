using System;
using System.Collections.Generic;

namespace Horizon.Game
{
    public enum PracticeSet { Street, Track, Cafe, Desk, Workshop, Bedroom, Shop, Stage, Kitchen, Garden, Library }
    public enum PracticeMove { Walk, Examine, Assemble, Talk, Rest, Sort, Write, Listen, Carry }
    public enum PracticeProp { Map, Shoe, Phone, Book, Folder, Clock, Box, Ticket, Cup, Flower, Record, Coin, Model, Basket, Note }

    public sealed class PracticeScript
    {
        public readonly PracticeSet Set;
        public readonly PracticeProp Prop;
        public readonly string Id, Goal, Obstacle, RouteA, RouteB, Success;
        public readonly PracticeMove MoveA, MoveB;

        internal PracticeScript(string row)
        {
            string[] v = row.Split('|');
            Id = v[0];
            Set = (PracticeSet)Enum.Parse(typeof(PracticeSet), v[1]);
            Prop = (PracticeProp)Enum.Parse(typeof(PracticeProp), v[2]);
            Goal = v[3]; Obstacle = v[4]; Success = v[9];
            MoveA = (PracticeMove)Enum.Parse(typeof(PracticeMove), v[5]); RouteA = v[6];
            MoveB = (PracticeMove)Enum.Parse(typeof(PracticeMove), v[7]); RouteB = v[8];
        }

        public string Route(int beat,bool second)
        {
            if (beat==0) return second?RouteB:RouteA;
            if (Set==PracticeSet.Track || Set==PracticeSet.Street) return second?"沿可通行的一侧绕过封闭处":"停在路口，核对方向再前进";
            if (Set==PracticeSet.Workshop) return second?"把零件移到另一侧重新组装":"找出偏移处，逐点校准接口";
            if (Set==PracticeSet.Cafe || Set==PracticeSet.Kitchen) return second?"给对方留一点时间，等他说完":"说出一个具体问题，确认彼此的意思";
            if (Set==PracticeSet.Garden) return second?"重新固定底部支点":"查看盆土和枝条，找出松动的位置";
            if (Set==PracticeSet.Shop) return second?"移到一旁，重排要留下的物品":"对着小票重新核对金额";
            if (Set==PracticeSet.Bedroom) return second?"把分心的东西送到远处，再回来":"放慢动作，从刚才停下的地方接续";
            if (Set==PracticeSet.Stage) return second?"沿过道换到更清楚的位置":"停下来，跟住这一段的节拍";
            return second?"把手头的材料按新的要求重排":"标出缺口，先补上这一小步";
        }
        public PracticeMove Move(int beat,bool second)
        {
            if (beat==0) return second?MoveB:MoveA;
            switch(Set)
            {
                case PracticeSet.Track: case PracticeSet.Street: return second?PracticeMove.Walk:PracticeMove.Examine;
                case PracticeSet.Workshop: case PracticeSet.Garden: return second?PracticeMove.Assemble:PracticeMove.Examine;
                case PracticeSet.Cafe: case PracticeSet.Kitchen: return second?PracticeMove.Listen:PracticeMove.Talk;
                case PracticeSet.Bedroom: return second?PracticeMove.Carry:PracticeMove.Rest;
                case PracticeSet.Shop: return second?PracticeMove.Sort:PracticeMove.Examine;
                case PracticeSet.Stage: return second?PracticeMove.Walk:PracticeMove.Listen;
                default: return second?PracticeMove.Sort:PracticeMove.Write;
            }
        }
        public string Hurdle(int beat)
        {
            if (beat == 0) return Obstacle;
            switch (Set)
            {
                case PracticeSet.Street: case PracticeSet.Track: return beat == 1 ? "转弯处的路标倒了，脚步在岔路前停住。" : "最后一段路被围起，原来的走法到了尽头。";
                case PracticeSet.Cafe: case PracticeSet.Kitchen: return beat == 1 ? "两人同时开口，又一起停下，话没能说完。" : "对方收起东西准备离开，留给这次见面的时间很短。";
                case PracticeSet.Workshop: return beat == 1 ? "第二次试装时连接处歪了，成品还立不住。" : "送到验收台时，一件松动的零件滑了下来。";
                case PracticeSet.Shop: return beat == 1 ? "到了柜台才发现总额有误，手停在付款键上。" : "店员催着结账，最初的打算又动摇了。";
                case PracticeSet.Bedroom: return beat == 1 ? "屏幕忽然亮起，刚安静下来的注意力又被带走。" : "时间又过去一格，刚才的仪式被打断了。";
                case PracticeSet.Garden: return beat == 1 ? "水从盆边流走，根旁的泥土还是干的。" : "扶正后枝条再次垂下，旧支点承不住它。";
                case PracticeSet.Stage: return beat == 1 ? "人群移动，刚刚找到的位置听不清主旋律了。" : "最后一段变得很轻，杂音又盖过了它。";
                default: return beat == 1 ? "回头检查时才发现，中间漏掉了一个步骤。" : "快收尾时，一项新要求让原先的安排失效了。";
            }
        }
    }

    public static class PracticeFilms
    {
        // Each route pairs an observable action with its own physical performance.
        private static readonly string[] Rows = {
"walk|Street|Map|从门口走到河边长椅，坐下来透口气。|巷口在修路，刚走出的几步又退了回来。|Examine|展开地图，找到沿河的另一条小路|Walk|沿街走到下一个路口绕过围挡|河岸出现了。走到长椅旁时，紧绷的肩膀松了下来。",
"jog|Track|Shoe|沿跑道慢跑到十分钟铃响。|起步太快，到了弯道就不得不停下。|Rest|靠内道放慢呼吸，再小步起跑|Walk|先走过这段弯道，再逐渐加速|计时器响了，最后几步仍踩在自己的节奏上。",
"explore|Street|Map|沿一条没走过的街抵达广场。|旧地图上的路口被新围墙截断了。|Examine|摊开地图，用塔尖校准方向|Walk|走到街角，从另一侧绕进广场|广场的钟映入眼帘，陌生路线第一次连起来。",
"nightwalk|Street|Shoe|沿亮着路灯的小径走完一圈。|下一盏灯熄了，脚步停在明暗交界处。|Examine|停下确认有灯的回程路|Walk|沿亮着橱窗的一侧绕行|门前那盏灯再次出现，这次夜行走完了。",
"opportunity|Street|Folder|带着资料赶到约定的入口。|到了入口才发现指引牌换了位置。|Examine|核对资料上的房间号|Walk|走到服务台确认新入口|资料在截止前交到接待人手里。",
"visit|Cafe|Flower|把一束花带到老朋友的桌边。|走到旧座位时，桌边没有人。|Examine|查看约定信息里的新桌号|Walk|沿窗边走过去寻找朋友|朋友接过花，把空椅子拉近了。",
"friend|Cafe|Phone|给朋友发出一条具体的问候。|打了又删，手机上只剩空白输入框。|Write|写下今天看到的一件小事|Talk|录一段简短的语音问候|屏幕亮起回复，两人的对话接上了。",
"reconnect|Cafe|Ticket|用一张旧票根重新聊起当年的事。|说出第一句话后，两人都沉默了。|Examine|展开票根找当年的日期|Talk|请对方说说那天还记得什么|两个人一起指着票根，终于笑了出来。",
"ask|Cafe|Note|带着卡住的那一步去求助。|第一次解释太笼统，对方没明白问题。|Write|在便签上圈出具体卡点|Talk|用一个实例重新提问|对方示范后，自己把卡住的一步做通了。",
"listen|Cafe|Cup|陪朋友把一件难开口的事说完。|急着给建议，朋友把话收了回去。|Listen|放下杯子，等对方说完|Talk|复述刚才听到的那一句|朋友点了点头，终于把后半段讲了出来。",
"mentor|Workshop|Model|带着第一次试作请前辈点评。|样品站不稳，演示才开始就倒了。|Examine|和前辈对照底座的接点|Assemble|照示范重装支架|再次松开手时，样品稳稳立住。",
"dinner|Kitchen|Cup|把晚饭摆好，两个人坐下来吃。|两样菜同时要出锅，摆好的餐位乱了。|Sort|先腾出桌面再依次摆盘|Carry|请同伴接过餐具一起摆桌|最后一只杯子落桌，两人终于同时坐下。",
"together|Workshop|Folder|和同伴一起备齐明天的材料。|两人重复做了同一份，关键的一份却缺着。|Sort|把材料按人重新分堆|Talk|当面约定各自补齐的部分|两摞材料在桌上合成完整的一套。",
"collaborate|Workshop|Model|把两个人做的部件组装成样机。|接口方向相反，两半怎么也扣不上。|Assemble|旋转连接件，逐点试装|Talk|请同伴扶稳底座一起对齐|接缝闭合，两个人一起放开了手。",
"teamproject|Workshop|Box|让小组共同完成交付箱。|盖箱时发现两份材料规格不一。|Sort|按规格重新分放零件|Carry|把不合规格的零件送到另一张台返工|两张工作台上的成果终于装进同一箱。",
"teach|Library|Book|让同伴独立完成书上的一道练习。|讲完一遍，对方仍停在第一步。|Write|把例题拆成两步写给他看|Listen|先听对方把困惑说出来|同伴放下提示纸，自己写出了最后一步。",
"budget|Shop|Coin|把下周要留下的钱分到储备格。|小票里漏记了一笔，账面突然对不上。|Sort|按小票逐笔清点零钱|Write|写下非必要项目并重算预算|最后一枚零钱落进储备格，账目对齐了。",
"comfort|Shop|Cup|下班后选一份预算内的小安慰。|喜欢的那一份超出了零钱的额度。|Examine|看清小份的价签再选|Walk|到另一侧货架找替代品|把选好的小礼物带走，也看见了实际花费。",
"impulse|Shop|Basket|把一时心动的商品带到收银台。|结账金额亮起，比想象中贵。|Sort|把次要商品取出，只买最想要的一件|Examine|核对总价后确认这次支出|收据落到手心，这次购买的所得和代价一起留下。",
"overcommit|Desk|Folder|把答应的几项任务排进日程。|两项交付落在同一格时间里。|Sort|按期限重排，给最急的一份让位|Talk|联系委托人，重新约定交付顺序|日程终于排开，额外承担的负荷也清楚可见。",
"rushjob|Workshop|Clock|赶在约定时间把急件交出去。|初次装配没有通过检查，钟还在走。|Assemble|拆回有问题的一段重新装配|Carry|送到复核台，逐件检查再交付|急件赶上交付，疲惫也留在肩上。",
"commission|Workshop|Model|按委托要求做完一件小样。|试制品装不上客户指定的支架。|Examine|核对样本尺寸后调整|Assemble|换连接方式再组装|样机就位，客户接过了成品。",
"smalljob|Workshop|Box|帮同事把待发货件整理好。|第一箱放偏，叠上的另一箱滑了下来。|Sort|按重量重排再装箱|Carry|分两趟把箱子搬到交接台|最后一个箱子摆稳，同事接过交接单。",
"project|Workshop|Model|给长期项目装上一个能测试的模块。|装好的模块第一次测试没有响应。|Examine|逐点检查接触位置|Assemble|拆回最小模块重新试装|再试一次，模块按预期动了起来。",
"forge|Workshop|Model|把笔记里的一条原理做成实物。|照着图搭好，模型却向一边倾斜。|Examine|对照草图找偏移的支点|Assemble|调整底座再放回构件|松开手后，纸上的原理第一次站在桌面上。",
"create|Workshop|Model|把脑中的形状做成第一个模型。|拼到一半，原来的轮廓合不上。|Write|先画出真正需要的轮廓|Assemble|用少一块材料试出新形状|模型立在台灯下，灵感有了看得见的形状。",
"practice|Workshop|Model|把最不熟练的接合动作独立做对。|第一次用力不匀，零件弹开了。|Examine|停在接触点慢慢观察角度|Assemble|把动作拆成对齐和压合两次练|重新合上时，两个零件一次到位。",
"risk|Workshop|Model|用小样试一次尚未验证的做法。|样品第一轮失稳，整套做法需要调整。|Examine|只改变一个支点再测试|Assemble|缩小样品做一个可逆的试验|小样通过了检查，尝试有了可以检验的结果。",
"resilience|Workshop|Model|把上次失败的作品重新立起来。|裂开的接头又松开，作品歪倒了。|Examine|找出反复松动的那一个接口|Assemble|换一个支撑方向重新接合|这一次作品撑住了，手才慢慢移开。",
"portfolio|Desk|Folder|整理一份别人能顺序看懂的作品集。|翻到中间时找不到成果对应的说明。|Sort|按项目重新配对说明和样例|Write|给缺页补一段说明|逐页翻过去，每个作品都能找到它的来历。",
"publish|Desk|Folder|把完成的作品交到读者手边。|发布前预览发现首页遗漏了标题。|Write|补上标题与一句导读|Examine|重新对照清单检查封面|新版本出现在展示台上，第一位读者翻开了它。",
"plan|Desk|Note|把明天第一步写进日程。|列出的事情太多，第一格迟迟空着。|Sort|把任务排出先后|Write|只写下起手的十分钟|最前面那一格终于有了具体可做的动作。",
"review|Desk|Folder|根据记录找出今天卡住的环节。|凭记忆写的时间和记录对不上。|Sort|把纸条按发生顺序排好|Write|用原始记录改写关键一步|失误落到一个可改的环节上，复盘页合上。",
"study|Library|Book|读懂一个概念并独立做出一道题。|合上书再做时，笔停在第二行。|Examine|翻回例题核对推导|Write|换一张纸从已知条件重写|合上书，最后一行仍能独立写出来。",
"shortstudy|Library|Clock|在半小时内弄懂一页笔记。|闹钟走过一截，注意力还停在杂事上。|Sort|清走桌面，只留这一页|Write|先写出本页要回答的问题|铃响时，一页笔记已经可以用自己的话讲清。",
"library|Library|Book|找到需要的书并借回家。|书架上留下一个空位，目标书不在。|Examine|查看索引找同题的另一册|Walk|走到归还架查找刚还的书|借阅单夹进书页，一本有用的书带了回来。",
"imagine|Desk|Map|把明天可能失败的一刻和补救步骤演一遍。|第一次预演只想到失败，接下来的动作是空白。|Write|在失败那一格写出补救动作|Walk|起身实际走一遍重新开始的路线|再推演一次，失败之后也有了下一步。",
"perspective|Desk|Note|给同一件难事写出另一种解释。|第一张纸上只有对自己的指责。|Write|用旁观者口吻重写事情经过|Examine|找出事实和推测各是哪一句|两张纸并排放好，下一步有了不同的走法。",
"commit|Desk|Note|把一个承诺落到日期和行动上。|写下的愿望没有期限，无法核对。|Write|写明日期和可以检查的结果|Talk|向同伴说清交付的那一步|签下名字时，承诺有了能兑现的时刻。",
"trigger|Desk|Box|让明早一坐下就能开始工作。|试着开工时，需要的工具仍找不到。|Sort|把工具按使用顺序摆在手边|Carry|把明早的材料搬到桌面中央|再坐下时，手一伸就碰到第一件工具。",
"avoid|Desk|Folder|为暂时放下的任务找到归处。|随手塞好的文件又滑回桌面。|Sort|单独归档并标记待办|Write|写明再次打开它的日期|桌面空了出来，未做的事也留下了痕迹。",
"rest|Bedroom|Clock|把夜里的最后一件事收好，上床休息。|刚坐到床边，手机又亮了。|Carry|把手机放到远处的架上|Rest|调暗床头灯，慢慢躺下|床头安静下来，人终于躺稳了。",
"again|Bedroom|Note|休息后重新做完一个小步骤。|刚看到整叠任务，人又退回椅背。|Sort|只留最上面的一项|Write|写下可以立刻动手的开头|重新坐直，桌上的第一项被划去了。",
"breathing|Bedroom|Clock|给自己留出十分钟安静呼吸。|刚开始计时，肩膀还是越抬越高。|Rest|坐稳，让呼气慢慢变长|Examine|跟着钟摆找回节奏|铃响时，肩膀放下，双手也松开了。",
"solo|Bedroom|Cup|独自安静地喝完一杯水。|几条通知让手反复伸向屏幕。|Carry|把手机放到屋子另一头|Rest|捧着杯子坐回窗边|杯子见底，这段时间完整地留给了自己。",
"weekend|Bedroom|Book|给周末留下一段安静阅读的时间。|杂物和待办堆满了原先的座位。|Sort|清出窗边的阅读角|Carry|带着书换到安静的一头|坐定后，书页终于连续翻了下去。",
"scroll|Bedroom|Phone|看看这一晚的消息，再把手机放下。|一条接一条的推荐让手停不下来。|Examine|查看时钟，读完当前这一条|Carry|锁屏后把设备放到远处|屏幕熄灭，钟面也留下了已经过去的时间。",
"episode|Bedroom|Phone|看完选择的这一集，结束今晚的播放。|片尾一到，下一集已经自动开始。|Examine|找出播放设置并关闭连播|Carry|带着遥控器走到屏幕前关机|画面暗下，今晚的观看在这里收住。",
"play|Bedroom|Phone|玩完约定的一局并退出。|结算页弹出再来一局的邀请。|Examine|核对用时后退出房间|Carry|放下设备，把手柄收回架上|结算画面消失，这局的快乐和用时都留下。",
"music|Stage|Record|从头到尾认真听完一张专辑。|第一段还没听完，手就想切歌。|Listen|把手收回来，跟住低音线|Examine|看一眼曲序再回到当前段落|末曲结束，唱片慢慢停转。",
"concert|Stage|Ticket|找到自己的位置听完现场演出。|站在人群后面，舞台声被聊天声盖住。|Walk|沿侧面通道换到合适的位置|Listen|留在原地跟住主唱的声音|尾音落下，掌声从身边响起。",
"celebrate|Kitchen|Cup|为刚完成的小进展留一刻庆祝。|举起杯子时，脑中只剩还没做完的事。|Write|在纸上记下这次完成的具体一步|Talk|对同伴讲讲自己跨过的那个坎|杯子轻轻碰在一起，这次进展被记住了。",
"tidy|Bedroom|Box|把地上的物件归位，清出能走的路。|刚塞进一箱，东西又从盖子边挤出来。|Sort|按用途重新分放物件|Carry|先把大件搬回固定位置|箱盖合上，脚下出现了一条干净的路。",
"garden|Garden|Flower|让窗边歪倒的花重新立稳。|直接扶起叶片，松手后枝条又垂下。|Examine|查看土壤，先处理根旁的松土|Assemble|加一根细支架再轻轻固定|手离开花盆，枝叶仍稳稳向着窗光。"
        };
        private static readonly Dictionary<string, PracticeScript> catalog = Build();
        private static Dictionary<string, PracticeScript> Build()
        {
            var result = new Dictionary<string, PracticeScript>();
            foreach (string row in Rows) result.Add(row.Substring(0, row.IndexOf('|')), new PracticeScript(row));
            return result;
        }
        public static PracticeScript For(string id) { return catalog[id]; }
        public static IEnumerable<string> Ids { get { return catalog.Keys; } }
    }
}
