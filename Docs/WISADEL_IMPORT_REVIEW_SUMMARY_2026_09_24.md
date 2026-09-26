# 维什戴尔导入与清单核对审阅摘要

更新时间：2026-09-24  
用途：交给另一位 AI 审阅本次维什戴尔角色导入、特效偏移、音频候选和时序文档。本文记录现状与证据，不代表 Unity 导入或运行时验收已经通过。

## 1. 用户要求与审阅范围

- 只导入维什戴尔原版与 `game#9`；不导入 `sale#14`。
- 新增角色专用 FX 偏移界面。每个 FX 项前有独立复选框，勾选后显示该项设置；复选框只控制设置项显示，不开关运行时特效。
- 左右朝向偏移独立保存；支持数值输入和横向滑杆，默认范围 `-8…8`；变更后自动保存并应用。
- 读取解包器生成的 JSON，核对音效事件映射和 FX 释放延迟，并写入文档。
- 旧角色不属于本任务范围；审阅时不要把当前工作区中其他角色的改动自动算入本任务。

## 2. 已完成的工作

### 2.1 角色导入与动作 / FX 接入

- 新增维什戴尔本地导入入口和角色专属 Bootstrap，源目录为 `D:\Ark\_Unpacked\wisdel`。
- 角色清单按原版与 `game#9` 建立战斗 Spine 与 Move 动作源的独立资源描述；Spine 文件复制为 Unity Spine importer 使用的 `.atlas.txt`、`.skel.bytes` 扩展名，并复制 atlas 引用的贴图页。
- 导入器复制头像、S2/S3 图标、语音 wav，并生成选中 FX 序列帧的 prefab。FX 目录过滤掉 `sale#14`，只选含 `f<number>.png` 帧的目录。
- `game#9` 选用其独有的 S3 start / trail / hit FX；普攻、S2 和共用 FX 使用原版目录。运行时代码不直接读取解包目录。
- 添加角色专属 Factory、Builder、Scene Builder、动作表现 Driver、远程普攻 / 技能原型、FX Controller 和偏移 Profile。

### 2.2 之前的 FX 导入异常

用户曾遇到 `ExtractedFrameFxImporter.ImportSelected` 报“没有找到指定的特效帧目录”。相关代码现已补充完整路径规范化、真实 `f<number>.png` 帧检查、空目录过滤和更可诊断的错误信息；Wisadel Bootstrap 也会先排除 `sale#14` 并检查帧文件。

源代码修复本身不能证明 Unity 中已经成功重跑导入。需在 Unity 编译完成后重新执行菜单并检查 Console、生成的 prefab 和场景表现。

此前 Editor 日志还出现过 Wisadel 命名空间 / 类型引用编译错误；目前相关源文件已能看到所需的 namespace imports，但没有新的编译结果可证明这些错误全部消失。审阅者应以一次最新 Unity 编译和 Console 为准，旧日志不能代表当前结果。

### 2.3 偏移窗口

维什戴尔窗口覆盖 9 个 FX slot：普攻 Start A/B/C、Trail、Hit，S2 Start / Buff / Hit，S3 Start / Trail / Hit。每项的复选框记在 EditorPrefs，只展开或隐藏设置区域；左右偏移分别存入 `WisadelFxTuningProfile`。数值输入和滑杆范围为 `-8…8`，编辑后保存 Profile 并刷新场景中的 Wisadel FX Controller。

## 3. 辅助清单核对结论

本次外部导出位于 `D:\Ark\_Unpacked\wisdel`。主要证据文件见第 5 节。

### 3.1 音效候选

`audio_mapping.json` 将 ACT 技能槽 1 对应原版 Skill 2、技能槽 2 对应原版 Skill 3。以下候选 key 与事件相符，且对应 `.wav` 文件均存在于 `sound\_banks`：

| ACT 事件 | 导出事件 | bank key |
| --- | --- | --- |
| 普攻挥击 | `ON_ABILITY_START` | `p_atk_0/p_atk_dkmrcaygn_n` |
| 技能槽 1 / 源 Skill 2 施放 | `ON_SKILL_START` | `btl_snd_0/b_char_atkboost` |
| 技能槽 1 / 源 Skill 2 特殊节点 | `ON_SKILL_SPECIAL_POINT` | `btl_snd_0/b_char_boostclose` |
| 技能槽 1 / 源 Skill 2 命中 | `ON_PROJECTILE_HIT` | `p_imp_2/p_imp_dkmrcaygn_h` |
| 技能槽 2 / 源 Skill 3 施放 | `ON_SKILL_START` | `btl_snd_0/b_char_atkboost` |
| 技能槽 2 / 源 Skill 3 特殊节点 | `ON_SKILL_SPECIAL_POINT` | `btl_snd_0/b_char_boostclose` |
| 技能槽 2 / 源 Skill 3 命中 | `ON_PROJECTILE_HIT` | `p_imp_1/p_imp_dkmrcaygn_s` |

这些是导出器的自动候选，不是人工试听后的最终音色确认。`configured` 中 attack / skills / slots 均未配置，`manualMap` 为空，`source.audio` 为空；清单引用的 `data/audio_data.json` 在本次素材目录对应位置不存在。普攻 `ON_ABILITY_ON` 有 4 个候选，存在歧义。当前 Wisadel `PlayableOperatorAudioProfile` 仍保持可选静音。通用 Profile 没有逐技能命中音字段；若最终要接命中声，需用角色专属 Impact 音频监听。`ON_SKILL_SPECIAL_POINT` 也不能错误地当成与施放音同时播放的 layer。

### 3.2 特效延迟

- `unity_import_manifest.json` 的 `skillTiming.effectSchedule` 为空；游戏技能表没有逐帧 FX 释放时刻。
- 本次 Bootstrap 当前选用的 FX tag，其 prefab 粒子 `particleMaxStartDelaySec` 都为 `0s`（15 FPS）。这是 prefab 实例化后的内部粒子启动延迟，不是技能按下到 FX 出现的延迟。
- `action_gif_manifest.json` 给出的原版 `Skill_2_Begin` 总长 `0.33s`、`Skill_3_Begin` 总长 `0.83s`，不能直接当成命中帧时刻。
- 代码目前的 ACT 原型值：S2 按键到 Impact 为 `0.62s`；S3 首次 Impact 为 `0.72s`，总计 3 段、相隔 `0.34s`。这些值未由本次 JSON 证实，仍需 Unity 中按 Spine 动作和 FX 首帧对齐。

## 4. 主要代码与文档

### Wisadel 任务代码

- 菜单与资源注册：[ArknightsActMenuRegistry.cs](../Assets/_Game/Editor/ArknightsActMenuRegistry.cs)、[PrtsPrototypeAssetCatalog.cs](../Assets/_Game/Editor/PRTS/PrtsPrototypeAssetCatalog.cs)
- 本地素材导入和角色构建：[WisadelLocalAssetBootstrap.cs](../Assets/_Game/Editor/WisadelLocalAssetBootstrap.cs)、[WisadelPrototypeOperatorBuilder.cs](../Assets/_Game/Editor/WisadelPrototypeOperatorBuilder.cs)、[WisadelPrototypePlayerFactory.cs](../Assets/_Game/Editor/WisadelPrototypePlayerFactory.cs)、[WisadelPrototypeSceneBuilder.cs](../Assets/_Game/Editor/WisadelPrototypeSceneBuilder.cs)
- 共用 FX 导入器修复：[ExtractedFrameFxImporter.cs](../Assets/_Game/Editor/Effects/ExtractedFrameFxImporter.cs)
- 偏移界面与运行时：[WisadelFxTuningWindow.cs](../Assets/_Game/Editor/WisadelFxTuningWindow.cs)、[FxOffsetEditorControls.cs](../Assets/_Game/Editor/FxOffsetEditorControls.cs)、[WisadelFxTuningProfile.cs](../Assets/_Game/Scripts/Gameplay/Characters/Wisadel/WisadelFxTuningProfile.cs)、[WisadelExtractedFxController.cs](../Assets/_Game/Scripts/Gameplay/Characters/Wisadel/WisadelExtractedFxController.cs)
- 动作 / 技能原型：[WisadelPresentationDriver25D.cs](../Assets/_Game/Scripts/Gameplay/Characters/Wisadel/WisadelPresentationDriver25D.cs)、[WisadelSkill.cs](../Assets/_Game/Scripts/Gameplay/Characters/Wisadel/WisadelSkill.cs)、[WisadelRangedBasicAttack.cs](../Assets/_Game/Scripts/Gameplay/Characters/Wisadel/WisadelRangedBasicAttack.cs)

### 本次及相关文档

- [CHARACTER_IMPORT_WORKFLOW.md](CHARACTER_IMPORT_WORKFLOW.md)：角色素材导入通用流程、偏移窗口规则，以及本次音效 / 时序核对记录。
- [README.md](README.md)：Docs 文档索引；确认当前入口及相关交接文档。
- [PROJECT_STATUS_AND_ROADMAP_2026_09_24.md](PROJECT_STATUS_AND_ROADMAP_2026_09_24.md)：当前项目进度与计划。
- [MASTER_HANDOFF_2026_09_23.md](MASTER_HANDOFF_2026_09_23.md)：项目总交接快照。
- [CHARACTER_SYSTEM_REFACTOR_HANDOFF_2026_09_22.md](CHARACTER_SYSTEM_REFACTOR_HANDOFF_2026_09_22.md)：角色切换 / 生命周期架构。
- [ASSET_INTEGRATION.md](ASSET_INTEGRATION.md)：资源导入层与运行时表现层的边界。
- [EXTRACTED_FX_PIPELINE.md](EXTRACTED_FX_PIPELINE.md)：序列帧 FX 导入与播放流程。

### 解包目录的清单和素材

- `D:\Ark\_Unpacked\wisdel\unity_import_manifest.json`
- `D:\Ark\_Unpacked\wisdel\audio_mapping.json`
- `D:\Ark\_Unpacked\wisdel\UNITY_IMPORT_GUIDE.md`
- `D:\Ark\_Unpacked\wisdel\spine\action_gif_manifest.json`
- `D:\Ark\_Unpacked\wisdel\effects\frames\<fx-tag>\timing.json`
- 音频候选：`D:\Ark\_Unpacked\wisdel\sound\_banks\<bank>\<clip>.wav`

## 5. 尚未验收 / 审阅建议

1. Unity 重新编译后执行 `ArknightsACT > 角色资源 > 导入维什戴尔（原版 + game#9）`，检查 Console 和生成资源，确认原版与 game#9 的 combat / Move Spine 都构建成功，且没有 `sale#14`。
2. 在游戏内检查普攻三段、S2、S3、受击 / 死亡动作，观察偏移复选框是否只控制面板设置显隐，左右值是否独立并能自动保存、应用。
3. 逐个试听第 3.1 节候选，再决定是否把主音 / 特殊节点 / 命中事件接入 Profile 或 Wisadel 专属监听器。
4. 根据源事件时间或 Unity 逐帧观察确认 S2/S3 的实际释放时刻；确认后再改原型延迟并更新工作流文档。
5. 审阅 `ExtractedFrameFxImporter.cs` 的共享代码变化，确认修复导入路径和帧过滤时没有改变其他角色的 FX 导入语义。

本轮整理文档时没有运行 Unity 编译、Play Mode 或测试。当前工作区有大量同时存在的旧角色源码改动及文档删除；其中包括 FrostNova、Schwarz、Chen、Gameplay 通用脚本等。不能仅凭 `git status` 将它们归因于本次 Wisadel 任务，也不要为审阅本摘要而清理或还原这些文件。应按文件 diff 单独确认来源。
