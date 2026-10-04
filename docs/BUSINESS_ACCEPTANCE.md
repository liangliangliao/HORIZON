# HORIZON v0.3 业务功能验收

本文保留 v0.3 的历史基线，以下 73 项检查和「展望」范围不代表最新版本。当前 v0.5 已补齐多次预测、六轴/时间跨度、七章剧情及真正可操作的 30 天人生；最新契约与验证见 [完整玩法升级](COMPLETE_GAME_V05.md)。

原业务基线保留；本轮继续改进拖拽、逐条结果和三维呈现。下面的 16 个功能组均有规则实现与入口；运行验收以对应提交的 Unity 工作流为准。

| 功能组 | 规则契约与可操作入口 | 检查 |
| --- | --- | --- |
| 1 开始与继续 | 首局直接开始、恢复活动人生；编号取最大存档编号，避免复用奖励标识 | GameSession、BusinessRule、PlayableFlow |
| 2 每天三选一 | 三牌、一次行动、六资源成本、始终可用的恢复牌、资源封顶 10 | GameSession、ObservationDesign |
| 3 投放提交 | 金色椭圆内松手成功，圈外归位；点击说明与使用备用；回声日期由牌固定 | PlayExperience、PlayableFlow |
| 4 状态查看 | 首局三维度、次局六维度；低值/拖牌/长按显示数字，松手恢复；全状态入口 | GameSession、PlayableFlow |
| 5 回声与反馈 | 固定到期、记录真实封顶效果、源行动解释；结果保留、读取与一次领取 | GameSession、PlayExperience、PlayableFlow |
| 6 因果与连锁 | 多来源图、二阶替换行动、支援成长合流、CASCADE 判定、节点追溯 | GameSession、ExperienceContent |
| 7 世界变化 | 第三局起环境事件有概率；种子存档；读档和观察不重抽；回声按时兑现 | ObservationDesign |
| 8 预测理解 | D4 锁定、D7 早上对照、可跳过、真实原因合计；1/3/10 只解锁信息，确认一次，无货币奖励 | GameSession、BusinessRule、PlayableFlow |
| 9 凝视未来 | 局内每天一次并保存；待回声、预计状态、截止日距离、III 两种条件未来；长展望共享预算 | GameSession、ObservationDesign、BusinessRule |
| 10 未来站 | D4 行动后进入、推进与段落恢复；三条最大真实根链；第三次选近期常用行为，仅改变观察关注，解锁 III | ExperienceContent、BusinessRule、PlayableFlow |
| 11 截止日与幻影 | 三门同时检查资源与历史准备；按规则重演源行动、后续选择和资源/门结果，无改善也如实显示 | GameSession、ExperienceContent、PlayableFlow |
| 12 快速重开 | 收藏、一次领取、时间线坍塌后直接新局，无串联结算或广告页 | PlayExperience、PlayableFlow |
| 13 人生档案 | 真实行为标题、改名保存、检索筛选、跨局回声、节点详情、多天分支；原人生与钱包不改写 | ExperienceContent、BusinessRule、PlayableFlow |
| 14 七章成长 | 不同回访日累计、不重置；六章是三天比较练习，不能提前开放三十天；七章沿用真实人生历史续推 | ObservationDesign、BusinessRule、PlayableFlow |
| 15 稀有事件 | 3–5 局间隔、保存与主动确认；旧记忆与真实回声风暴；新无因之果 D6 先收到结果，D9 后揭因 | ExperienceContent、BusinessRule、PlayableFlow |
| 16 分享与外观 | Unity 实录十秒 GIF、重演核实文案、玩家发起系统分享；星尘仅买外观，不买准确率或撤销人生 | ExperienceContent、PlayExperience、PlayableFlow、GIF 解码 |

## 本轮修正

- 预测确认先提交已读状态，再累计理解；同帧重复点击与恢复后确认均不重复领取理解。首局解锁方向也会实际显示。
- 未来站按根因果链大小、近期行为频次选取内容；不再强塞每类一项，也不虚构未用行为。关注只改变标签与同日排序。
- 三十天预览沿用当前或最新完成的人生，包括已走行动、事件种子、稀有结果。第六章采用独立练习；第七章真实人生预览仍受每日观察限制，首页不能绕过。
- “无因之果”先给真实余力，来源暂时隐藏；D9 揭示不再次发放。相同选择重演还原结果，改变源行动消除该特定结果。旧人生保留旧记忆版规则，不补发资源。精力和心情已满时优先转为可容纳的理解或能力；保存事件效果，旧版本 6 未存效果的事件仍按原效果重演。
- 长按状态显示数字；精力用尽提示恢复；幻影显示因此改变的后续行动与资源；编号避免重复，自动标题以行为生成且保留手动改名。
- 未知来源在地图与连锁演出都保持隐藏到 D9，真实链长度和奖励仍按完整因果图计算。

## 范围与检查

完整 0–35 节映射见 [V03_ACCEPTANCE.md](V03_ACCEPTANCE.md)。固定回声日期、明确投放圈与首局三资源为清晰操作及渐进学习的具体取舍。新规则为版本 6、牌池 4，旧局保持旧牌池、门槛、资源效果和重演方式。

共 73 项 Unity 检查：GameSession 21、PlayExperience 6、ExperienceContent 13、ObservationDesign 7、BusinessRule 14、PlayableFlow 7、FeedbackInteraction 5。工作流先验证规则与真实按钮/手势、22 张竖屏画面及十秒 GIF，全部通过后才构建 ARMv7 / ARM64 APK。通过状态以该提交的 CI 为准。

本轮已继续制作曲面网格、场景灯光、Bloom、回声轨迹及结果呈现；成熟美术、动画自然度与剧情演出质量仍需持续迭代。另需设备或真人验收：触控/性能/原生分享、前一小时用时、乐趣与现实迁移。支付、Steam、完整三十天战役及后续大规模商城未列为 v0.3 强制功能。
