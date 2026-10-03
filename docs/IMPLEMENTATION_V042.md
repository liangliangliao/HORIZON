# v0.4.2 实现与验收

开发分支：`feat/horizon-v0.4.2`，基于最新开发提交 `ac2d359`。规则版本 9，牌池版本 7，MasterRunState schema 1。旧规则版本、旧牌池、原短局/长局、存档备份、实际回声收据、因果地图、分支重演、十秒 GIF 与外观钱包继续保留。

## 已实现的可玩系统

| 系统 | 行为与入口 | 主要实现 |
| --- | --- | --- |
| 选择、时间、预测、Boss | 原竖屏卡牌与 12/30 天主局，延迟回声、三道门和多次预测 | GameSession、CampaignRules、CardCatalog |
| 卡牌家族与选择空间 | 八张新牌；实验、镜像、混沌、想象模式进入扩展牌池；成熟因果机会优先保留 | MasterSpecification、MasterContent |
| Decision Lock | 点牌后 LOCK；四步执行；主动解锁记录重新比较；执行中禁止直接换牌 | DecisionExecution、GameSession.Master |
| Action Engine / Trigger | ME → 行动发动机；降低摩擦；最多装备三项环境提示 | HorizonApp.Master |
| Thought Monsters / Council | 动机权重分解、来源展开与念头的合理部分/代价 | InnerCouncil、ThoughtMonsters |
| Imagination / Recovery | ME → 想象；输入目标、选择难度；强制 1/3/4 次挫折与恢复；过程可中断继续 | ImaginationEngine |
| Future Memory / Déjà Vu | 完成想象后留下记忆；主局相似挫折情境采取匹配动作后连接真实模拟证据 | GameSession.Master、MasterArchive |
| 想象对照 | 第一处 Divergence Point 的规则接口与测试；剧情阶段与原主局真实因果分别保存 | ImaginationEngine.Compare |
| HORIZON ME / Pattern | 最近 60 条情境观察；至少两条人生出现失败结构；镜像模式展示近期结构；一次不同动作触发突破 | PlayerModel、PatternEngine |
| Knowledge / Worldviews | If / Then 技能逐阶段附证据；八种认知工具可以加入个人卡组 | KnowledgeForge、HorizonApp.MasterJourney |
| Reward / Spectacle | 六级领域事件、每局两次 Mythic 上限、日内 Overdrive 贡献上限、Pattern Broken / Comeback / Overdrive / Déjà Vu 演出 | RewardEngine、MasterFeedback、MasterSpectacleGraphic |
| Orbit / Reservoir / Cascade | 八方向轨道；六次真实投资在能力/状态/关系/机会条件成熟后兑现；连锁沿真实图计算 | GameSession.Master、FutureOrbitGraphic |
| Opportunity / Momentum | 因果机会当日有效；立即接住减一点精力成本；次日关闭并记录消退 | OpportunityWindow、ImmediateEffect |
| Reality Bridge / Constellation | 每日本地日期一个自愿小动作；完成不可重领；三种证据齐备连接现实星座并触发 Convergence | RealityBridge |
| Parallel Lives / Future Message | 相同起点的本地反事实重演与对比；经验保存、剪贴板导出/导入，重复消息去重 | ParallelLives、MasterJourney |
| 内容与统计隔离 | IAIAdapter 仅返回内容字段，默认本地规则内容，明确 generatedByAI=false；本地事件计数不包含玩家文本 | AIAdapter、AnalyticsLedger |
| 保存与重演 | 新系统随快照持久化；初始模式、模式证据、未来记忆、理解点与知识版本随人生保存；准备操作按日期重演 | MasterRunState、MasterCommand |

## 运行

用 Unity **2022.3.22f1** 打开 `Assets/Scenes/Boot.unity`，以 1080×1920 竖屏 Play。首局第 5 天起、后续人生开放 ME 入口；主页可进入想象和现实。完成当前人生后从 ME → 人生模式选新局。世界观与装备支持结构化选择，资源结算不调用内容生成器。

恢复入口：继续一生恢复执行状态；ME → 想象恢复未完成预演；奖励确认位置保存在事件记录中；现实节点跨局保留。Android 返回键可关闭新面板并执行其返回动作。

## 验证

本地无 Unity 编辑器时可执行独立规则验证：

```bash
dotnet run --project tools/RulesHarness.csproj -- --workers=0 --result=artifacts/rules.xml
```

此 runner 使用 NUnitLite 和 Unity 公共字段 JSON 契约的兼容实现，只验证领域规则；不能替代 Unity JsonUtility、渲染、生命周期或 Android 设备测试。旧规则回归与新 Master 规则共 83 项；覆盖 8 模式 × 8 种子、存档重载、命令重演、强制恢复、跨局模式证据、奖励幂等、真实图父节点、积累兑现、机会窗口和现实证据要求。

`MasterPlayableFlowTests` 在真正 Unity Play Mode 操作 LOCK、Trigger、四步执行、目标输入、想象失败恢复、保存记忆、轨道、现实任务与星座，生成 `40`–`44` 号竖屏截图。原 39 张画面和全部旧 EditMode/Play Mode 回归继续执行。CI 分别上传规则 XML、Unity 结果、竖屏图、GIF 与 ARMv7/ARM64 APK；必须看当前提交的结果，不能沿用旧提交的成功状态。

## 尚未宣称完成的生产能力

- 在线 AI 尚未接入。没有 API 密钥，也没有调用外部模型；接口与可运行的本地内容独立存在。需要凭证选择后再配置受保护的服务端代理，不能把 API 密钥放进 Unity/Android 客户端。
- Parallel Lives 当前为本地同起点重演；Future Message 通过玩家主动复制/导入交换，尚无在线多人会话、帐号或同步服务。
- Long Run 当前为 30 天长期内容变体；没有无限局。十种 Boss 状态还需要逐个设计独立内容与平衡，当前可玩 Boss 保留截止日三道门，并在行动发动机中展示思维怪物。
- 想象与真实时间线的自动语义匹配尚需扩展，当前比较接口按动作序列寻找第一处分歧；无法据此宣称现实问题已被系统理解。
- 本轮新增美术、声音与动效仍为程序化资产。震动语言、触控与功耗需 Android 真机验证，15–25 分钟节奏、情感效果、五层体验与长期留存需真人验收。
- 现实行动依赖玩家亲自确认。去重和日期约束能阻止重复领取，不能通过软件证明现实动作真的发生。

后续开发只更新 v0.4.2 验收矩阵；旧文档作为历史参考。
