# v1.0 余项实现与复验

本轮沿用原有 22 组奖励、28 组动画需求。实现状态与实际验收分开记录；以当前提交 CI 的 NUnit XML、场景截图和 Android 报告为准。没有手机帧率或触觉证据时，不填写真机验收通过。

| 原余项 | 实现 | 关键检查 |
| --- | --- | --- |
| R04 / A05 景深与基础身体表现 | 场景相机独立 URP Renderer Feature 按深度计算失焦；镜片聚焦后恢复清晰；低动态关闭失焦，暂停/跳过恢复状态。胸部呼吸、肩部与头部松弛由同一个导演时钟采样 | `DepthFocusChangesRenderedPixelsAndClearsAfterSkipping`；`BakedEffectsLodAndBodyBreathingRespectPauseAndReduction`；Android 原生景深帧与颜色检查 |
| R12 真实多帧胶片 | 离开已经到达的预演阶段时渲染关键帧：胜利锚点、努力、失败、改变策略、胜利。帧带 runId、真实 beatIndex、阶段和策略；最多 5 张 128×96 JPEG 随存档、不可变奖励回执和纪念物保存。回忆使用相应恢复策略的帧；电影中逐帧冻结并缩入胶片 | `ActualImaginationFramesPersistAndRenderInTheFilmstrip`；`MemoryFramesFollowRealBeatsAndCopiesCannotRewriteReceipts`；旧档/尺寸边界检查 |
| R13 投影世界 | 平面蓝图之后依次出现街道、房间、人物、障碍、未来场景；玩家走入投影。生成过程用扫描线/溶解材质，完成后恢复实体材质；未来房间等到最后一层才出现，木材、墙面和家具保留各自颜色 | `ProjectionMaterializesFiveLayersAndUsesCompiledShaderGraph` |
| A01 Shader Graph | `HorizonProjection.shadergraph` 为实际被加载使用的 URP Unlit 图。公开的 BaseColor、Reveal、Clock 连接投影函数及主栈；时钟由导演传入，暂停时不会自行流动 | Shader Graph 导入/编译检查；投影和未来人物的运行时材质断言 |
| A27 16 类特效 | 见下表；复用池内独立几何、光路、图表材质和烘焙序列，不依赖单一通用粒子改名 | `SpecializedEffectsUseDifferentGeometryAndSurfaces` 及上述胶片/景深检查；实际业务事件截图保持原有文案 |
| A28 移动优化与测量 | 人物三级距离 LOD，主演演出保留完整动作；现有对象池/静态合批/实例化；按质量预算粒子；离线烘焙两张 16 帧图集，尘埃与蓄能使用图集叠加实时 3D。持续超时后依次降低分辨率、阴影、装饰，恢复用更长滞后窗口，保留镜头、节奏、人物和文字 | 帧统计/滞后单元测试、低质量场景回归、Android 性能 JSON；实际手机 60/30 FPS 尚须采样 |

## 特效对应关系

| 特效 | 实际表现与入口 |
| --- | --- |
| Energy Trail | 正向精力物件后方的曲线轨迹，尖端跟随真实物件位置 |
| Time Trail | Time Echo / Cascade / Singularity 的时路带 |
| Causal Beam | 真实 receipt.causes 节点间的因果线与归因脉冲 |
| Shockwave | 重大结果落点的扩张地面环 |
| Node Activation | 已存在因果节点上的衰减扩张光环 |
| Screen Distortion | 重大落点的短时径向折射；只处理场景相机 |
| Future Hologram | 未来人物上的 Shader Graph 扫描线半透明投影 |
| Memory Film | 有来源标识的真实预演帧、冻结缩入和胶片格 |
| Dissolve | Pattern Broken 旧障碍按噪声阈值消散 |
| Time Fracture | Failure / Singularity 的分层时间碎片与独立旋转 |
| Pattern Crack | 旧中断点上逐步扩展的锯齿裂缝，与历史线破裂分镜对应 |
| Convergence Beam | IMAGINATION / SIMULATION / REALITY 三条独立能量流汇合 |
| Orbit Trail | 仅为已激活 orbitBits 绘制的椭圆轨道连接 |
| Reservoir Energy | 突破储能核心内部的预烘焙旋流图集 |
| Overdrive Horizon Glow | 远处地平线弧光扩张，随视野推进增强 |
| Reality Impact Dust | 现实节点落地的预烘焙环形尘埃，无重型流体模拟 |

## 真机复验

安装当前已通过 CI 的 APK。在设置中使用“运行表现 → 重新记录并继续”，分别在标准画质和省电画质游玩至少一分钟，覆盖普通行动、预演及重大演出，然后复制报告。报告包含设备/GPU/系统/分辨率、场景与质量档、样本时长、平均 FPS、P95 帧间隔、最慢帧与超预算比例。P95 使用每个场景/档位最近 1800 个样本；平均值包含记录期间所有有效帧，长停顿不会被过滤成高帧率。暂停与后台时间不计入，初次载入留出预热帧。

Android 模拟器只能验证 GLES 兼容性、界面与帧记录链路。其帧率不替代真实手机的数据，程序也无法确认玩家是否实际感受到触觉。实际目标仍为普通场景 60 FPS，重大演出优先 60 FPS、最低 30 FPS；达到这些数值需要对应设备报告支持。

原生自动检查通过设置页重新记录，在标准和省电画质分别等待 100 个真实渲染帧，验证预热后各至少 60 个有效样本、暂停不增加计数，并从“复制报告”读取实际写入文件。CI 同时核对平均帧率与总帧数/时长的一致性、P95/最慢帧和两个画质目标；空报告不能通过检查。这些短时样本不作为帧率达标证据。

胶片小图有明确的尺寸和编码长度上限，读取前核对 JPEG 尺寸。旧存档没有真实图像时不制造旧预演截图；既有文字、来源与纪念物仍可读取。缩略图是保存与回忆用途，不代表电影级美术质量已经人工验收。
