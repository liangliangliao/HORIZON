"""Fixed v0.4.2 engineering acceptance checklist, including external outcomes.

Each chapter has equal weight. Its independently named checks have equal
weight within that chapter. Caps record known incomplete scope; missing or
failed execution evidence awards zero. This is not a player satisfaction score.
"""
from pathlib import Path

ALIASES = {
 'life': ['WideChoicesGroupedEchoesAndPreparationCanCompleteAnActualLife'],
 'touch': ['PhoneCardIsRaycastableAndDragThroughEventSystemPlaysExactlyOnce'],
 'ui': ['ExpeditionChoicesKnowledgeThoughtAndNarrativeArePlayableOnPhone'],
 'boss': ['EveryBossCanBePreparedRecoveredAndActuallyReached'],
 'story': ['StoryChallengeConnects'],
 'modes': ['EveryModeCompletesBalancedAndRecoveringLivesWithResumeAndReplay'],
 'long': ['LongRunIsSixtyDaysAndRestoresAndReplaysItsDistinctLength'],
 'echo': ['GrowthEchoReturnsOnScheduledDayAndMarksItsSource'],
 'six': ['SixResourcesPayImmediateCostsAndDelayedAbilityAndOpportunity'],
 'prediction': ['FourthDayPredictionIsSealedAndMeasuredAfterTheSeventhDayEchoes'],
 'prediction6': ['SixAxesExplainEveryActualChangeIncludingCaps'],
 'forecast': ['UncertainForecastDoesNotRerollOrResolveTheRealLife'],
 'graph': ['ACollaborativeOpportunityMergesTwoActualHistoriesAndSurvivesSave'],
 'replay': ['ReplayingCommandsReproducesResourcesAndDecisionCausality'],
 'lock': ['LockedDecisionRequiresExecutionAndSurvivesReload'],
 'unlock': ['UnlockIsRecordedAsExecutionPhaseReopenedDecision'],
 'cost': ['PreparationConsumesFocusAndActuallySavesEnergyWithMatchingEquipment'],
 'route': ['RouteTradeoffsAndEquipmentRemovalAreRealTransactionsAndSurviveReload'],
 'environment': ['ThoughtResponseIsPaidOnlyOnceAndActuallySupportsTheFollowingAction'],
 'momentum': ['PreparationUsesFocusAndCanExpireMomentumWithoutDeletingTheOpportunity'],
 'thought': ['EveryThoughtHasAReasonAndTradeoffRatherThanAThreat'],
 'council': ['CouncilWeightsHaveSixVoicesAndExplainEnvironmentMoneyAndFailure'],
 'knowledge': ['KnowledgeRequiresARealConditionAndAMatchingSimulationBeforeReality'],
 'knowledge5': ['KnowledgeCannotSkipFromKnowingToRealExperience'],
 'knowledge8': ['EightKnowledgeSkillsKeepTheirHighestEvidenceAcrossLives'],
 'worldview': ['WorldviewsFormALimitedDeckAndBecomeCausalPerspectivesOnActualChoices'],
 'imagine': ['ImaginationCannotReachVictoryWithoutFailuresAndRecovery'],
 'imagine2': ['TwoSetbacksHaveGoalSpecificProcessCostsRecoveryAndCalibration'],
 'compare': ['ActualChoiceDivergenceUpdatesRecentModelOnceAndDoesNotInventReality'],
 'recover': ['RealSetbackAndNextActionProduceARecoveryComparison'],
 'mirror': ['MirrorRunRecreatesARepeatedNodeAndNeedsActualRecoveryThenGrowth'],
 'pattern': ['RepeatedPatternBreakUsesActualPriorFailuresAndCapsMythicRewards'],
 'reward': ['MasterSnapshotsAreIndependentAndRewardAcknowledgementIsIdempotent'],
 'overdrive': ['OverdriveBuildsFromActualExecutionHasADailyCapAndExpires'],
 'reservoir': ['RecoveryAndRelationshipsHaveIndependentPaidOnceReservoirs'],
 'cascade': ['CascadeAndSingularityComeFromRealMergedActionsInLongLives'],
 'memory': ['ImaginationIsIsolatedUntilAnExplicitCompletedPathIsAttached'],
 'reality': ['RealityQuestIsOncePerDateAndConvergenceNeedsAllThreeKindsOfEvidence'],
 'horizon8': ['HiddenCauseRequiresInsightAndRevealsARealParentWithoutChangingResources'],
 'horizon': ['OneObservationContractControlsDistanceTypesAndCapabilities'],
 'station': ['ActualStationNetworkAndMemoryPagesResumeAndNeverAwardResources'],
 'gallery': ['CompletedFutureStationHasActualMemoriesOrbitAndSelectableTimelines'],
 'selves': ['FutureSelfVersionsUseActualCompletedLivesAndKeepTheirEvidence'],
 'masterui': ['MasterFeaturesCanBePlayed'],
 'first': ['FirstLifeTeachesTimeAndPreparesAChoiceWithoutVisitingTheFeatureHub'],
 'families': ['NewQuickLifeGraduallyIntroducesFamiliesWithoutRemovingGrowthOrRecovery'],
 'role': ['EveryNarrativePurposeWorksOfflineAndCannotMutateTheRules'],
 'azure': ['PastedResponses'],
 'aiui': ['OnlineAISettingsAndFutureSelfUseActualUIWithoutPersistingCredentials'],
 'audio': ['SoundAndHapticsHaveDistinctEchoRecoveryAndMythicPhrases'],
 'social': ['two real HTTP clients share a scenario, publish distinct lives and receive a future message'],
 'socialclient': ['SocialClientUsesHttpsMembershipAndBoundedPublicPayloads'],
 'message': ['ReceivedExperienceBecomesADelayedEchoWithoutInventedResourceRewards'],
 'socialsave': ['room data survives a service restart without storing raw member tokens'],
 'mp4': ['TenSecondMp4IsExportedByTheShareButtonWithAnAudioTrack'],
 'privacy': ['SharedTimelinesContainOnlyPublicChoicesAndARealTenSecondSoundtrack'],
 'save': ['BackupRecoversTruncatedPrimaryAndPreservesUnreadableEvidence'],
 'architecture': ['EventSubscriberFailureCannotUndoRuleTransaction'],
 'cosmetic': ['SceneryPurchaseRequiresCurrencyAndOnlyChargesOnce'],
 'no_power': ['ASurprisingPredictionDoesNotChangeResourcesOrGrantPower'],
}
CHAPTERS = []
def chapter(number, title, source, *checks):
    criteria=[]
    for i, check in enumerate(checks, 1):
        # text | required alias(es) | optional known-completion cap | limitation
        fields=check.split('|'); desc, keys=fields[:2]
        cap=float(fields[2]) if len(fields)>2 and fields[2] else 1
        limitation=fields[3] if len(fields)>3 else ''
        tests=[test for key in keys.split(',') if key for test in ALIASES[key]]
        criteria.append({'id':f'{number:02}.{i:02}', 'requirement':desc, 'tests':tests,
                         'completion_cap':cap, 'known_limit':limitation})
    CHAPTERS.append({'chapter':number,'requirement':title,'source':source.split(','),'criteria':criteria})
G='Assets/Scripts/Game/'
A='Assets/Scripts/'
U=A+'UI/'
chapter(1,'时间策略、Roguelite、人生模拟、因果、自我探索',G+'GameSession.cs,'+G+'GameSession.Expedition.cs',
 '每日可权衡的成长、刺激、恢复选择|life','种子、路线和不同人生具有可重演变体|modes,long',
 '选择、因果与近期模式连接到未来自己|story,pattern','前两层单独好玩的首次玩家证据||0|尚未完成独立玩家试玩研究')
chapter(2,'TIME / FORESIGHT / IMAGINATION / PATTERN',G+'GameSession.Master.cs,'+G+'ImaginationEngine.cs',
 '行为延迟、积累并改变后来的选择空间|echo,graph','信息成长、概率与隐藏来路|forecast,horizon8',
 '提前练习努力、失败和恢复|imagine,imagine2','反复结构可被不同真实动作突破|pattern,mirror')
chapter(3,'统一核心循环',G+'GameSession.Master.cs,'+A+'HorizonApp.MasterJourney.cs',
 '观察、选择、结算、回声与预测对照|life,prediction','困难、调整、恢复与再次行动|story,recover',
 '因果图、模式、未来自己与时间地图|story,station,pattern','想象、模拟和现实桥梁形成可保存闭环|masterui,reality,compare')
chapter(4,'12/30/Long/Parallel/Imagine/Experiment/Mirror/Chaos',G+'CampaignRules.cs,'+G+'GameSession.Expedition.cs',
 '十二天可完成并抵达 Boss|life,boss','三十天与六十天可保存、完成和回放|modes,long',
 '五种扩展模式有不同规则与选择|modes,mirror','12 Days 实际用时15—25分钟||0|自动执行不能证明玩家用时')
chapter(5,'六资源、趋势、波动、相互影响、选择空间',G+'GameSession.cs,'+G+'MasterSpecification.cs',
 '六项资源支付与封顶真实结算|six,prediction6','趋势和波动来自已发生资源样本|ui',
 '低状态会改变后来的机会与支援|graph','恢复保持可玩并重新打开行动空间|life,modes')
chapter(6,'十四行为家族与完整卡牌数据',G+'MasterSpecification.cs,'+G+'PlayGuide.cs',
 '十四家族进入实际牌池与模式|families,modes','Delay/EchoType/标签/风险/承诺/触发/AI上下文可用于规则|replay,environment',
 '风险、承诺与世界观作用于实际因果|worldview,boss','现在结果、未来结果和可用成本在卡面可读|life,touch')
chapter(7,'TIME ECHO',G+'GameSession.cs',
 '即时结果与延迟结果分别结算|six,echo','固定日期返回并标记真实来源|echo',
 '二阶回声继续传播到机会与结果|graph','存档、重开、回放不会重复领取回声|replay,life')
chapter(8,'CauseGraph 作为统一因果数据',G+'GameSession.cs,'+G+'GameSession.cs',
 '行动、事件、决定、念头、失败恢复都有原因节点|boss,environment,lock',
 '多来源连接形成真实路径并保存|graph,replay','图驱动回声、合流、预测解释和时间地图|cascade,prediction6,station',
 '图驱动想象对照与故事分享|compare,mp4')
chapter(9,'LOCK PREDICTION / SYNCHRONIZED / SURPRISE',G+'CampaignRules.cs,'+A+'HorizonApp.Campaign.cs',
 '玩家亲自锁定资源方向预测|prediction','未来到达后叠加真实六维结果|prediction6',
 '接近与惊讶保持公平，资源不按对错奖励|no_power','校准理解保留在后续人生|compare')
chapter(10,'八级信息视野',G+'HorizonProgress.cs,'+A+'HorizonApp.ContentStudio.cs',
 '现在、一天、三天的可见范围|horizon,first','概率与二阶影响受到信息能力约束|forecast,prediction6',
 '平行未来和不同未来自己可查看|station,modes,gallery','VIII 或 Overdrive 揭示真实隐藏来路|horizon8,ui')
chapter(11,'Decision Lock 与 Execution Mode',G+'GameSession.Master.cs',
 '锁定已选行动并切换为具体执行步骤|lock,life','未完成准备时不能偷换成其他决定|lock',
 '允许主动解锁并记录重开决策|unlock','执行、因果与锁定断点保存和回放|lock,replay')
chapter(12,'Action Engine 八变量与改变条件',G+'DecisionExecution.cs,'+G+'GameSession.Expedition.cs',
 '动机、能力、触发、摩擦、情绪、替代奖励、疲劳、社会压力|council,ui',
 '环境、步骤和承诺改变实际可行动状态|environment,route','准备支付成本并支持匹配行动|cost',
 '过度准备会耗尽行动势头|momentum')
chapter(13,'Trigger 装备环境',G+'DecisionExecution.cs,'+G+'GameSession.Expedition.cs',
 '闹钟、预约、朋友、承诺、押金、路线、票、地点、期限|route,ui',
 '不同提示有匹配范围与成本，最多三个|cost,route','可拆卸、更换并保存真实装备因果|route,replay')
chapter(14,'六种 Thought Monsters',G+'DecisionExecution.cs,'+A+'HorizonApp.Expedition.cs',
 '更好机会、明天、完美、今天不适合、舒服、目光|thought',
 '每种念头具有合理部分和可见代价|thought,ui','理解、权衡、决定的响应实际影响行动条件|environment,ui')
chapter(15,'Inner Council',G+'DecisionExecution.cs,'+A+'HorizonApp.Master.cs',
 '六动机由真实状态生成归一化权重|council','点击动机解释睡眠、天气、失败、奖励与摩擦来源|council,ui',
 '议会提供理由，最终选择属于玩家|ui,environment')
chapter(16,'Knowledge Forge 五阶段',G+'PlayerModel.cs,'+A+'HorizonApp.Expedition.cs',
 '八项知识都有具体 IF/THEN 条件与动作|knowledge8,ui','RECOGNIZE 需要真实情境，SIMULATE 需要匹配行动|knowledge',
 'EXECUTE 要有现实报告，EXPERIENCE 要再次回顾|knowledge5,masterui','最高经验与证据跨局保存|knowledge8')
chapter(17,'世界观构筑',G+'PlayerModel.cs,'+G+'GameSession.Expedition.cs',
 '八种思想作为视角而非标准答案|worldview','最多三种组成可更换个人卡组|worldview',
 '视角进入行动、念头与实际因果|worldview,ui','知识转行动与玩家决定仍受规则约束|role,knowledge5')
chapter(18,'Imagination Engine',G+'ImaginationEngine.cs,'+A+'HorizonApp.Master.cs',
 '胜利锚点、时间倒退、准备、努力与抵达|imagine,masterui','目标类别有专属困难，过程消耗虚拟状态|imagine2',
 '失败、恢复、再行动与调整方法必须经历|imagine,imagine2','校准与可选AI内容改变下一次困难情境|imagine2,ui')
chapter(19,'强制失败与 Recover Phase',G+'ImaginationEngine.cs',
 '重要预演至少一次真实失败阶段|imagine','困难目标支持2、3、4次失败|imagine,imagine2',
 '休息、求助、降低目标、改方法后才能再战|imagine2,masterui','非法存档不能跳过失败与恢复|imagine')
chapter(20,'Future Memory 与 Déjà Vu',G+'ImaginationEngine.cs,'+G+'GameSession.Master.cs',
 '记忆来自预演中的恢复与再次开始|imagine2','相似目标和行为匹配后产生模拟证据|memory',
 '记忆唤醒事件和未来自己切入|masterui','记忆价值随韧性增长并可跨局保存|imagine2,knowledge8')
chapter(21,'想象/实际校准',G+'ImaginationCalibration.cs',
 '预演与实际行动对照并找第一次分歧|compare','实际受挫后再对照恢复策略，不凭空制造失败|recover',
 '分歧进入模型并用于下次预演|compare,imagine2','现实完成时间接入比较；任意目标多日现实轨迹|reality,masterui|0.5|现实部分目前是单步本人报告，尚非任意目标完整多日轨迹')
chapter(22,'Comeback Multiplier',G+'GameSession.Master.cs,'+G+'ImaginationEngine.cs',
 '失败再开始形成×2/×3/×4韧性链|imagine2,story','实际恢复再行动才形成关键再战证据|mirror,boss',
 '倍率提升Insight、记忆、演出与Pattern机会|imagine2,pattern|0.5|Pattern突破由多局证据判定，尚未加入独立倍率概率规则')
chapter(23,'Pattern Detection 与 Mirror Run',G+'PlayerModel.cs,'+G+'GameSession.Expedition.cs',
 '重新比较、退出、短期奖励及念头结构持续留证|pattern,environment','个人模式来自多局，单局反复不冒充多局|pattern',
 'Mirror主动重现近期节点并要求真实不同动作|mirror,ui')
chapter(24,'PATTERN BROKEN Mythic',G+'GameSession.Master.cs,'+U+'MasterSpectacleGraphic.cs',
 '真实多局重复后不同动作触发突破|pattern,mirror','过去多条线在同一模式位置中断|ui,pattern',
 '当前线继续、碎裂与发光演出|ui','静音、心跳与爆发，并受Mythic次数上限约束|audio,pattern')
chapter(25,'六级 Reward Engine',G+'MasterSpecification.cs,'+A+'HorizonApp.MasterFeedback.cs',
 'Micro/Local/Combo/Major/Epic/Mythic层级|reward,life','结果、回声、合流、胜利与突破有分阶段反馈|life,cascade,story',
 '高阶每局上限、持久确认与去重|pattern,reward')
chapter(26,'HORIZON OVERDRIVE',G+'GameSession.Master.cs,'+U+'HorizonWorld3D.Master.cs',
 '预测、准备、执行、恢复累计能量且每日有上限|overdrive','临时提升信息范围并按日过期|overdrive',
 'UI、地平线、音乐与粒子随状态变化|ui,overdrive','真实合流降低表现阈值，条件成熟激活Orbit|cascade,overdrive')
chapter(27,'Future Orbit / ALL LINKED',G+'GameSession.Master.cs,'+U+'HorizonWorld3D.Identity.cs',
 '八个未来方向由实际行为与结果激活|replay,boss','重大因果链可连续点亮多个节点|cascade',
 'ALL LINKED 是八位真实完成且去重的事件|reward|0.5|八位条件存在，尚未记录自然玩家完成全部八位的实玩证据',
 '未来自己获得实体轨道与外观升级|masterui,ui,gallery')
chapter(28,'Causal Reservoir / BREAKTHROUGH',G+'GameSession.Expedition.cs',
 '学习、训练、恢复和关系分别积累真实来源|reservoir','能力、状态、关系等复合条件成熟后兑现|reservoir',
 '大奖连接真实投资来源且保存后不重复支付|reservoir,replay','玩家能查看储备进度与兑现条件|ui')
chapter(29,'CASCADE / CAUSAL SINGULARITY',G+'GameSession.Master.cs,'+U+'MasterSpectacleGraphic.cs',
 '至少两次实际行动合流才形成大因果链|cascade','连续爆亮且加速的节点演出|ui,cascade',
 '更长的实际因果链触发 Singularity|cascade','准备按钮与单独一个行为不能制造超级Combo|cascade')
chapter(30,'多版本 Future Self',G+'ExpeditionSystems.cs,'+U+'HorizonWorld3D.Identity.cs',
 '疲惫、富有、孤独、平静、再战、平行和老人版本|ui,station,gallery,selves|0.5|七种映射已实现，固定完整人生样本已自然验证五类；全部版本的自然出现仍待验证',
 '提问与回忆来自真实行为，不替玩家决定|station,gallery,role','身份、颜色和轨道产生不同空间形象|masterui,ui,gallery',
 '平行自己与另一条真实重演连接|modes,station,gallery')
chapter(31,'Future Station 空间停顿',U+'HorizonWorld3D.Causality.cs,'+A+'HorizonApp.Expedition.cs',
 '阶段结尾离开刺激主局进入空间与未来自己|life,station,gallery','实际因果记忆成为空间中的时间线|station,gallery',
 '靠近、选一段回忆并读取片段|station,gallery,ui','低音量、慢节奏与断点恢复|station,gallery')
chapter(32,'Reality Bridge',G+'RealityBridge.cs',
 '每天最多一个自愿小任务|reality','本人主动确认并创建持久Reality Node|reality,masterui',
 '现实行动不能由游戏币购买或重复刷同日领取|reality,cosmetic','现实动作确实发生的独立证据||0|目前依赖本人报告，软件不能证明本人真实完成')
chapter(33,'Reality Constellation',G+'RealityBridge.cs,'+A+'HorizonApp.RealityStory.cs',
 '真实报告节点跨局积累而不随重开消失|reality','节点可点开读取想象/模拟/现实证据|masterui',
 '游戏行动来源与现实节点连接|reality,compare','星座可继续增长，完成同日任务不会重复增加|reality')
chapter(34,'REALITY CONVERGENCE',G+'RealityBridge.cs,'+U+'MasterSpectacleGraphic.cs',
 '必须拥有同一目标想象、匹配模拟、本人现实报告|reality','任一类证据缺失不得冒充三线重合|reality',
 '三节点连成一起，产生Mythic演出|masterui,ui','一次重合只触发一次，保存不会重复奖励|reality')
chapter(35,'十种行为状态 Boss',G+'StoryChapter.cs,'+G+'ExpeditionSystems.cs',
 '截止、舒适、完美、明天、目光、可能、不确定、疲劳、再等、永远准备|boss',
 '每种有独立准备与抵达条件|boss','恢复、资源、提示、承诺和策略实际决定抵达|boss',
 '十种挑战均完成三个起点、保存后继续与真实因果追溯|boss,story')
chapter(36,'人生可能性地图与路线取舍',G+'GameSession.Expedition.cs,'+A+'HorizonApp.Expedition.cs',
 'Today 向成长/关系/赚钱/恢复/探索路线选择|route,ui','同时只构筑一条主要路线，切换有代价|route',
 '路线改变后续手牌并进入因果图|route,replay','路线空间、互斥窗口与可移动关卡|route,ui|0.5|目前为路线选择与时间图，尚非完整可自由移动的可能性关卡')
chapter(37,'Opportunity Window',G+'GameSession.Master.cs',
 '机会具有开放日和过期日|momentum','接住窗口会改变实际手牌/成本并留下来源|momentum,replay',
 '跨日等待使机会消失，不能无限以后再说|momentum','岗位/会议/考试/关系等多种持续窗口内容|boss|0.5|已有作品、合作、支持与Boss窗口，内容类型和多日窗口仍有限')
chapter(38,'Momentum',G+'GameSession.Expedition.cs',
 '成熟动机窗口降低即时行动成本|momentum','过度准备与重比使势头消退|momentum',
 '动机过期有独立事件，机会与势头分别保存|momentum,replay')
chapter(39,'HORIZON ME',G+'PlayerModel.cs,'+G+'GameSession.Expedition.cs',
 '偏好与最近选择从实际观察而来|ui,compare','退出/犹豫/重新比较/念头记录情境证据|pattern,environment,ui',
 '学习哪些Trigger/Reward支持行动|environment,ui|0.5|当前记录匹配行动和选择频率，尚未证明因果有效性',
 '按近期疲劳/犹豫建议休息或更小开始，并允许模式改变|ui,pattern')
chapter(40,'AI 六类内容个性化与公平边界',G+'OnlineAI.cs,'+A+'HorizonApp.ContentStudio.cs',
 'DeepSeek / Azure / Foundry / Responses 协议与错误回退|azure,aiui',
 'Quest/NPC/FutureSelf/Pattern/Imagine/知识转行动接入|role,ui','内容白名单不改变资源、核心因果、概率或胜负|role,no_power',
 '本人Azure/DeepSeek资源端到端真实连接||0|无用户API密钥，当前验证为实际客户端加模拟服务商')
chapter(41,'手机竖屏与时间空间手势',A+'HorizonApp.Playability.cs,'+U+'HorizonVisuals.cs',
 '上方未来、中间现在、下方行为牌|first,touch','有厚度倒角的立体手牌，真实EventSystem触摸拖入未来|touch',
 '刺激/成长/恢复/关系有不同反馈空间语言|life,ui','Android真机触控、文字、帧率和耗电||0|本次尚无真机执行证据')
chapter(42,'十类专属动效',U+'MasterSpectacleGraphic.cs,'+U+'HorizonWorld3D.Master.cs',
 'Echo/Cascade/Overdrive/Victory/Breakthrough各有表现|ui,cascade,overdrive',
 'Pattern Broken/Reality Convergence多线专属图形|ui,masterui','FailAgain/FutureSelf/Orbit/Singularity过程反馈|ui,cascade',
 '最终动效品质与设备表现验收||0|程序化演出已实现，尚无独立美术与真机质量验收')
chapter(43,'声音的因果语言',G+'FeedbackLanguage.cs,'+U+'HorizonWorld3D.Master.cs',
 '刺激高频、成长渐进、恢复柔和、Echo双音|audio','Pattern先静音、心跳，再低频爆发|audio',
 '音乐随洞察变换，支持静音与偏好|overdrive,ui','玩家仅听音色辨识过去行为返回||0|需要盲听测试，不能以PCM不同代替辨识效果')
chapter(44,'Haptics',G+'FeedbackLanguage.cs,'+U+'HorizonWorld3D.Master.cs',
 '短震、双震、递增、暂停/轻/重事件语言|audio','普通无关事件不滥用震动|audio',
 'Android按SDK适配波形并支持关闭|audio','真机触觉、无画面辨识与耗电||0|没有真实设备波形/辨识结果')
chapter(45,'第一小时循序学习',A+'HorizonApp.Playability.cs,'+A+'HorizonApp.Chapters.cs',
 '初始只做选择并理解行为会回来|first,life','预测、准备与失败再战逐步进入主流程|first,story',
 '复杂系统可选，旧人生可隔离练习|life,ui','首次玩家无帮助完成与真实第一小时测量||0|自动全局执行不证明初次玩家理解和用时')
chapter(46,'长期留存',G+'PlayerModel.cs,'+A+'HorizonApp.Expedition.cs',
 '同起点换选择、回忆与时间线重演|modes,station','个人节点重现、不同未来自己和现实星座|mirror,reality,ui,gallery',
 '30/60天与十种故事提供多局内容|long,boss','真实回访、留存和自发再挑战||0|尚未做真实玩家留存实验')
chapter(47,'在线Parallel Lives / Future Messages',G+'SocialProtocol.cs,services/parallel-lives/server.mjs',
 '两个匿名成员从同一种子和规则开始|social,socialclient','邀请、同步、分歧比较与服务端断点持久化|social,socialsave',
 '主动投递的经验成为对方世界的延迟回声|social,message','现成公网服务与两个手机联网实玩||0|服务可部署并已本机HTTP验证；目前未部署公网地址')
chapter(48,'十秒Animated Timeline视频分享',A+'HorizonApp.Share.cs,'+U+'TimelineVideoWriter.cs',
 '从实际图与三次关键选择生成故事|mp4,privacy','真实10秒60帧H.264 MP4，不把GIF算视频|mp4',
 'AAC音轨、540×960竖屏、系统分享入口|mp4','Android MediaCodec编码与第三方应用接收||0|Editor导出可自动验证；Android编码/分享仍待真机')
chapter(49,'商业化价值边界',G+'OnlineAI.cs,'+G+'RealityBridge.cs',
 '外观购买真实扣款、不能重复付费领取|cosmetic','购买不改变预测准确率、因果规则和错误|no_power,cosmetic',
 '不能购买中奖概率或Reality Quest|reality,cosmetic','音乐/主题/环境/章节的真实支付接入||0|已有游戏内星尘外观，未接入支付与完整商品目录')
chapter(50,'爽、策略、理解、自我、现实五层',G+'GameSession.Expedition.cs,'+A+'HorizonApp.Expedition.cs',
 '卡牌/连锁/Boss和路线资源策略可完成|life,boss,cascade','因果/预测/隐藏变量可理解追溯|prediction6,horizon8,station',
 '想象、模式和未来自己接入真实行为|imagine2,mirror,ui','现实层有本人确认的持久证据连接|reality',
 '前两层独立好玩、后面三层发生有效迁移||0|需要真实玩家体验与现实迁移观察')
chapter(51,'Unity模块与领域事件隔离',G+'GameSession.Master.cs,'+G+'AIAdapter.cs',
 '规则结算不依赖动画完成、帧率和观察者成功|architecture','时间/卡牌/图/预测/想象/模式/模型/奖励分别组织|replay,imagine,pattern',
 'VFX/音效/震动/UI消费领域事件，Analytics去重|reward,architecture','AI仅内容，Save保留版本、断点与旧局|role,save')
chapter(52,'Phase A—E 顺序与交付',G+'GameSession.cs,'+A+'HorizonApp.Expedition.cs',
 'A 选择/时间/Echo/图/Boss/锁定/预测|life,boss,lock,prediction','B Echo/Cascade/站/预演/念头/模式|cascade,ui,mirror',
 'C ME/熔炉/议会/发动机/Triggers|knowledge8,council,environment,ui','D Overdrive/Orbit/Reservoir/Mythic/现实桥梁|overdrive,reservoir,reality,ui',
 'E AI/在线双人/星座/经验/长期内容|role,social,long|0.5|AI真实资源和公网在线服务尚未验收，长期留存未测量')
chapter(53,'最终玩家检验',A+'HorizonApp.Playability.cs,'+G+'AIAdapter.cs',
 '真实玩家主动想到行为三天后会回来||0|尚未观察首次玩家表达',
 '真实玩家识别自己反复改主意的位置||0|尚未记录独立玩家访谈',
 '真实玩家主动练习失败之后怎样恢复||0|需要长期玩家行为证据',
 '现实相似节点采取新的行动||0|需要本人现实行为观察，不能由自动测试证明')
assert [c['chapter'] for c in CHAPTERS] == list(range(1,54))
