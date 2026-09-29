# HORIZON / 远见

Unity 竖屏游戏原型，取自《HORIZON / 远见》v0.3 的第一阶段。玩家每天把一张行动牌向上推入未来；即时变化立刻可见，埋下的回声在之后某一天返回。12 天后，截止日把整条时间线展开，检视能力、状态与支援。

## 打开与试玩

1. 用 **Unity 2022.3.22f1** 打开仓库根目录，等待 `com.unity.ugui` 和 Test Framework 导入。
2. 打开 `Assets/Scenes/Boot.unity`，按 Play。空场景由 `HorizonApp` 在运行时构建界面，无需手动放置预制体。Game 视图建议设为 **1080 × 1920** 或 9:16。
3. 第一次进入点击「看看」；之后每天**向上拖动**一张牌越过屏幕中部。点牌可以短暂查看资源数字。长按未来区域 0.65 秒可每天凝视一次；右上角可查看时间地图。
4. 第 4 天先在三条轨道上上下拖动，预测第 7 天的精力、心情和洞察，再封存预测。当天选牌后向上滑动三次靠近未来的自己；第 7 天回声兑现后，对照预测与真实结果。
5. 第 12 天自动播放截止日与可能分支，然后直接进入下一局。重启应用会在简洁首页继续保存的局或查看过往记录。

Android/iOS 目标为竖屏、单指操作。运行时请求竖屏并根据 `Screen.safeArea` 布局；中文字体从系统已安装字体中选择。制作正式包时应加入已授权的内置字体，并在 Player Settings 中固定默认方向。界面用 uGUI 程序化绘制，背景使用 `Assets/Resources/HorizonSky.jpg`。

## 目前可玩的纵切片

- 每天三张意图明确的牌：即时快乐、长期成长、恢复；能量不足时成长牌不能打出，恢复牌始终可用。每类六张行动，按局数错位出现。
- 三项资源以紧凑状态轨道显示，低资源或点牌时显示数值。卡牌以 0/80/160 ms 升起，拖动时出现因果光线与未来目标。
- 未来队列、长线收益、即时快感的隐藏代价；首次 TIME ECHO 放慢播放，之后加速。
- Day 4 轨道绘制预测，Day 7 将预测与实际状态叠合；接近时解锁未来方向信息，不发放数值奖励。Day 4 打牌后进入可滑动的局内未来站。
- 每日一次 FOCUS MODE；第二局解锁 Horizon II 的节点类型提示。
- 12 天三门结算、可能分支提示、自动重开；当前与已完成的时间地图包含行动到回声的因果线，连同预测与未来站进度保存在本地。

未来站已经进入第 4 天的主循环，但仍是三段短互动；方案中 40～60 秒的完整演出、二阶因果、CASCADE、分享动画、长期内容和商业化尚未实现。

## 检查

Unity 内打开 **Window → General → Test Runner → EditMode**，运行 `GameSessionTests`。测试覆盖首日手牌、延迟回声、即时成本、零精力恢复、预测兑现、未来站与 12 天结算。也可以在具有 Unity Editor 的机器上运行：

```bash
Unity -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults TestResults.xml -quit
```

当前开发环境没有 Unity Editor，本地只能进行源文件与项目结构检查；GitHub Actions 会实际导入和编译 Android 项目。设备比例、字形、触控与动效节奏仍需在手机上验证。

## Android APK 工作流

`.github/workflows/android-apk.yml` 在指向 `main` 的 PR 更新或 `main` 收到提交时运行，也可在工作流进入默认分支后从 Actions 页面手动启动。它使用 Unity 2022.3.22f1 构建 Android APK，并将结果保存为 `HORIZON-Android-APK` 工作流产物（保留 14 天）。这是用于试玩的 APK；上架前需要另配正式签名与发布配置。

首次构建前，在仓库 **Settings → Secrets and variables → Actions** 设置 Unity 授权：个人版使用 `UNITY_LICENSE`（`.ulf` 文件完整内容）、`UNITY_EMAIL`、`UNITY_PASSWORD`；Pro 使用 `UNITY_SERIAL`、`UNITY_EMAIL`、`UNITY_PASSWORD`。不要把授权文件或密码提交到仓库。缺少密钥时工作流会在授权检查步骤明确报错。

## 下一阶段

先用 9:16 设备试玩记录拖动成功率、TIME ECHO 识别率、预测绘制手感与一局完成时间；再扩展未来站演出、二阶因果和 CASCADE。保持规则逻辑在 `Assets/Scripts/Game`，把表现与存储放在 `HorizonApp` 和 `Assets/Scripts/UI`，便于以后用正式视觉与音效替换。
