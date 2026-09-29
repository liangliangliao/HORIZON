# HORIZON / 远见

Unity 竖屏游戏原型，取自《HORIZON / 远见》v0.3 的第一阶段。玩家每天把一张行动牌向上推入未来；即时变化立刻可见，埋下的回声在之后某一天返回。12 天后，截止日把整条时间线展开，检视能力、状态与支援。

## 打开与试玩

1. 用 **Unity 2022.3.22f1** 打开仓库根目录，等待 `com.unity.ugui` 和 Test Framework 导入。
2. 打开 `Assets/Scenes/Boot.unity`，按 Play。空场景由 `HorizonApp` 在运行时构建界面，无需手动放置预制体。Game 视图建议设为 **1080 × 1920** 或 9:16。
3. 第一次进入点击「看看」；之后每天**向上拖动**一张牌越过屏幕中部。点牌可以短暂查看资源数字。长按未来区域 0.65 秒可每天凝视一次；右上角可查看时间地图。
4. 第 12 天自动播放截止日与可能分支，然后直接进入下一局。重启应用会在简洁首页继续保存的局或查看过往记录。

Android/iOS 目标为竖屏、单指操作。运行时请求竖屏并根据 `Screen.safeArea` 布局；中文字体从系统已安装字体中选择。制作正式包时应加入已授权的内置字体，并在 Player Settings 中固定默认方向。界面、场景和粒子目前由 uGUI 程序化绘制，无需外部美术资源。

## 目前可玩的纵切片

- 每天三张意图明确的牌：即时快乐、长期成长、恢复；能量不足时成长牌不能打出，恢复牌始终可用。
- 三项资源以小圆点显示，低资源或点牌时显示数值。卡牌以 0/80/160 ms 升起，拖动时出现因果光线与未来目标。
- 未来队列、长线收益、即时快感的隐藏代价；首次 TIME ECHO 放慢播放，之后加速。
- 每日一次 FOCUS MODE；第二局解锁 Horizon II 的节点类型提示。
- 12 天三门结算、可能分支提示、自动重开；当前与已完成的时间地图、回声档案，以及 PlayerPrefs 本地保存。

原型中的未来站只是已完成局的氛围入口。预测绘制、完整未来站演出、二阶因果、CASCADE、分享动画、长期内容和商业化均在后续阶段；目前不将它们伪装为已完成玩法。

## 检查

Unity 内打开 **Window → General → Test Runner → EditMode**，运行 `GameSessionTests`。测试覆盖首日手牌、延迟回声、即时成本、零精力恢复、12 天结算与局内恢复。也可以在具有 Unity Editor 的机器上运行：

```bash
Unity -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults TestResults.xml -quit
```

当前开发环境没有 Unity Editor，提交前只能进行源文件与项目结构检查；实际导入、编译、设备比例、字形和动效节奏仍需在 Unity 与手机上验证。

## 下一阶段

先用 9:16 设备试玩记录拖动成功率、TIME ECHO 识别率和一局完成时间；再做玩家亲手绘制预测、Day 4 未来站完整分镜、因果链和 CASCADE。保持规则逻辑在 `Assets/Scripts/Game`，把表现与存储放在 `HorizonApp` 和 `Assets/Scripts/UI`，便于以后用正式视觉与音效替换。
