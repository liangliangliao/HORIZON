# 参考捕鱼中奖演出的三维渲染升级

开发基线：仓库最新分支 `fix/apk-rewards-animation@c9a1570`。升级分支：`feat/3d-jackpot-animation`。

参考五张截图的视觉语言：金红色立体宝箱、镜面金属边框、紫色光球、放射光束、开箱爆光、金币飞散和有厚度的金色倍率。保持 HORIZON 的竖屏玩法、角色、真实因果证据和奖励规则。

## 已接入游戏

- 全场景共用的 `HorizonLit` 增加金属度、清漆、边缘光、环境反射色带和逐光源高光。金币、宝箱、角色道具与日常场景均使用升级后的材质。
- `HorizonWorld3D.Jackpot` 接入既有导演的开始、采样、画幅调整和退出流程。Major 起可呈现立体宝箱；Epic/Mythic 在原事件的二次揭示后开箱，保留前面的因果分镜。Surprise、隐藏来源回声采用紫色光球；负收益与准备事件不会喷金币或开箱庆祝。
- 宝箱拥有开合铰链、拱形盖、金色包边、铆钉、卷纹、紫宝石和皇冠。倍率牌使用烘焙的真正三维数字网格，带侧面和倒角，不依赖平面字体假装厚度。数字由仓库内 SIL OFL 字体生成，来源与许可仍见 `Assets/Resources/HorizonSansSC-OFL.txt`。
- 光晕、放射光束和冲击闪光直接由透明着色器渲染；Android GLES3 不需要后处理即可看到这些效果。光束只读取导演时钟，暂停和 Hit Stop 不会继续旋转。
- 日常回声、星尘与截止日的飞散改用 64 槽复用对象池和带浮雕的金币网格。每次奖励不再创建和销毁一批临时物体。电影金币池最多 48 个，省电最多 8 个，自适应低画质最多 20/12 个。
- 减弱动效关闭金币、星屑、冲击闪光、数字弹跳与光束旋转，保留固定镜头、主体开合、静态光晕和文字。中途切换画质会更新当前画面；跳过和退出会清空舞台。

`RewardSpectacle` 只读取 `RewardPlan`。所有金币均为演出装饰；倍率牌标明「演出倍率」，不改实际金钱、星尘、六资源、因果深度或收藏。实际增减继续由原收据和字幕展示，重播不发奖。

## 验证与预览

便携检查：

```bash
dotnet run --project tools/RulesHarness.csproj -- --workers=0
```

新增 17 个便携用例验证六级演出的预算、真实资源快照、负面与准备事件、隐藏来源、暂停、跳过、尾部清理和非法时间。

Unity EditMode 新增 `JackpotRenderingTests`，从真实游戏事件生成金色宝箱与紫色光球，检查实体数字、暂停、低画质、减弱动效和重播隔离，并导出：

- `artifacts/cinematics/jackpot-*.png`：金色突破、现实合流、紫色神秘演出，包含正常、省电和减弱动效三档。
- `artifacts/cinematics/jackpot-gold-motion.gif`、`jackpot-violet-motion.gif`：360×640、20fps，由实际 Unity 相机录制的完整演出。

APK 工作流保留原 ARMv7/ARM64 构建及验收，并允许以最新修复分支为目标的 PR 运行。上述预览随 `HORIZON-Cinematic-Reward-Previews` 产物上传。Unity 6 的编译、着色器导入、实际截图与 APK 结果以本次提交的 CI 为准。

仅有静态参考截图，没有原始角色模型、贴图和动画视频。这次交付的是项目内可运行的程序化三维演出；不声称复刻参考游戏的商业角色资产或已通过手机帧率验收。

数字资产可重建：开发环境安装 `fonttools`、`shapely>=2.1` 后运行 `python3 tools/build_reward_digits.py`。运行游戏不需要 Python 或在线下载。
