# Ch'en 自绘特效交接记录

更新时间：2026-09-17  
适用分支：`feat/phase-08-world-visuals`

## 当前决定

### 2026-09-18 用户新增决定：提取与应用拆成两段

后续特效工作统一采用：

1. 使用外部 `D:\Effect\EffectExtractor.exe` 提取 PNG 帧。
2. 在 Unity 执行 `ArknightsACT > Assets > Import Extracted Frame FX`，由项目工具生成 Sprite 动画和 Prefab，并按事件应用到 `CustomFxMountPoint`。

这条决定允许使用“提取后的渲染帧”作为项目表现资源，但仍不恢复原始 AssetBundle 的运行时加载，也不重新挂载旧的 `ChenOriginal*Fx*` 组件。具体流程见 `Docs/EXTRACTED_FX_PIPELINE.md`。

原客户端 AssetBundle 战斗特效不再直接挂载到运行时角色。已导入的 OHMS 资源、依赖分析和修复工具保留在本地，仅作为研究参考；后续攻击、拔刀和技能特效可以使用项目内自绘/自制资源，也可以使用提取器生成的 PNG 帧序列，但两者都通过项目自己的 Prefab 和事件控制器接入。

这不是删除资源：`Assets/_Game/Art/FX/OriginalClient/Chen/` 仍可用于比对素材和节点命名，但该目录被 `.gitignore` 排除，不应作为新的运行时依赖提交。

## 已完成的停用点

- `ChenPrototypePlayerFactory` 的 `Create` 与 `Create25D` 不再调用原客户端 FX 组件或 `ChenOriginalSkillFxCatalog`。
- 两种角色生成路径都会创建空的 `CustomFxMountPoint` 子节点，作为未来自绘控制器的稳定挂点。位置为角色根节点本地 `(0, 0.8, 0)`，名称不要改动。
- 本地 `PrototypeRun.unity` 中已移除 `ChenOriginalSkillFxController`、`ChenDashSpineFxGateController`、`ChenOriginalSpineFxVisibilityController` 三个组件及其序列化块。
- `ChenOriginalSkillFxController`、`ChenOriginalSkillFxCatalog`、OHMS 导入菜单和分析脚本暂不删除，避免丢失反查信息；不要在新场景中重新添加这些运行时组件。

## 为什么暂停原客户端方案

技能 3 的刀光节点确实存在于 `chen.ab` 的层级中（例如 `daoguang`、`daoguang_liang`、`baoci_01/02`、`baoshan`、`smoke`、`huoxing`），但可见结果依赖其他 bundle 的材质、纹理和渲染配置。单独导出 `chen.ab` 会留下外部 `FileID/PathID`，并且 OHMS Structured 导出不包含可直接复用的原始 Animator/AnimationClip 行为。此前出现的紫色、错误贴图和“有 Spawn 日志但画面没有刀光”，都是依赖链/渲染行为不完整的表现，不适合作为项目运行时基础。

依赖调查和导入过程仍记录在：

- `Docs/OHMS_EFFECT_IMPORTER.md`
- `Assets/_Game/Art/FX/OriginalClient/Chen/OHMS_IMPORT_REPORT.txt`（本地生成）
- `ChenSkill03FxAnalysis.txt`（本地分析输出）

## 后续自绘特效接入约定

新控制器挂在 `Player_Chen`，通过 `transform.Find("CustomFxMountPoint")` 获取挂点。自绘 Prefab 应使用项目当前渲染管线可用的材质/Shader，不再引用 `OriginalClient/Chen` 中的外部材质或纹理。

建议按“事件驱动、表现与伤害解耦”实现：

| 表现 | 监听位置 | 建议时机 |
| --- | --- | --- |
| 普攻刀光 | `PlayerAttackController.AttackStarted` / `AttackHit` | 起手播放短刀光；命中时播放受击火花 |
| 拔刀（技能 1，`ChenSkill1`） | `ChenSkill1.HitResolved` | 伤害结算后播放一次横向刀光 |
| 绝影（资源键为 `chen_skill_03_*`，代码类为 `ChenSkill2`） | `ChenSkill2.StrikeResolved` | 每次斩击结算后生成对应方向的刀光；槽位 2 的起手可监听 `PlayerSkillController.SkillCastSucceeded` |

如果事件接口需要扩展，优先在现有 Gameplay/Combat 事件上增加只读事件，不要让特效脚本直接修改伤害、连段或技能状态。所有生成的实例应由控制器统一回收（`ParticleSystem.Stop(true, StopEmittingAndClear)` 或对象池）。

## 验证停用是否生效

1. 在 Unity 中执行 `ArknightsACT > Build Prototype Scene`，或重新打开本地 `PrototypeRun.unity`。
2. 在 Hierarchy 选择 `Player_Chen`，确认存在 `CustomFxMountPoint`，且没有上述三个 `ChenOriginal*Fx*` 组件。
3. 进行普攻、拔刀和技能 3：伤害、连段和技能逻辑应继续工作，但不应再出现原客户端 FX 的 `[ArknightsACT/ChenSkillFX] Runtime bridge ready` 或 `SpawnOnActor` 日志。
4. 后续制作自绘特效时，只把新控制器和 Prefab 接到 `CustomFxMountPoint`；不要恢复旧的原客户端挂载调用。

## 恢复旧方案（仅用于研究）

如需再次研究原节点，使用 `ArknightsACT > Assets > OHMS Effect Importer` 或对应的 staged import/repair 命令。研究完成后不要把 `ChenOriginalSkillFxController` 加回 Prototype Scene；运行时方案仍以本文件的自绘约定为准。
