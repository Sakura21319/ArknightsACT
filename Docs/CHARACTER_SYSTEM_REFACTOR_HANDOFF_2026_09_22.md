# 角色系统解耦 / 局内切换重构交接

日期：2026-09-22  
分支：`codex/mobile-city-map-generator`

## 目标

本轮重构将“当前角色”从 Stage / HUD / 奖励 / 商店 / 音频等通用系统中解耦，解决后续新增角色时需要持续添加 `Chen` / `Schwarz` 特判的问题，并为局内角色切换提供稳定入口。

核心约束：

- 通用系统不能判断具体角色类或 `OperatorId`。
- 角色自己的技能、表现、音频、技能强化由角色自己声明。
- 局内 Run 状态随当前角色切换迁移。
- 角色本地技能状态可以在切走后保留，切回来继续使用。
- 新一局开始时必须清理所有已注册角色的局内状态，而不只是当前角色。
- 不做配队系统；局内只保留简单的“当前角色切换”。
- UI / 按键适配器不得直接 SetActive 切 Player，应统一调用角色切换服务。

## 新运行时结构

### PlayerRuntimeContext

文件：

`Assets/_Game/Scripts/Gameplay/Characters/PlayerRuntimeContext.cs`

职责：

- 持有唯一 `ActivePlayer`
- 注册所有本局可用角色
- 按 `operatorId + skinId` 查找角色
- 执行角色切换
- 保持切换位置 / 朝向
- 保持生命值比例
- 触发 `ActivePlayerChanged(previous, next)`
- 调用 `IPlayerSwitchStateTransfer` 迁移公共 Run 状态

常用 API：

```csharp
PlayerRuntimeContext.Instance.RegisterPlayer(operatorRoot);
PlayerRuntimeContext.Instance.SwitchTo(operatorRoot);
PlayerRuntimeContext.Instance.TrySwitchTo("Schwarz", "snow#1");
```

### PlayableOperatorSwitchController

文件：

`Assets/_Game/Scripts/Gameplay/Characters/PlayableOperatorSwitchController.cs`

这是 UI / 菜单 / 关卡事件应该使用的公开入口。

支持：

- `Register(...)`
- `RegisterReserve(...)`
- `TrySwitchTo(operatorId, skinId)`
- `TrySwitchTo(Transform)`
- `TryCycleNext()`
- `CanSwitchNow()`

默认阻止以下状态强制切人：

- 普攻动作中
- 技能施法中
- 冲刺中
- 当前角色死亡
- 切换冷却中

核心控制器不绑定键位。Prototype 场景额外挂了 `PrototypeOperatorSwitchInput`，当前仅用 `Tab` 调用 `TryCycleNext()`；正式交互以后可直接替换这个很薄的输入适配器。

### IPlayerSwitchStateTransfer

需要随角色切换迁移的数据实现：

```csharp
public interface IPlayerSwitchStateTransfer
{
    void CopySwitchStateTo(Transform destination);
}
```

当前已接入：

- `CollectibleInventory`
- `LevelUpgradeInventory`
- `CharacterSkillUpgradeInventory`
- `ScavengingInventory25D`
- `TemporaryCombatBuffs`

不应该实现该接口的典型内容：

- 当前角色动画状态
- 当前角色技能施法协程
- 角色自身技能 CD / SP（切回该角色时继续保留）
- Spine / FX / 瞄准模式表现

## 技能强化

`CharacterSkillUpgradeInventory` 现在会保存本局获得过的全部角色技能强化。

切换逻辑：

1. 陈获得陈的强化。
2. 切黑，库存会迁移，但黑只应用 `CharacterId == Schwarz` 且自身 Applier 支持的条目。
3. 黑继续获得黑强化。
4. 再切回陈，陈的 Applier 会根据完整库存恢复陈自己的强化。

`PrototypeRogueliteFactory` 不再根据当前角色选择技能强化池。

现在会自动扫描：

`Assets/_Game/Data/Roguelite/SkillUpgrades/**`

下的全部 `CharacterSkillUpgradeDefinition`。

以后新增角色无需修改中央技能池函数。

## 通用 Player 引用

以下系统已经改为从 `PlayerRuntimeContext` 解析当前角色，并在关键系统中监听 `ActivePlayerChanged`：

- `RogueliteGameFlowController`
- `RogueliteStageRuntimeController`
- `RogueliteShopController`
- `RogueliteRewardController`
- `CharacterSkillUpgradeRewardController`
- `LevelUpRewardController`
- `RogueliteRouteController`
- `PrototypeRoomLoopController`
- `RoguelitePrototypeAudioController`
- `CameraFollow25D`
- `CameraFollow2D`
- `Prototype25DCameraFollow`

StageRuntime 在切人时还会重新建立当前角色与已生成敌人的 IgnoreCollision 关系。

旧场景兼容：

`RogueliteGameFlowController` 会在运行时自动检查并补齐：

- `PlayerRuntimeContext`
- `PlayableOperatorSwitchController`

因此已有 PrototypeRun 不要求仅为了这次架构重构而强制重建场景。

## 新局重置

`RogueliteGameFlowController.BeginOperation()` 现在会遍历：

`PlayerRuntimeContext.RegisteredPlayers`

并对全部角色执行新局重置：

- 清空藏品
- 清空通用等级强化
- 清空角色技能强化库存
- 清空搜刮背包 Run 状态
- 恢复生命
- 调用 `IPlayerRunResettable.ResetForNewRun()`

这样待机角色不会把上一局的角色本地状态带入下一局。

## 角色音频

新增：

`Assets/_Game/Scripts/Gameplay/Characters/PlayableOperatorAudioProfile.cs`

Stage / Audio 通用系统不再写：

- `if Schwarz`
- `else Chen`

陈和黑自己的 Factory 分别创建自己的 `PlayableOperatorAudioProfile`。

`RoguelitePrototypeAudioController` 在角色切换时：

1. 解绑旧角色技能 / 普攻 / Health 事件
2. 读取新角色 AudioProfile
3. 绑定新角色事件

如果新角色没有 Profile，不会继续错误播放上一角色的专属音频。

## Player 工厂解耦

新增：

`Assets/_Game/Editor/PlayableOperatorPrototypeComposer.cs`

统一负责所有角色的基础装配：

- Health
- StatusController
- CombatEntity / Team.Player
- PlayerInputReader
- PlayerDashController
- PlayerAttackController
- PlayerDamageGate
- PlayerCombatProfile
- PlayableOperatorIdentity
- PlayerSkillController
- CollectibleInventory
- LevelUpgradeInventory
- CharacterSkillUpgradeInventory
- TemporaryCombatBuffs

角色 Factory 只负责：

- 基础数值参数
- Profession / CombatFeature
- 角色 Identity / UI Resource Key
- 角色技能组件
- 角色技能 UpgradeApplier
- 角色特殊攻击模式
- Spine / Presentation
- 角色 FX
- 角色 AudioProfile

陈与黑已经迁移到该 Composer。

## 后续新增角色标准流程

假设新增 Texas：

1. 创建角色技能 / 特殊战斗组件。
2. 创建 `TexasSkillUpgradeApplier : ICharacterSkillUpgradeApplier`。
3. 在角色 Factory 调用 `PlayableOperatorPrototypeComposer.AddFoundation(...)`。
4. 添加 Texas 自己的技能组件。
5. 调用 `PlayableOperatorPrototypeComposer.CompleteGameplay(...)`。
6. 添加 `PlayableOperatorAudioProfile`。
7. 创建 Texas 的 `CharacterSkillUpgradeDefinition` 资产并放在 `Data/Roguelite/SkillUpgrades/Texas/`。
8. Prototype 场景要允许切到 Texas 时，只需在 `PrototypeOperatorRosterFactory` 的 `KnownOperatorIds` 加 `Texas`，并在 `CreateOperator()` 增加一条 Texas Factory 创建分支。
9. 局内切换仍统一调用：

```csharp
switcher.TrySwitchTo("Texas", "default");
```

当前 Prototype 直接按 `Tab` 在已注册角色间循环切换。这里的待机角色只表示“切换目标”，不是编队系统。

正常情况下无需修改：

- StageRuntime
- GameFlow
- Shop
- RewardController
- HUD
- Camera
- AudioController
- 中央技能强化池

## HUD

`GameplayHUDController` 仍由每个 Player 的 `PlayerSkillController` 自动创建。

角色切换时：

- 旧 Player 失活 -> 旧 HUD 停止
- 新 Player 激活 -> 新 HUD 使用该角色自己的 `PlayableOperatorIdentity` / 技能实例

因此无需再建立一个“陈 HUD / 黑 HUD”的中央分支。

## 测试

新增：

`Assets/_Game/Tests/EditMode/PlayerRuntimeContextTests.cs`

覆盖：

- 切换后 ActivePlayer 正确
- 旧 Player 失活，新 Player 激活
- 血量比例迁移
- `IPlayerSwitchStateTransfer` 状态迁移
- `operatorId + skinId` 查找切换

`Game.Tests.EditMode.asmdef` 已增加 `Game.Gameplay` 引用。

FolderBridge 已执行：

- source test smoke：通过
- source build validation：通过
- 29 个本轮改动 C# 文件花括号 / `#if/#endif` 配对检查：通过
- 通用 Roguelite / Audio / PrototypeStageRuntimeFactory 角色硬编码搜索：无 Chen/Schwarz 角色分支残留

注意：FolderBridge 当前没有项目 Unity Editor build task，因此以上不等价于 Unity 实际脚本编译。最终仍应在 Unity 打开工程后确认 Console 为 0 compile errors，并执行 EditMode Tests。

## 当前边界

本轮只做“方便新增角色 + 简单局内切换”。

- 不做配队。
- 不做角色选择页面。
- 不做角色轮盘。
- 不改现有主页里原本就存在的“编队”占位按钮。
- Prototype 场景用 `Tab` 在已注册角色间简单切换。
- 正式系统以后只需要替换输入/UI 层，`PlayableOperatorSwitchController` 和各通用系统无需再改。
