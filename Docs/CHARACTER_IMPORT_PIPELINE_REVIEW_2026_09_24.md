# 角色快速导入流程审阅与 V2 优化方案

更新时间：2026-09-24  
审阅对象：`WISADEL_IMPORT_REVIEW_SUMMARY_2026_09_24.md`、`CHARACTER_IMPORT_WORKFLOW.md` 以及 Chen / Schwarz / FrostNova / Wisadel 现有接入代码。

## 0. 2026-09-24 实施状态

本审阅随后用 `D:\Ark\_Unpacked\wisdel` 与 `D:\Ark\_Unpacked\skadi` 两套真实导出包继续验证，第一阶段已经直接落实到项目，而不再只是建议：

- 新增 `LocalOperatorAssetImportUtility`：统一本地解包根目录、Spine/Sprite/Voice 复制、Presentation 构建、FX 增量检测。
- 新增 `LocalOperatorPresentationImporter` + `LocalOperatorImportPlan`：普通角色改为“描述资源”，不再复制整套 Bootstrap 基础设施。
- 新增 `LocalOperatorPackageScanner` + `LocalOperatorQuickImportWindow`：第一次接角色时可以直接选择 ArkMod 导出目录，自动识别 char/build Spine、皮肤后缀、头像、图标、语音与 FX，不要求先写角色代码。
- Wisadel 已迁移到 ImportPlan；普通刷新按当前皮肤，只导入实际绑定 FX，不再扫描整个 `effects/frames`。
- 新增 Skadi 真实验证样例：default / marthe#5 / summer#3 三套 combat + move Spine、头像、S2/S3 图标、语音及 28 个按皮肤声明的 FX。
- `PrototypeOperatorDefinitionAssetUtility.Ensure()` 已改为同步式 Ensure，已有 Definition asset 会跟随代码中的皮肤/UI key 更新。
- `CurrentOperatorAssetRefreshService` 脚本重载后不再自动保存场景，只 MarkSceneDirty。
- 本地解包根目录默认 `D:\Ark\_Unpacked`，并可从 Unity 菜单修改，不要求每个角色硬编码完整路径。
- 新增一次性 Unity Editor migration：在下一次安全的脚本 reload/editor update 中自动同步 Wisadel + Skadi；执行一次后停用。显式导入菜单仍保留。

### 当前验证边界

Bridge 可以验证源码和两个外部导出包的实际目录结构，但当前环境不能直接跨 workspace 搬运 PNG/SKEL/WAV 二进制，也不能执行 Unity Editor。检查项目目录时 Skadi Assets 尚未出现，因此**本轮代码重构已落盘，但 Skadi 的实际 Unity AssetDatabase 导入要等 Unity Editor 执行新脚本后完成**。

FolderBridge 的 build capability 已返回 source smoke validation 成功，但它明确是 `validation-only`，不能当作 Unity C# 编译或 Play Mode 验收。

---

## 1. 结论

当前流程的**架构方向基本正确**：

- Runtime 不读取 `D:\Ark\_Unpacked`，外部素材只通过 Editor 导入。
- `IPrototypeOperatorBuilder` + `PrototypeOperatorRegistry` 自动发现角色，角色不需要加入一个运行时 switch。
- `PlayableOperatorPrototypeComposer` 已经抽出了部分通用战斗组件。
- `CurrentOperatorAssetRefreshService` 已实现脚本编译后只刷新当前角色/皮肤，而不是重刷所有角色。
- Gameplay、Presentation、FX、Audio 的职责已经开始分离。
- Wisadel 的原版 / game#9 分离、sale#14 排除、Move Spine 分离、左右 FX 偏移独立保存，这些处理方式是合理的。

但当前实现仍然属于**“角色专属代码驱动的导入流程”**。继续按 Wisadel / Schwarz / FrostNova 的方式复制，会让新增角色所需 Editor 代码、菜单、路径、FX 配置和刷新逻辑持续膨胀。

后续建议的目标不是把角色 Gameplay 也完全数据化，而是：

> **把“素材搬运、Spine 构建、FX 导入、Audio 导入、Definition/皮肤资源生成、校验、增量刷新”全部配置驱动；只保留真正不同的角色 Gameplay、特殊 Presentation 和极少数 Import Extension。**

---

## 2. 当前流程中值得保留的部分

### 2.1 Builder 自动发现

`PrototypeOperatorRegistry` 使用 `TypeCache.GetTypesDerivedFrom<IPrototypeOperatorBuilder>()` 自动发现 Builder，这一点应该保留。

新增角色不应该再修改一个中央角色 ID 列表，也不应该在 Stage / HUD / Camera / Reward / AudioController 内加入角色名分支。

### 2.2 当前角色编译后刷新

`CurrentOperatorAssetRefreshService` 的总体方向正确：

- `DidReloadScripts` 后等待 Unity 不再 compiling/updating；
- 解析当前场景中的 `PlayableOperatorIdentity`；
- 只调用当前角色 Builder 的 `RefreshAssets()`；
- 不遍历所有角色。

这正符合“角色资源只刷新当前选中的角色”的目标。

### 2.3 外部素材与 Runtime 隔离

现有角色都在 Editor 阶段把外部素材复制到 `Assets/_Game/...`，Runtime 不直接访问解包目录。该边界必须继续保持。

### 2.4 角色差异留在角色层

Chen、Schwarz、FrostNova、Wisadel 已经证明角色之间会存在真实差异：

- Chen 有 OHMS / 复合刀光等特殊 FX 路线；
- Schwarz 有 S2 composite FX；
- FrostNova 有 Winter Skill3 的特殊帧序列；
- Wisadel 有原版/game#9 的皮肤 FX 差异和多段技能。

因此不要试图把所有角色 Runtime Gameplay 强行塞进一个“万能角色控制器”。V2 应该只统一**导入与绑定基础设施**，特殊机制继续允许角色扩展。

---

## 3. 当前实现需要优先修正的问题

### P0-1：Definition 的 Ensure 实际是 create-only

当前 `PrototypeOperatorDefinitionAssetUtility.Ensure()`：

- Asset 不存在时创建并 `Configure()`；
- Asset 已存在时直接 return，不同步最新代码中的角色名、排序、皮肤、头像 key、技能图标 key。

这会导致 Builder 已经增加/修改皮肤，但旧 `Operator_*.asset` 仍保持旧内容。

**建议：**

将 Ensure 改为真正的 Ensure/Sync：

1. 创建不存在的 asset；
2. 已存在则比较期望数据与当前序列化数据；
3. 仅有变化时调用 `Configure()` + `SetDirty()`；
4. 输出“created / updated / unchanged”。

这是以后自动生成角色 Definition 的前提。

### P0-2：脚本重载刷新不应自动保存场景

`CurrentOperatorAssetRefreshService.Refresh()` 在刷新当前场景角色后会：

- MarkSceneDirty；
- 立即 `SaveScene(scene)`。

脚本编译/Reload 是高频事件，不适合隐式保存用户当前场景编辑。

**建议拆成两种刷新模式：**

- `script reload`：只刷新 Assets；必要时对当前对象做非破坏性引用刷新，但不自动 SaveScene。
- `manual / rebuild`：允许 Apply + MarkDirty；只有明确的生成/保存动作才 SaveScene。

### P0-3：Wisadel 的 FX 实际没有做到“只导入使用项”

`WisadelLocalAssetBootstrap.ImportEffects()` 当前会：

1. 扫描整个 `effects/frames`；
2. 排除名称含 `sale#14`；
3. 只要目录里有 `f<number>.png` 就加入 ImportSelected。

这和流程文档写的“只导入要使用的 FX 帧目录”不一致。

Wisadel Runtime 实际只绑定：

- attack A/B/C start
- attack trail
- attack hit
- skill 02 start/buff/hit
- skill 03 start/trail/hit（原版/game#9）

导入器应该由**明确 FX binding 列表**生成 sourceFolders，而不是扫描整包。

### P0-4：ExtractedFrameFxImporter 当前不是真正的增量导入

`ExtractedFrameFxImporter.ImportSelected()` 对每个选中 FX：

- 删除目标 Frames 中旧 png；
- 全量复制所有帧；
- `SaveAndReimport()` Sprite；
- 删除并重建 `.anim`；
- 删除并重建 AnimatorController；
- 删除并重建 Prefab；
- 前后多次同步 `AssetDatabase.Refresh()`。

因此角色 Bootstrap 即使传入 `force=false`，一旦走到 ImportSelected，本质仍然是重建。

**建议：**

为每个 FX 生成 fingerprint：

```text
source relative path
frame count
each frame file size
latest write time
timing.json hash/mtime
import settings version
```

fingerprint 未变化时直接跳过。只有变化的 FX 才复制和重建。

### P0-5：本地源根目录硬编码

当前角色代码直接写：

- `D:\Ark\_Unpacked\wisdel`
- `D:\Ark\_Unpacked\shwaz`
- FrostNova 对应本地路径

建议增加项目级设置：

```text
Arknights Local Asset Root = D:\Ark\_Unpacked
```

角色只保存相对目录：

```text
wisdel
shwaz
frstar2
```

设置可放 EditorPrefs / ProjectSettings，不进入 Runtime。

---

## 4. 当前架构的主要扩展性问题

### 4.1 “自动注册角色”与中央 PrtsPrototypeAssetCatalog 互相矛盾

角色 Registry 已经是自动发现，但每增加本地角色仍要手工修改：

`Assets/_Game/Editor/PRTS/PrtsPrototypeAssetCatalog.cs`

加入 Default / Motion / Skin / Motion descriptor。

也就是说角色注册去中心化了，但素材描述仍然中心化。

**V2 应把本地角色 descriptor 移出 PrtsPrototypeAssetCatalog。**

PRTS Catalog 只保留真正的 PRTS/远程公共资源；本地角色 descriptor 从角色 Import Profile 自动生成。

### 4.2 LocalAssetBootstrap 重复代码过多

Wisadel / Schwarz / FrostNova 都重复实现了：

- CopyRaw
- CopySprite
- CopySpineSet
- ToAbsolutePath
- FindType
- BuildPresentation
- HasImportedXXX
- Audio copy
- AssetDatabase Save/Refresh
- 本地路径检查

建议抽成：

```text
LocalOperatorAssetImporter
LocalAssetCopyUtility
LocalSpineImportUtility
OperatorFxImportService
OperatorAudioImportService
OperatorImportValidator
```

角色专属 Bootstrap 最终应该缩小为一个 Profile 或非常薄的 adapter。

### 4.3 Factory 仍重复构建大量角色壳

Wisadel / Schwarz / FrostNova Factory 仍重复：

- new Player GameObject
- CharacterController 参数
- AddFoundation
- Inventory
- PlayerMotor
- PresentationBillboard
- Generated Presentation attach
- Placeholder
- PresentationDriver
- FX mount point
- DamageTintFlash
- WorldHealthBar
- DamageNumberEmitter

建议新增统一的：

`PlayableOperatorFactoryComposer.BuildShell(OperatorBuildPlan plan)`

角色 Factory 只填写：

- CharacterController 尺寸
- Profession / CombatFeature
- 基础攻击与防御参数
- Presentation descriptor
- Motion descriptor
- PresentationDriver 类型
- 角色 Gameplay 安装回调

不要把不同技能机制抽成同一个数据表；只统一“角色壳”。

### 4.4 角色专属菜单会继续膨胀

现在已有：

- 霜星调节
- 黑 FX 调节
- 维什戴尔 FX 偏移
- 维什戴尔单独导入菜单

未来每个角色再加两个菜单会快速失控。

建议最终只保留：

```text
ArknightsACT/角色/角色与皮肤
ArknightsACT/角色/导入当前角色
ArknightsACT/角色/强制导入当前角色
ArknightsACT/角色/当前角色 FX 调节
ArknightsACT/角色/导入诊断
```

窗口根据当前 `PlayableOperatorIdentity` / Editor 选择加载对应 Profile。

### 4.5 Audio Profile 的事件粒度不足

Wisadel 的清单已经暴露这个问题：

- Skill Start
- Skill Special Point
- Projectile Hit

不是同一个时刻，但当前通用 Profile 更偏“slot1Sfx / slot2Sfx”。

建议后续引入通用事件 Cue：

```text
AttackStart
AttackFire
AttackHit
SkillStart(slot)
SkillSpecialPoint(slot)
SkillImpact(slot)
SkillEnd(slot)
UnitBorn
UnitDead
```

每个 Cue 可绑定 0..N AudioClip + 播放策略。这样确认后的命中音不需要每个角色再写专用监听器。

---

## 5. 推荐的 V2 数据模型

建议保留两层数据，不直接完全相信自动导出的 manifest。

### 5.1 外部导出层：unity_import_manifest.json

由 ArkMod exporter 自动生成，负责“事实”：

- char / charId / displayName
- 导出的 skin 列表
- combat Spine / move Spine 文件
- 完整 animation names
- icons / avatars
- FX frame groups
- timing.json
- sound banks / audio candidates
- skill_table 机制数据
- 导出错误与缺失项

这里不应该写 ACT 项目特有的最终决策。

### 5.2 项目集成层：OperatorImportProfile

Unity 项目生成一个 `OperatorImportProfile`，负责“选择”：

```text
operatorId
sourceFolder
skins[]
  skinId
  combatSpine
  motionSpine
  avatar
skillSlots[]
animationBindings[]
fxBindings[]
audioBindings[]
excludedAssets[]
presentationLayout
importVersion
```

FX binding 示例：

```text
event        = SkillImpact
slot         = 2
skin         = game#9
sourceTag    = skill_03_hit_game#9
runtimeKey   = Skill3Hit
offsetSlot   = Skill3Hit
required     = true
```

这样“素材有哪些”和“游戏决定用哪些”不会混在一起。

---

## 6. 推荐的角色快速导入流程

### 阶段 A：扫描，不写 Assets

输入：

`D:\Ark\_Unpacked\<char>`

Unity 读取：

- `unity_import_manifest.json`
- `audio_mapping.json`
- `spine/action_gif_manifest.json`
- 必要的 `timing.json`

生成扫描报告：

```text
2 skins detected
4 spine sets detected
3 skills detected
79 FX groups detected
14 FX groups selected by binding
7 audio candidates
2 unresolved mappings
0 required files missing
```

扫描阶段不能调用 AssetDatabase 大规模导入。

### 阶段 B：自动生成 Integration Profile

自动推断：

- default skin
- skin ID
- `char_*.skel/atlas` 为 combat
- `build_char_*.skel/atlas` 为 motion
- avatar/icon key
- Attack / Skill_x 动画候选
- FX 名称候选
- audio event 候选

无法唯一确定的字段标 `NeedsReview`，不要静默猜。

### 阶段 C：人工只处理“歧义”

导入窗口只要求人工确认：

- 哪些皮肤要导入
- ACT Skill1/2 映射原作哪个技能
- 多候选 FX 选哪个
- 多候选 Audio 选哪个
- 特殊 Begin/Loop/End 映射
- 特殊召唤物 / composite effect

其余全部自动。

### 阶段 D：增量导入当前皮肤

普通编译/刷新只处理：

- 当前 Operator
- 当前 Skin
- Profile 中实际绑定的资源
- fingerprint 变化的文件

“导入全部皮肤/全部 FX”必须是显式操作，不作为普通 Refresh。

### 阶段 E：生成/同步项目资源

Generic Importer 负责：

1. 同步 Operator Definition；
2. 同步 Spine 源；
3. 构建 Presentation Prefab；
4. 导入选择的 FX；
5. 创建/同步通用 FX Tuning Profile；
6. 导入已确认 Audio；
7. 输出 Import Report。

### 阶段 F：安装角色 Gameplay

角色 Builder 只负责：

- 调用通用 BuildShell；
- 安装角色 BasicAttack / Skill 组件；
- 安装特殊 Presentation Driver；
- 如有例外，调用角色 `IOperatorImportExtension` / Runtime extension。

### 阶段 G：自动校验

每次导入后检查：

- Definition 与 Profile 是否一致；
- 默认皮肤是否唯一；
- combat/motion Spine 是否存在；
- PrefabKey 是否重复；
- 必需动画是否存在；
- 每个 required FX 是否生成 Prefab；
- FX Binding 是否引用不存在的 tag；
- Audio Binding 是否引用不存在的 wav；
- Runtime 是否出现 `D:\Ark\_Unpacked` 路径；
- 当前角色能否创建；
- 当前皮肤能否 Attach Presentation。

最后只输出一份明确报告，不让用户靠翻 Console 猜是否成功。

---

## 7. 性能优化建议

### 7.1 AssetDatabase 批处理

当前导入链路中 `ImportAsset`、`Refresh(ForceSynchronousImport)`、`SaveAssets` 调用较多。

推荐：

1. 文件系统复制阶段集中完成；
2. 使用 `AssetDatabase.StartAssetEditing()/StopAssetEditing()` 包裹可安全批处理的文件写入；
3. 一次 Refresh；
4. 批量设置 TextureImporter；
5. 只对发生变化的文件 Reimport；
6. 最后一次 SaveAssets。

注意必须用 try/finally 保证 StopAssetEditing。

### 7.2 文件变化检测

至少比较：

- File.Exists
- Length
- LastWriteTimeUtc

更稳妥的方案是保存 SHA-256，但 PNG 数量大时不应每次全量 hash。

推荐两级检测：

```text
fast path: length + mtime + frame count
slow path: 只有 fast path 有变化时才 hash
```

### 7.3 FX 级缓存

不要对一个角色做“是否所有 FX 都已经导入”的布尔判断。

缓存粒度应为：

```text
Wisadel/default/skill_02_hit
Wisadel/game#9/skill_03_hit
...
```

这样新增一个 FX 不会导致 79 个 FX 全部重建。

---

## 8. 通用 FX 调节建议

Wisadel 当前是 9 个**逻辑 offset slot**：

- BasicStart（A/B/C 三个 Start Prefab 共用）
- BasicTrail
- BasicHit
- Skill2Start
- Skill2Buff
- Skill2Hit
- Skill3Start
- Skill3Trail
- Skill3Hit

这一设计本身是合理的，但流程文档应明确：

> 一个 offset slot 可以被多个 FX prefab 共用；“Prefab 数量”不等于“调节 slot 数量”。

V2 建议用通用 Profile：

```text
OperatorFxTuningProfile
  entries[]
    key
    displayName
    rightOffset
    leftOffset
```

再由角色 FX Binding 指定 `offsetKey`。

这样不需要为每个角色创建：

- XxxFxSlot enum
- XxxFxOffsetSetting
- XxxFxTuningProfile
- XxxFxTuningWindow

只有运行时事件特别复杂的角色才保留专属 Controller。

---

## 9. 例外扩展机制

Generic Importer 必须允许角色例外，否则最终仍会出现大量 if(operatorId == ...)。

建议接口：

```csharp
internal interface IOperatorImportExtension
{
    string OperatorId { get; }

    void Validate(OperatorImportContext context);
    void ImportExtraAssets(OperatorImportContext context);
    void ConfigureGeneratedPlayer(GameObject player, OperatorImportContext context);
}
```

典型用途：

- FrostNova Winter Skill3 特殊 frame sequence；
- Schwarz S2 composite FX；
- Chen OHMS structured FX；
- 召唤物 / token；
- 非标准多 Presentation Spine。

普通角色不实现该接口。

---

## 10. 推荐实施顺序

### 第一阶段：先解决速度与稳定性

1. 修正 `PrototypeOperatorDefinitionAssetUtility.Ensure()` 为同步式 Ensure。
2. 脚本 reload 不再自动 SaveScene。
3. 抽出统一 SourceRoot 设置。
4. Wisadel 改为 explicit FX bindings，不再扫描全部 FX。
5. `ExtractedFrameFxImporter` 增加 FX 粒度增量缓存。
6. 合并不必要的 AssetDatabase Refresh / Save。

完成后，即使暂时不做 V2 Profile，新角色导入速度也会明显改善。

### 第二阶段：配置驱动导入

1. 新增 `OperatorImportProfile`。
2. 新增 manifest scanner / validator。
3. Local Spine / icon / avatar / FX / audio 全部走 Generic Importer。
4. 本地角色 descriptor 从 `PrtsPrototypeAssetCatalog` 移出。
5. 菜单收敛为“当前角色”入口。

### 第三阶段：减少每角色样板代码

1. 增加 `PlayableOperatorFactoryComposer.BuildShell()`。
2. 通用 FX tuning window。
3. Audio event cue profile。
4. Import Extension 处理少数例外角色。

---

## 11. 新角色最终期望体验

未来导入一个常规角色，理想流程应是：

```text
ArkMod 导出角色
    ↓
Unity 打开“角色导入器”
    ↓
选择 D:\Ark\_Unpacked\xxx
    ↓
自动读取 manifest
    ↓
显示：皮肤 / Spine / 动画 / FX / Audio 映射
    ↓
只确认 1~5 个有歧义的项
    ↓
点击“导入当前皮肤”
    ↓
Definition + Presentation + FX + Audio + Tuning 自动生成
    ↓
只编写该角色真正不同的 BasicAttack / Skill / PresentationDriver
    ↓
自动验证
```

常规角色不应该再手写一个 300~800 行的 LocalAssetBootstrap，也不应该再往中央菜单和 PRTS Catalog 里增加角色专属入口。

---

## 12. 对 WISADEL_IMPORT_REVIEW_SUMMARY 的最终判断

该文档对于“本次 Wisadel 到底做了什么、哪些内容尚未验证”记录得比较清楚，特别是：

- 区分了素材候选与人工确认；
- 区分了 prefab 粒子内部 startDelay 与 Gameplay 释放延迟；
- 明确 Runtime 不读取解包目录；
- 明确 game#9 / sale#14 的范围；
- 明确 Unity 实机验收仍未完成。

需要补强的不是 Wisadel 个别步骤，而是把它从“角色专属导入范例”升级为“通用导入系统的一个 Profile”。

因此建议：

> **不要继续以 WisadelLocalAssetBootstrap 为模板复制第五、第六个角色。下一步应先完成第一阶段的 6 项基础优化，再接更多角色。**
