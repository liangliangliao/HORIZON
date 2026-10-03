# HORIZON / 远见 — Master Product Specification v0.4.2

**v0.4.2 是唯一开发基线。** v0.1–v0.4.1 及仓库原先使用的 v0.5/v0.6 文档保留为设计与实现历史，不再作为并行产品规范。最新开发基础为 `codex/playable-feedback-3d@ac2d359`。

产品：时间策略 × Roguelite × 人生模拟 × 因果推演 × 自我探索。核心问题是“我现在做的事情，会把未来变成什么？”进一步观察重复选择、训练另一条路径，并用一次真实行动连接现实。

四个支柱：**TIME / FORESIGHT / IMAGINATION / PATTERN**。成长扩大信息视野；想象包括努力、失败、恢复和再行动；行为模型描述近期结构，不提供永久人格标签。快乐、休息和长期投资都具有价值，最终选择属于玩家。

## 统一循环

观察现在 → 想象未来 → 选择 → 行动进入时间线 → 即时结果 → 后果传播 → TIME ECHO → 预测与实际对照 → 困难与失败 → 调整、恢复、再行动 → 因果链兑现 → Pattern Broken / Reinforced → Future Self → 时间地图 → Reality Bridge → 下一条人生。

## 不变的约束

- 快局 12 Days，目标体验 15–25 分钟；另有 30 Days、Long Run、Parallel Lives、Imagination Run、Experiment Run、Mirror Run、Chaos Run。时长与趣味性需真人试玩验收。
- 六资源是精力、心情、专注、金钱、关系、能力；包括短期值、趋势、波动和对选择空间的影响。旧 JSON 字段 `insight` 保留，表示专注；奖励理解点独立为 `insightPoints`。
- 卡牌携带 Delay、EchoType、CauseTags、MotivationTags、Risk、Commitment、Visibility、Trigger、SecondOrderEffects、AIContext。行为家族涵盖刺激、投资、恢复、关系、收入、探索、承诺、提示、知识、世界观、风险、再战、想象、决定。
- CauseGraph 是统一依据：行动、回声、决定、执行、想法、恢复、预测、突破与结果都可链接。动画、声音、震动、UI、分享与本地统计消费领域事件，不参与胜负结算。
- Horizon I–VIII：现在 → 一天 → 三天 → 概率 → 二阶影响 → 平行未来 → 未来自己 → 隐藏变量。信息能力跨局保留。Overdrive 短暂提高视野，不改变回声概率。
- Decision Lock 完成决定，随后进入执行；可以解锁，必须记录执行时重新开启比较。Action Engine 包括 Motivation、Ability、Trigger、Friction、Emotion、Alternative Reward、Fatigue、Social Pressure。
- Trigger 是环境结构：闹钟、预约、朋友提醒、公开承诺、押金、路线、票、地点、Deadline、限制。念头有合理部分；内在议会展示动机权重与来源。
- Knowledge Forge：KNOW → RECOGNIZE → SIMULATE → EXECUTE → EXPERIENCE。每阶段需要证据。世界观提供视角，不能替代决定。
- Imagine Run：Victory Anchor → 回到今天 → 准备 → 努力 → 挫折 → Recover → Retry → 调整 → 抵达。普通目标至少一次失败；困难目标 2–4 次。关键恢复行为形成 Future Memory，相似情境触发 Déjà Vu。现实与想象应找出第一个 Divergence Point。
- Comeback ×2、×3、×4 表示韧性链，影响理解、记忆和演出，不提供货币倍率。同一天多条负面回声不能冒充多次重新开始。
- Personal Pattern 需要跨局重复证据；相似节点上的继续行动可触发 Pattern Broken，同一模式每局至多突破一次。
- Reward 六级：Micro、Local、Combo、Major、Epic、Mythic；每局 Mythic 最多两次。高层奖励保持稀有。Pattern Broken 表现过去的失败路径中断、当前路径继续；Reality Convergence 连接想象、模拟、现实。
- Future Orbit 八方向：工作、能力、财富、关系、健康、自由、意义、身份。全部点亮形成 ALL LINKED。Causal Reservoir 保存投资，条件同时成熟时兑现 Breakthrough。Cascade 使用真实因果链，达到高阈值形成 Causal Singularity。
- Future Station 是阶段间的情绪停顿；Future Self 回忆与提问，不宣布正确答案。地图是互有取舍的未来可能性空间。机会与 Momentum 有窗口。
- Reality Bridge 每个本地日期最多一个小任务，由玩家确认真实完成；不发金币，不重复领取。Reality Constellation 保存长期现实节点。三类证据齐备才允许 Reality Convergence。
- AI 只处理内容、理解、NPC、情境、解释和知识转行动。资源、概率、核心因果与胜负始终规则化：Rules keep it fair. AI makes it personal.
- UI 上方是未来，中间是现在，下方是行动与过去；拖牌高度不改变已标记日期。声音和震动应区分回声、成长、恢复、连锁、最高级事件，并服从玩家设置。
- 第一小时只教行为会回来、未来可预测、困难可准备、失败可恢复、不同选择形成不同自己；高级系统按经历开放。
- 留存来自重走人生、寻找未来自己、突破模式与现实连接。社交比较时间线，不做自律排行榜；Future Message 是经验回声。分享是十秒动态时间线故事。
- 商业化仅可出售外观、环境、音乐和章节；不能购买预测准确率、消除错误、提高奖励概率或商业化 Reality Quest。

## 交付与验收

实现范围、运行方法、验证证据和仍需产品验收的边界见 [v0.4.2 实现说明](IMPLEMENTATION_V042.md)。开发顺序仍遵循 Phase A（核心规则）→ B（体验与模式）→ C（个性化行动结构）→ D（奖励与现实桥）→ E（AI、社交与长期内容）。

完成标准包括：玩家能预见回声回来、辨认重复比较的节点、预演失败后的下一步，并在现实中亲自采取一次不同动作。自动测试只能证明规则与交互契约，不能证明这些体验效果。

**SEE FARTHER. IMAGINE DEEPER. FIGHT THROUGH. BREAK THE PATTERN. LIVE THE FUTURE.**
