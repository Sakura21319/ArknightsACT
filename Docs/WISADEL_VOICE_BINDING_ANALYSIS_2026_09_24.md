# 维什戴尔角色语音绑定分析

日期：2026-09-24

## 结论

当前项目不是“语音没有导出”，而是“导出的语音没有全部绑定到运行时事件”。

- `D:\Ark\_Unpacked\wisdel\voice\voice` 中已有 70 个 WAV：35 个语音 ID，各有一个同名 `_1` 变体。
- `D:\Effect\_tools\workflow\data\charword_table.json` 中，维什戴尔有 76 条语音配置：38 个语音 ID × 原版和 `sale#14` 两套皮肤。
- 当前能找到 WAV 的语音 ID 是 35 个；CN_038（新年）、CN_043（生日）、CN_044（周年庆典）只有表格元数据，没有出现在当前 `wisdel` 语音包中。
- `_1` 不是新的触发类型，而是解包时的同名文件冲突编号。由于原版和 `sale#14` 使用同样的 `CN_###` 名称，`_1` 很可能对应第二套皮肤的同名语音。
- 当前项目把所有 WAV 都复制进 Unity，但 `WisadelLocalAssetBootstrap.LoadBattleVoices()` 只加载 CN_025～CN_028，并将同一个池同时用于两个技能槽。
- `RoguelitePrototypeAudioController.HandleSkillCast()` 只在技能成功释放时播放语音，其他 `placeType` 没有运行时事件绑定。

## 数据来源的分工

### `charword_table.json`：角色语音语义

它定义了 `voiceId`、显示名称、文本、解锁条件、`placeType` 和原始 `voiceAsset`。例如：

```text
CN_019 -> BATTLE_START      行动出发
CN_020 -> BATTLE_FACE_ENEMY 行动开始
CN_021/022 -> BATTLE_SELECT 选中干员
CN_023/024 -> BATTLE_PLACE  部署
CN_025..028 -> BATTLE_SKILL_1..4 作战中语音
CN_029..032 -> 战斗结果
```

### `audio_mapping.json`：战斗音效语义

它描述的是 `p_atk_*`、`p_imp_*`、`b_char_*` 等 FMOD/战斗音效的触发关系，不包含角色语音的 `CN_###` 绑定。因此只看 `audio_mapping.json` 会漏掉大量角色语音。

## 维什戴尔语音的完整触发分组

| 语音 ID | `placeType` | 触发时机 | 当前项目状态 |
|---|---|---|---|
| CN_001 | HOME_PLACE | 任命助理 | 未绑定 |
| CN_002..004 | HOME_SHOW | 主页点击/交谈 | 未绑定 |
| CN_005..009 | HOME_SHOW | 晋升、信赖解锁后的主页交谈 | 未绑定，需保留解锁条件 |
| CN_010 | HOME_WAIT | 主页闲置一段时间 | 未绑定 |
| CN_011 | GACHA | 干员报到 | 未绑定 |
| CN_012 | LEVEL_UP | 观看作战记录/升级 | 未绑定 |
| CN_013 | EVOLVE_ONE | 精英化 1 | 未绑定 |
| CN_014 | EVOLVE_TWO | 精英化 2 | 未绑定 |
| CN_017 | SQUAD | 编入队伍 | 未绑定 |
| CN_018 | SQUAD_FIRST | 被任命为队长 | 未绑定 |
| CN_019 | BATTLE_START | 行动开始前/进入地图 | 未绑定 |
| CN_020 | BATTLE_FACE_ENEMY | 第一次面对敌人/进入交战 | 未绑定 |
| CN_021..022 | BATTLE_SELECT | 战斗中选中干员 | 未绑定 |
| CN_023..024 | BATTLE_PLACE | 干员部署 | 当前原型没有部署事件 |
| CN_025..028 | BATTLE_SKILL_1..4 | 作战中触发技能语音 | **已绑定**，两个技能共用池 |
| CN_029 | FOUR_STAR | 高难度完成 | 未绑定 |
| CN_030 | THREE_STAR | 三星完成 | 未绑定 |
| CN_031 | TWO_STAR | 非三星完成 | 未绑定 |
| CN_032 | LOSE | 行动失败 | 未绑定 |
| CN_033 | BUILDING_PLACE | 进驻设施 | 未绑定 |
| CN_034 | BUILDING_TOUCHING | 设施中点击干员 | 未绑定 |
| CN_036 | BUILDING_FAVOR_BUBBLE | 信赖触摸/气泡 | 未绑定 |
| CN_037 | LOADING_PANEL | 标题/加载界面 | 未绑定 |
| CN_038 | NEW_YEAR | 新年活动 | 当前包没有 WAV |
| CN_042 | GREETING | 问候 | 未绑定 |
| CN_043 | BIRTHDAY | 生日 | 当前包没有 WAV |
| CN_044 | ANNIVERSARY | 周年庆典 | 当前包没有 WAV |

CN_015、CN_016、CN_035、CN_039～CN_041 等 ID 不属于当前这套维什戴尔配置，不应为了补齐编号而虚构绑定。

## 当前代码实际做了什么

### 1. 资源导入

`LocalOperatorPresentationImporter.ImportVoices()` 会把 `voice/voice` 下的每个 WAV 原样复制到：

```text
Assets/_Game/Art/Audio/Wisadel/Voices/
```

所以资源层面已经可以引用 CN_001～CN_044 中当前存在的文件，包括 `_1` 文件。

### 2. 运行时绑定

`WisadelLocalAssetBootstrap.LoadBattleVoices()` 只读取：

```text
CN_025.wav
CN_026.wav
CN_027.wav
CN_028.wav
```

然后 `ConfigurePlayer()` 将同一个数组同时传给 `slot1Voices` 和 `slot2Voices`。这解释了为什么现在技能能说话，但其他界面和战斗语音没有反应。

`RoguelitePrototypeAudioController` 当前只监听：

- `PlayerSkillController.SkillCastSucceeded`：播放 CN_025～CN_028 随机池
- 普通攻击、受伤、死亡：播放战斗音效，不是角色台词
- `PlayerRuntimeContext.ActivePlayerChanged`：只重新绑定玩家，没有播放 `BATTLE_SELECT`
- `RogueliteGameFlowController.StateChanged`：目前只切 BGM，没有播放 `BATTLE_START` 或结算台词

## 推荐的实现顺序

### 第一阶段：直接接入当前原型已有事件

1. `BeginOperation()` 成功进入 `Running` 后播放 CN_019。
2. 第一名敌人进入可交战状态时播放 CN_020，并用一次性标志避免每帧重复。
3. `ActivePlayerChanged` 或明确的选中事件播放 CN_021/CN_022 随机池。
4. `ConfirmExtraction()` 根据结算结果播放 CN_030；如果以后增加星级评价，再分别使用 CN_029/CN_031。
5. `OnPlayerDied()` 使用 CN_032。
6. 如果以后加入真正的部署事件，再在部署成功点播放 CN_023/CN_024。

### 第二阶段：补齐非战斗语音

建议把 `PlayableOperatorAudioProfile` 从“技能两个数组”扩展成按语义命名的语音池，例如 `HomeShowVoices`、`BattleStartVoices`、`BattleSelectVoices`、`SettlementVoices`、`FacilityVoices`。这样不会把 `CN_###` 编号散落在通用系统中。

主页点击、闲置计时、编队确认、升级/精英化确认、设施点击、问候和加载界面都应从角色配置读取，而不是在场景控制器里写死维什戴尔编号。

### 第三阶段：皮肤与解锁条件

- 原版和 `sale#14` 共享同一个 `CN_###` 逻辑 ID，但必须在导入阶段确定变体，不能只靠 Unity 文件名猜测。
- 当前 `game#9` 没有在 `charword_table` 找到单独的语音表项，因此现有实现会继续复用默认角色语音；这与视觉/特效皮肤切换是两套独立配置。
- `CN_005/006` 的解锁类型是 `AWAKE`，`CN_007/008/009` 是 `FAVOR`，不能在主页无条件随机播放。
- CN_038、CN_043、CN_044 虽有配置，但当前导出包没有对应 WAV，接入前需要重新解包对应语言/活动语音包。

## 结论性的绑定建议

当前 CN_025～CN_028 共用技能语音池是合理的：`BATTLE_SKILL_1..4` 是游戏语义分类，不等于“每个技能只能使用一个固定编号”。后续可以在有明确技能分支或技能阶段事件时再细分；现在优先补齐 CN_019、CN_020、CN_021/022、CN_029～032，收益最大，也最符合现有原型事件结构。

## 参考文件

- `D:\Effect\_tools\workflow\data\charword_table.json`
- `D:\Ark\_Unpacked\wisdel\audio_mapping.json`
- `D:\Ark\_Unpacked\wisdel\voice\voice\`
- `Assets/_Game/Editor/WisadelLocalAssetBootstrap.cs`
- `Assets/_Game/Editor/LocalOperatorPresentationImporter.cs`
- `Assets/_Game/Scripts/Gameplay/Characters/PlayableOperatorAudioProfile.cs`
- `Assets/_Game/Scripts/Gameplay/Audio/RoguelitePrototypeAudioController.cs`
- `Assets/_Game/Scripts/Gameplay/Roguelite/Routing/RogueliteGameFlowController.cs`
