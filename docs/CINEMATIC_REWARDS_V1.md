# 电影式动画与实体奖励 v1.0

当前修复与功能补齐请见 [两份 v1.0 方案独立核对](REWARD_CINEMATIC_ACCEPTANCE_V1.md)。下文的历史 CI 数字不能作为当前功能提交的验收。

基于 main `9527132`，产品规则基线仍为 Master v0.4.2。本次实现依据用户提供的《Master Product Specification v0.4.2》《Reward System Specification v1.0》《Cinematic & Animation Specification v1.0》三份 DOCX。

## 实现

- GameSimulation → DomainEvent + RewardReceipt → RewardDirector → CinematicDirector。事件在发出前保存实际资源增减、行动、来源日期、可观察因果图、历史中断证据、轨道和视野状态。可视层拿深拷贝；声音、文字、相机、震动与物品不调用资源结算、不读取后来变化的游戏状态。
- 六资源保留中文增减文案，分别以能量电芯、暖光灯、聚焦透镜、钱包、双端连接环和工具箱呈现。负变化使用离开、变暗与收束减弱；实际数量达到 20 后改用更大的设备，避免几十或几百个实例。数量、实体规模与演出倍率分开。
- 既有机制物件包含预测板、洞察棱镜、望远镜、投影机、机械锁、行动提示、修复包、胶片、储存核心、现实里程碑和三棱汇流体。纪念物从实际事件生成、去重并存入人生备份；旧存档按已有历史事件补齐收藏，不补发资源或星尘。
- Micro / Local / Combo / Major / Epic / Mythic 分别使用 0.24 / 0.75 / 1.6 / 3.6 / 6.8 / 11.8 秒。重大事件保留原文、事件名称和实际资源文字。Epic/Mythic 共用预兆、蓄力、升级、主体登场、Hit Stop、冲击、倍率、二次揭示、大奖、结算、回游戏的骨架。
- TIME ECHO 回到真实原行动，暂停后沿因果图回到今天；来源日期不会从后来的状态推测。Cascade 的节点间隔为 0.35、0.28、0.21、0.15 秒，终点停顿 150ms 后整个网络点亮。九层 Causal Singularity 可使用 Mythic，但仍受每局最多两次 Mythic 的既有预算限制。
- Pattern Broken 只显示实际记录的历史中断线和玩家真实的新动作。旧线在同一点中断，当前角色继续穿过，旧线碎裂，未来自己出现，断裂链环进入收藏。不虚构五次失败，也不虚构 ×100 或 ×500。
- Reality Node 先以有重量的里程碑落地；存在已记录的 Imagination、Simulation 和 Reality 三个节点时，才播放三线合流。现实任务完成后逐个展示事件，避免跳过 Reality Node。节点和收藏都不随重玩重复领取。
- Imagination 保留原状态机与操作：Victory Anchor → 倒退到今天 → 准备与努力 → 失败 → Recovery → 再行动 → 改变策略 → 抵达 → Future Memory。角色前进、跌到下层、抓住支点、修路及使用已选择的支持都来自当前预演状态，电影不代替玩家作恢复选择。
- Orbit、Reservoir、Inner Council、Knowledge Forge 和既有 Thought Monsters 增加可观察的三维空间表现；文字与选择仍由原系统提供。储备检查页面不会随机打开核心或触发结算。Future Station 保持安静的现有慢节奏。

## 工具链与性能

Unity 6 LTS **6000.0.62f1**，URP **17.0.4**，Cinemachine **3.1.3**，Timeline **1.8.7**，Animation Rigging **1.3.0**。人物动作仍以项目内程序化关节动画制作；本次使用 Animator 和运行时 Animation Rigging 视线约束接入导演时钟；未制作完整角色 Animator Controller 或 Shader Graph 美术资产。电影相机由 Cinemachine 手动更新，PlayableGraph 与一个可暂停的纯规则时钟控制整个感官时间线。字幕使用 TMP 与原项目已授权的中文字体。

编辑器自动创建移动端 Forward URP 配置；构建前也会检查该配置。LitColor 保留旧渲染 SubShader 并添加 URP 光照与阴影；阴影及深度通道在本着色器内编译，避免 Unity 6 对跨着色器 UsePass 的关键字空间断言。Bloom 使用 URP Volume；Android GLES3 禁用不兼容后处理链，保留场景光照和自发光。竖屏导出通过真实 URP 场景纹理和完整 Canvas 合成，解决 URP Base Camera 清屏覆盖部分 viewport 的问题。全屏演出暂时隐藏原页面的按钮、文字及背景，退出时恢复。

三维舞台、物件和冲击粒子复用。普通冲击最多 64 粒子，省电画质最多 12；时间碎片省电画质由 32 降为 8。省电模式保留全部镜头分镜、主体、文字、真实倍率和结算。减弱动效使用固定宽镜头，关闭白闪、相机震动和粒子，保留核心动作和意义。

暂停会冻结时间线、关节、粒子与字幕闪光，Hit Stop 完全静音。跳过丢弃剩余感官 cue，只确认已结算事件；取消页面恢复相机，未完成事件保持待展示。日常收据的“继续”明确结束该批演出，独立确认每个已排队事件。重复播放不产生资源、货币或收藏。

## 原分支历史验证（不代表本提交或两份 v1.0 方案覆盖率）

- 本地已通过 **244/244** 项便携测试，包括原 223 项规则回归与新增 21 项奖励测试：六资源映射、符号、实际截断后的数量、快照隔离、旧事件兼容、真实模式证据、三线现实证据、十一段导演语法、递进节点节奏、150ms 停顿、暂停/慢帧/跳过一次性语义、收藏迁移与去重、Overdrive 临时视野隔离和未揭晓因果来源保护。
- 源码提交 `4959d79` 的真实 Unity 6 CI 已通过 **307/307** 项测试，包含完整可玩流程及下列动画测试；两个 HTTP 服务检查也通过。原有 53 章、207 项验收报告为 **89.6855%**，通过既有 85% 门槛，未降低门槛或跳过失败测试。
- `CinematicPlayableTests` 在仓库 CI 的真实 Unity 场景中验证暂停/跳过/取消、相机恢复、省电粒子预算、主体与旧线保留、储备核心复用后的关闭状态、纪念物序列化恢复，以及真实 Pattern Broken、Reality Convergence 的竖屏渲染。预期产物 `90-pattern-3d.png`、`91-reality-convergence-3d.png` 已接入 CI 检查与上传；结果以该提交的 Actions 日志及测试 XML 为准。
- 同一 CI 导出的 **91 张**实际 PNG 预览、60 帧/10 秒 GIF 和含 AAC 音轨的 10 秒 H.264 MP4 已下载并通过本地解码校验。动画预览为 540×960，常规预览为 1080×1920，两张长屏预览为 1080×2400。校验脚本同步增加两张动画预览，仍要求完整编号、正确尺寸及非空画面。
- 全部 C# 文件已通过 Roslyn 语法检查，仓库 CI 已成功完成 Unity 6 类型编译并运行图形测试。本地 Unity 编辑器镜像因 Docker 存储空间不足未能安装，且没有可用的本地 Unity 激活凭据。完整 Unity EditMode/可玩流程、实际竖屏预览及 Android ARMv7/ARM64 构建由仓库 CI 验证；编译通过不代表这些验收全部通过。

本地便携测试可以使用 `dotnet run --project tools/RulesHarness.csproj -- --workers=0`。图形测试必须使用 Unity 6 和图形设备，不能用 `-nographics`。

CI 在临时容器中使用 `UNITY_EMAIL` 与 `UNITY_PASSWORD` 在线激活 Unity；Pro 账号还可提供 `UNITY_SERIAL`。测试与 Android 构建统一使用 Game-CI CLI v0.1.72，Android 构建器固定为 v6.0.0。不向临时容器传入旧的 `UNITY_LICENSE` 文件，避免旧 `.ulf` 的时间戳或机器绑定阻止 Unity 6 启动。仓库中已有的 secret 无需删除，工作流只使用上述账号配置。

## 验收边界

模型、动作与音效是项目内程序化资产。三份规格中的概念、文字、动作方向和事件证据有工程覆盖；参考视频未随本次附件提供，不能声称已完成逐镜头视频品质对照。60FPS/最低30FPS、真机触控、音画震动的设备级同步、第一小时节奏、成熟美术品质和情绪效果需要真实手机与玩家验收。不得用规则测试或截图代替这些结论。
