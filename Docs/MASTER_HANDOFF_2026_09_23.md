# ArknightsACT 总交接文档

更新时间：2026-09-23
项目：D:\WorkSpace\ArknightsACT
主要运行场景：Assets/_Game/Scenes/PrototypeRun.unity
项目 Unity 版本：6000.0.23f1

> 本文件是 2026-09-23 起的总交接入口。
>
> 目的不是保存每一轮实验历史，而是告诉下一位接手者：项目现在是什么、已经完成什么、哪些只是调试态、哪些结论不能回退、下一步应该按什么顺序继续。
>
> 若本文件与更早的 Phase / Handoff 文档冲突，以 本文件 + 对应最新专项 Handoff + 当前源码 为准。

---

# 1. 项目当前定位

当前项目已经从早期的横版 ACT / 路线型 Roguelite 原型，推进为：

**2.5D ACT 战斗 + 连续切尔诺伯格移动城区探索 + 搜打撤主循环 + 轻度肉鸽构筑 + 明日方舟藏品体系。**

当前不是单纯复刻塔防，也不是纯房间制 Roguelite。

核心体验是：

    主页
      ↓
    行动准备
      ↓
    进入切城
      ↓
    探索城区 / 搜建筑 / 搜容器 / 战斗 / 开箱 / 使用设施
      ↓
    藏品进入本局并立即生效
    普通物资进入未撤离二维背包
      ↓
    继续深入下一 Stage 或返回撤离点
      ↓
    主动撤离
      ↓
    结算
      ↓
    普通物资进入系统仓库
      ↓
    仓库 / 交易出售
      ↓
    获得长期龙门币
      ↓
    返回主页

失败时：

    行动中死亡
      ↓
    未撤离普通物资丢失
    未撤离藏品持续效果撤销
      ↓
    失败结算
      ↓
    返回主页

最关键的产品边界：

- 跨 Stage 不等于撤离。
- 只有真实撤离才会让普通物资安全进入长期仓库。
- 藏品严格属于当前 Run，不进入长期仓库。
- 源石锭是本局资源，不是长期货币。
- 龙门币是长期局外货币。
- 当前不做完整编队系统，只做局内当前角色切换。
- 当前地图只围绕切尔诺伯格，不扩展其他城市。
- 现阶段应优先做“完成度、稳定性和手感”，不要继续无边界横向加系统。

---

# 2. 当前完成度总览

| 模块 | 状态 | 当前结论 |
| --- | --- | --- |
| 通用 ACT 移动 / 普攻 / 冲刺 / 技能 | 已有稳定骨架 | 继续保持 Gameplay 与角色表现解耦 |
| 陈 | 基础完成 | ACT 动作、技能、HUD、提取帧 FX 管线已接入 |
| 黑 Schwarz | 已接入，仍处视觉调试态 | 三皮肤、S2 + S3、狙击模式与皮肤 FX 已接 |
| 霜星 FrostNova | 已接入，当前主要调试对象 | 四套 Presentation、冬痕技能/FX/常驻 Buff 已接 |
| 局内角色切换 | 架构完成，交互仍偏 Prototype | 统一走 PlayerRuntimeContext / PlayableOperatorSwitchController |
| SP 技力 | 已完成基础迁移 | 已替代旧纯 cooldown 主逻辑 |
| 正式战斗 HUD | 第一版完成 | HP、双技能 SP、Ready、回技反馈、背包、源石锭 |
| 藏品数据库 | 正式数据完成 | 118 件，最终 XLSX 为唯一人工源 |
| 搜刮容器 | 完成第一版并扩展 | 12 类建筑、28 类容器、五档掉落 |
| 二维背包 | 第一版完成 | 初始 4x5，可扩容至 6x8，支持拖拽/整理/丢弃/重拾 |
| 真撤离 | 第一版完成 | 每阶段可返回撤离，跨 Stage 不自动结算 |
| 系统仓库 / 买卖 | 第一版完成 | 长期库存、LMD、购买/出售、批量出售 |
| 主页 / 结算 / 准备页 | 第一版完成 | UGUI 主壳已替代旧主 IMGUI |
| 切城生成器 | 主体完成 | 三阶段 4x3 / 4x4 / 5x4 连续城区 |
| 城区视觉 | 已进入精修阶段 | 四区差异、建筑立面、道路、门牌、设施、地标 |
| 小地图 / 探索指引 | 第一版完成 | 迷雾、建筑状态、M 展开、N 导航 |
| 市政核心 / 高塔 | 已实现 | 可进入、可登高、含真实搜索资源与设施 |
| Defense / Armor | 未正式化 | 当前部分藏品仍走兼容映射 |
| 开局携带 / 安全箱 / 完整配装 | 未做 | 属于后续搜打撤深化 |
| 长期基地 / 永久成长 | 未做 | 只保留方向和占位 |
| 真正多人联机 | 未做 | 设施只预留 Authority / Revision 边界 |

---

# 3. 架构硬规则

## 3.1 依赖方向

当前继续遵守：

    Game.Core
       ↓
    Game.Combat
       ↓
    Game.Gameplay
       ↓
    Character-specific Skill / Presentation / FX / Composition

含义：

- Core 不认识玩家、角色、动画、输入。
- Combat 不认识陈、黑、霜星。
- 通用 Gameplay 不允许通过 characterName、OperatorId、具体角色类型分支行为。
- 角色差异放到 Assets/_Game/Scripts/Gameplay/Characters/<Character>/。
- 表现层负责 Spine / FX / SFX 映射，但真实伤害、目标、冷却、技力、战斗状态由 Gameplay / Combat 决定。

禁止重新引入：

- 巨型 PlayerController。
- if (characterName == "Chen") 之类的通用层特判。
- 动画直接修改 HP。
- FX 脚本直接结算伤害。
- Gameplay 硬编码 PRTS URL、model id 或 atlas/skel 路径。
- UI 自己直接 SetActive 切 Player。

## 3.2 当前角色上下文

核心：

- PlayerRuntimeContext
- PlayableOperatorSwitchController
- PlayableOperatorIdentity
- IPlayerSwitchStateTransfer
- IPlayerRunResettable

PlayerRuntimeContext 负责唯一 ActivePlayer，并广播 ActivePlayerChanged。

PlayableOperatorSwitchController 是正式切换入口。

切换时需要保持/迁移：

- 位置；
- 朝向；
- 生命值比例；
- 本局公共状态；
- 搜刮背包；
- 当前需要迁移的通用 Run 状态。

角色自己的技能内部状态可以保留在自己的实例上。

新 Run 必须重置所有已注册角色，而不是只清理当前角色。

当前 Prototype 输入由 Tab 打开/关闭角色切换界面，1-9 选择目标。以后可以替换输入/UI，但不要绕过 PlayableOperatorSwitchController。

---

# 4. 当前输入与主要交互

当前 README 已同步到源码方向：

- WASD / 方向键：移动
- Space：跳跃
- J / 左键：普通攻击
- K / Shift：冲刺
- L：技能槽 1
- I / 右键：技能槽 2
- Tab：局内角色切换面板
- 1-9：角色面板中选人，或搜刮窗口中拾取对应槽位
- E：撤离点 / 已开放下一阶段入口等流程交互
- F：搜索容器 / 拾取世界掉落
- G 长按：使用城市设施
- B：背包
- U：背包内扩容
- R：背包内自动整理
- T：搜刮窗口中尽量全部拾取
- M：展开 / 收起探索地图
- N：切换探索 / 返程 / 下一阶段导航
- Esc：关闭当前模态 UI

不同角色可能在技能状态下额外消费鼠标输入，例如 Schwarz S3 的瞄准射击。

---

# 5. 角色系统当前状态

## 5.1 陈 Ch'en

陈是最早建立 ACT 架构的角色，仍是通用设计的重要参考。

当前核心动作约束：

- 地面三段普攻。
- 不做空中攻击。
- 不做下劈。
- 不做 Dash Attack。
- 冲刺只作为位移 / 无敌 / 动作取消工具。
- 两个主动技能。

当前技能映射：

- Slot 1：赤霄·拔刀，对应原版 S2。
- Slot 2：赤霄·绝影，对应原版 S3。

角色显示由战斗 Spine 负责；BaseMotion 只作为 Move 动作重定向来源。

### 陈 FX 当前规则

原客户端 AssetBundle FX 不再作为运行时主链。

当前正式方向：

    EffectExtractor.exe
      ↓
    PNG 帧
      ↓
    ExtractedFrameFxImporter
      ↓
    Sprite / Animation / Prefab
      ↓
    Character FX Controller
      ↓
    监听技能 / 普攻事件播放

陈保留 CustomFxMountPoint 作为稳定挂点。

OHMS 导入器、原客户端 FX 目录、依赖分析脚本只用于研究和反查，不要重新挂回运行时。

---

## 5.2 黑 Schwarz

当前已接入三套皮肤：

- Default
- Snow / snow#1
- Striker / striker#1

当前两个技能槽：

- Slot 1：原版 S2 暮眼锐瞳，对应 SchwarzSkill1。
- Slot 2：原版 S3 战术的终结，对应 SchwarzSkill2。

当前仍处视觉调试阶段，两技能使用 debugInfiniteDuration：

- 可手动开启/关闭；
- 方便反复验证 FX；
- 正式平衡前必须恢复正常持续时间和技力规则。

### 普攻

已经是独立远程射手逻辑，不使用陈的近战判定。

相关核心：

- SchwarzRangedBasicAttack
- IPlayerBasicAttackModifier
- PlayerAttackController

### S3

当前 S3 是独立狙击模式：

- 开启状态本身不自动开枪；
- 鼠标决定朝向；
- 点击有效目标后才射击；
- 使用运行时 RawImage 狙击镜；
- 有 authored skill_03_trail_<skin> 时不再叠加程序 tracer；
- S3 Hit 使用 skill_01_hit_<skin>；
- buff_02 / buff_03 为持续层，不是一次性起手 FX。

### Schwarz 当前待办

高优先级：

- Unity 实机确认 S3 Hit 大小与位置。
- 继续校正 Snow S3 Trail 高度和左右偏移。
- 确认 S3 shot 动作和视觉节奏。

视觉完成后：

- 关闭 debugInfiniteDuration。
- 恢复正式持续时间 / 技力。
- 等正式 Defense / Armor 层完成后再接黑的破甲 / 天赋逻辑。

不要回退：

- 不混用三套皮肤 FX。
- S2 不播放 S3 动作。
- 进入 S3 不等于立即开枪。
- authored S3 Trail 存在时不叠程序 tracer。
- 不用负 Transform Scale 粗暴镜像整套 FX。
- 左右偏移继续独立维护。

---

## 5.3 霜星 FrostNova

霜星是当前最新、最需要继续实机调试的角色。

当前有四套 Presentation：

| SkinId | 显示 | 来源 |
| --- | --- | --- |
| default | 霜星 | spine/enemy_1505_frstar |
| winter#1 | 冬痕 | spine/enemy_1510_frstar2 |
| new#1 | 霜星·新 | spine_new/enemy_1505_frstar |
| winter_new#1 | 冬痕·新 | spine_new/enemy_1510_frstar2 |

四套 prefab 使用独立 PrefabKey，不能重新只按 BaseName 生成，否则 spine 与 spine_new 会互相覆盖。

### Skin 恢复

运行时必须从 PlayableOperatorIdentity.SkinId 恢复 FrostNovaSkinVariant。

不要删除 SyncSkinFromIdentity / Awake 恢复链。

这是此前 Winter 被错误当成 Default、导致技能动作选错的根因修复。

### 当前技能

当前主要以 Winter / 冬痕作为调参基准。

调试阶段：

- 无 CD；
- 技力始终可用；
- 不消耗；
- 可连续释放；
- 但完整动画仍必须播放完。

这个无限技能逻辑只属于 FrostNova 自己，不允许影响全局技能系统。

当前映射：

- Slot 1 / 冰环：Winter Spine Skill_1。
- Slot 2 / 冰暴：Winter Spine Skill_3。

此前 Skill3 逐帧角色覆盖方案已经撤销。

FrostNovaFrameSequenceAsset、FrostNovaWinterSkill3Frames.asset 等属于排查遗留，目前运行时不应依赖。

### 动画完整播放

已建立原则：

**动画速度只改变总播放时间，不能因为 gameplay 状态提前结束而截掉动作。**

SpineCharacterPresentation2D 已增加动画速度/时长查询能力。

FrostNova 技能会等待 RemainingActionVisualSeconds，再退出施法状态。

### 普攻

已经完成：

- 远程 Arts 普攻。
- 普攻移动锁跟随真实攻击动画时间，不再跟旧 recovery。
- 出伤时间由 Trail Delay + Projectile Flight 自动推导。
- 当前约 0.82 秒。
- 左右独立弹道 Offset。
- Offset 只影响发射起点，最终仍插值到真实目标，修复“终点多飞一截”。

### 冰环

当前：

- 播放 Skill_1。
- 可以空放。
- 有目标时自动搜索目标。
- Range FX 跟随目标 Transform。
- 无目标时只播完整角色动作，不生成冰环打击 FX，也不造成该次范围伤害。

### 冰暴

正确链路：

    Skill_3
      ↓
    frstar2_skill_03_start   手部
      ↓
    frstar2_skill_03_range   第一段聚气
      ↓
    frstar2_skill_03_range_02 第二段爆开

不要把 Skill2 资源误接到 Skill3。

### Winter Buff

当前结论：

- buff_03：眼部特效，当前默认关闭，触发时机仍需确认。
- buff_04：用途未确认，当前默认关闭。
- buff_05：常驻背部特效。
- buff_01 / buff_02：当前不使用。
- buff_06：manifest 中有依赖迹象，但当前导出 frames 中无资源。

Buff05：

- Winter 启用时生成；
- 持续跟随；
- 循环播放；
- Skill3 开始隐藏；
- Skill3 完整结束后恢复；
- 左右 Offset / Angle 独立调节。

### FrostNova 当前实机待验

P0：

1. 冰环
   - 确认 Slot1 始终 Skill_1。
   - 目标 FX 正确附着并跟随。
   - 空放无伤害 / 无 Range FX。

2. 普攻
   - 左右发射起点。
   - 终点必须都落真实目标。
   - 动画结束后立即移动是否自然。

3. Skill3
   - 确认始终使用 Winter 原生 Skill_3。
   - 起手和收尾无位移跳变。
   - Buff05 全程隐藏并在完整结束后恢复。

4. Buff05
   - 左右位置继续微调。
   - 检查循环是否闪烁。

5. Buff03 / Buff04
   - 继续确认原版出现条件。
   - 未确认前不要强行默认开启。

6. FrostNova 音频
   - 当前没有可靠实际音频文件。
   - 不要猜测绑定，也不要复用其他角色声音。

---

# 6. 战斗 HUD 与技力

正式 UGUI Combat HUD 已取代旧临时 HUD。

当前包括：

- 玩家头像；
- HP；
- 两条技能行；
- SP / 技力；
- 技能快捷键；
- 技力未满绿色、真正满值黄色；
- 角色头顶 Ready；
- 藏品触发 +N SP 短反馈；
- 背包使用格数；
- 当前源石锭。

Ready 规则：

- 技能行内部不显示 READY 文本。
- Ready 只通过角色头顶原版风格标识表现。
- 使用指定 sprite_skill_ready__-3542339109505237889。
- sprite_skill_bg 做扩散淡出。
- 单技能 Ready 为普通黄色态。
- 双技能同时 Ready 为增强橙红态。

已退役：

- PlayerSkillPointHUD 的 OnGUI，只保留兼容空壳。
- RogueliteProgressHUD 的旧 Run Level / 源石锭重复 HUD。
- ScavengingWindowUI 的旧常驻右下背包状态条。

不要恢复重复 HUD。

### 当前战斗底层最大缺口

Defense / Armor 正式化尚未完成。

当前需要未来建立：

- Physical Defense；
- Arts Resistance；
- Armor Penetration；
- DamageSystem 统一结算；
- 藏品防御类效果迁移；
- Schwarz 破甲/天赋迁移。

目前部分“防御增加/敌人防御下降”等效果仍用 IncomingDamage / PhysicalDamage 的兼容映射，未来正式系统上线时必须迁走，不能两套同时生效。

---

# 7. 藏品 / 数据库

唯一正式人工维护源：

Docs/RogueRelics/RogueRelicDatabase_IS1_CurrentPool.xlsx

当前正式数据：

- 总物品：118
- 藏品：57
- 收集品：61
- Runtime 图标：118 / 118
- 重复 ID：0
- 未解析效果：0

正式运行时：

- Assets/_Game/Resources/ScavengingCatalog.json
- Assets/_Game/Resources/RogueRelics/RuntimeIcons/
- Logs/RogueRelicRuntimeImport.json

Importer：

- Assets/_Game/Editor/Tools/import_is1_runtime_catalog.py
- Assets/_Game/Editor/RogueRelicRuntimeImportBootstrap.cs

硬规则：

**不要再用旧预处理规则、CSV、旧图标爬取脚本覆盖最终 XLSX。**

legacy_roguelike 只作为离线兜底资源，不是正式 Runtime 数据源。

---

# 8. 当前已生效的藏品 / 战斗效果

CollectibleInventory 当前已经实际接入：

- AllDamagePercent
- PhysicalDamagePercent
- ArtsDamagePercent
- TrueDamagePercent
- MaxHealthPercent
- EnemyMaxHealthPercent
- IncomingDamagePercent
- AttackSpeedPercent
- InitialSkillPoints
- SkillPointRecoveryPerSecond
- SkillPointRecoveryPercent
- SkillPointOnBasicHit
- SkillPointOnSkillCast
- IngotOnAcquire

藏品生命周期：

    获得实体奖励
      ↓
    ScavengingInventory25D.TryAcceptReward
      ↓
    进入未撤离背包
      ↓
    CollectibleInventory.Acquire
      ↓
    本局效果立即生效

主动丢弃：

- 从未撤离背包移除；
- Release 持续效果；
- 一次性 InitialSkillPoints / IngotOnAcquire 不倒扣。

重新拾取：

- 恢复持续效果；
- 不重复触发一次性奖励。

Run 结束：

- 藏品不进入系统仓库；
- 清除本局持续效果。

---

# 9. SP / 技力系统

旧的纯 cooldown 技能逻辑已迁移为 SP 技力池。

接口：

Assets/_Game/Scripts/Gameplay/Abilities/IPlayerSkill.cs

陈当前基准：

- 赤霄·拔刀：20 消耗，10 初始，1/s。
- 赤霄·绝影：30 消耗，20 初始，1/s。

ReduceCooldown 仍留兼容，但语义已经转为等价技力恢复。

FrostNova 当前为了表现调试走角色专属无限技能逻辑，不能据此改全局 SP 规则。

---

# 10. 搜刮容器与建筑

当前城市搜索体系已从早期少量箱子升级为：

- 12 类搜索建筑。
- 28 类容器。
- 五档容器等级。
- 每类建筑有普通容器池、稀有容器、空置概率。
- 装饰随机与掉落随机分离，视觉改动不应改变奖励。

五档大致从杂物到封存高价值容器，掉落仍基于现有四档物品珍稀度，不新增不存在的物品类别。

容器规则：

- deterministic weighted roll；
- 固定 RewardSeed；
- 重开不 reroll；
- 尽量避免同箱重复；
- row-major / first-fit 排列；
- 不用战斗随机流。

搜索过程：

- 靠近后 F 打开；
- 自动逐件搜索；
- 每件有 Unsearched / Searching / Revealed / Taken；
- 移动、离开、受击等可打断当前搜索；
- 已揭示 / 已拿取状态保留。

---

# 11. 二维背包

初始规格：

**4 x 5 = 20 格**

支持真实物品尺寸：

- 1x1
- 1x2
- 1x3
- 2x1
- 2x2

当前支持：

- first-fit 自动放置；
- 手动拖拽；
- 合法位置判断；
- R 自动整理；
- 拖出背包生成世界物品；
- 世界物品重新拾取；
- 藏品丢弃 / 重拾的效果生命周期。

暂未完成：

- 物品旋转；
- 冲突占位的直接交换；
- 更完整的拖拽落点高亮和替换交互。

### 本局扩容

使用源石锭。

当前升级：

| 等级 | 尺寸 | 总格 | 价格 |
| --- | --- | ---: | ---: |
| Lv0 | 4x5 | 20 | 初始 |
| Lv1 | 4x6 | 24 | 4 |
| Lv2 | 5x6 | 30 | 7 |
| Lv3 | 5x7 | 35 | 10 |
| Lv4 | 6x7 | 42 | 14 |
| Lv5 | 6x8 | 48 | 18 |

跨 Stage 保留，本 Run 结束后重置。

---

# 12. 真撤离 / 仓库 / 经济

每个 Stage 起始区域附近生成 ExtractionPoint。

E 打开撤离选择：

- 继续探索；
- 确认撤离。

关键规则：

- Stage1 → Stage2 不结算。
- Stage2 → Stage3 不结算。
- 最终 Boss 后也不是自动 FinishRun。
- 玩家仍需真实撤离。

### 成功撤离

普通物资：

- 写入 RogueliteMetaState.Warehouse。
- 不自动出售。

藏品：

- 不进入长期仓库。
- Run 结束统一清理效果。

### 长期 Meta

RogueliteMetaState 当前保存：

- 龙门币；
- 指挥官等级占位；
- 仓库 item id → count。

当前持久化：

PlayerPrefs / ArknightsACT.MetaState.v1

### 仓库 / 交易

已支持：

- 我的物品 / 商店；
- 搜索；
- 低价值筛选；
- 物品网格；
- RuntimeIcons；
- 持有数量；
- 仓库总价值；
- 详情；
- 单件购买；
- 单件出售；
- 批量出售当前结果；
- 一键出售低价值物品。

当前低价值阈值：

<= 2000 LMD

购买价：

SellPrice x 1.25，按 10 向上取整。

尚未做：

- 批量购买；
- 排序；
- 收藏 / 锁定；
- 仓库容量；
- 动态市场；
- 每日库存；
- 从仓库带物资进局。

---

# 13. 主页 / 行动准备 / 结算

当前主壳已经切到运行时 UGUI。

旧 RogueliteGameFlowController.OnGUI 只作为 fallback。

主页视觉规则：

- 方案 B：精简战术卡片。
- 左侧只保留左下“指挥官等级”。
- 右侧：开始行动、仓库/交易、编队占位、任务占位。
- 不恢复干员 / 模组 / 采购 / 情报等底部横条。
- 不显示局内源石锭。
- 不使用 Gameplay 摄像机作为主页背景。

当前正式背景已经确认文件存在：

Assets/_Game/Resources/UI/Shell/home_chernobog.jpg

当前 LMD 图标也已确认文件存在：

Assets/_Game/Resources/UI/Currency/lmd.png

因此旧文档中“尚未确认是否落盘”的说法已经过时。

仍需 Unity Play Mode 实际确认：

- Sprite Import 正常；
- 主页实际显示；
- 不透 Gameplay 场景；
- 4K / 16:9 下布局稳定；
- LMD 图标比例与清晰度正常。

### 行动准备页

第一版已经有：

- 切城地区选择；
- 风险等级 I-V。

目前风险等级的 UI / 数据选择已存在，但不要假设它已经完整驱动敌人强度和全部战利品倍率。

当前城市区域本身已经有 Core / Industrial 等区域倍率；行动准备风险等级与这套区域风险仍需要后续统一设计，避免重复倍率体系。

---

# 14. 切城地图生成器

当前不再是旧 2x2。

默认三阶段：

| Stage | 区块 |
| --- | --- |
| 1 | 4 x 3 |
| 2 | 4 x 4 |
| 3 | 5 x 4 |

地图是连续可走城区，可绕行和返回。

起点 / 撤离区域固定在西南安全侧，Boss / 下一阶段方向总体位于东北。

当前仍以种子可复现为重要约束。

不要新增另一套平行 BuildingGenerator 或 CityGenerator。

---

# 15. 四区城市设计

当前每层默认包含四种区域角色：

- 外围防线
- 核心区
- 废墟区
- 工业区

当前最新结论：

**普通房屋恢复规则排列。**

9/22 文档中的整体错落、随机偏转建筑方案已经被 9/23 方案取代。

可以保留：

- 不同建筑用途；
- 尺寸差异；
- 立面差异；
- 容器池差异；
- 有约束的细节随机。

不要恢复整片街区的大幅随机旋转/错位。

### 区域差异

外围：

- 灰绿；
- 防线围板；
- 相对安全。

核心：

- 锈橙警戒；
- 管制文字；
- 执勤 / 档案建筑；
- 更高敌人强度与资源密度。

废墟：

- 灰褐；
- 更高空置房；
- 破碎铺板和断柱。

工业：

- 蓝灰金属；
- 压缩机、维修/货运建筑；
- 二层增加高架管线；
- 敌人生命有额外倍率。

---

# 16. 城市建筑 / 视觉精修

当前 12 种建筑有：

- 独立用途；
- 各自容器池；
- 立面颜色；
- 程序混凝土纹理；
- 窗框 / 玻璃 / 窗台；
- 檐口；
- 落水管；
- 门灯；
- 墙面修补；
- 告示；
- 不同屋顶 / 门头 / 雨棚等。

街道已有：

- 路灯；
- 排水格栅；
- 长椅；
- 沥青修补；
- 铺装接缝；
- 门前独立地坪；
- 中文门牌；
- 装卸警示等。

原则：

- 用发光材质表达大量小灯，不要给每栋建筑堆实时灯。
- 附加视觉细节不改变碰撞 / 导航。
- 装饰随机数与奖励随机数分离。

---

# 17. 当前地标与垂直探索

## Stage 1 市政核心

一层存在：

- 市政应急指挥所；
- 封存档案广场；
- 两栋相向可进入大厅；
- 二楼回廊；
- 档案 / 办公 / 军需容器；
- 档案保险柜；
- 真实窗洞、窗台和内部路线。

## Stage 2 动力调度塔

约 43 米天线尖端高度。

已有：

- 四座重型支脚；
- 中央井筒；
- 维护环；
- 装甲片；
- 顶部控制室；
- 信号灯；
- 底层十字通路；
- 16 段可步行检修楼梯 / 坡段与转角平台；
- 34.7 米附近塔冠屋顶可达；
- 塔顶档案保险柜和军需箱。

角色可实际走上去，不依赖传送。

导航只通过正确入口节点连接不同高度，避免视觉可见时错误跨空连接。

---

# 18. 城市设施

基础三类：

- 罗德岛应急补给：恢复生命。
- 天灾观测中继：测绘附近区域。
- 源石配电柜：解除特定储备箱封锁。

高塔新增三类风险互动：

- 过载柜：支付最大生命 20%，解锁贵重货柜。
- 泄压阀：3 秒预警后，对半径 4m 活跃生命造成最大生命 15% 伤害，再开放设备箱。
- 联动开关：12 秒内启动另一端，完成后解锁缓存，可重试且不重复发奖。

设施已有：

- StableId；
- Revision；
- Ready / Armed / Spent；
- 剩余时间；
- 事件；
- IsAuthority。

但：

**这不是多人联机实现。**

未来接网络时必须由服务器验证：

- 距离；
- 视线；
- 长按时长；
- 当前生命；
- 请求 Revision；
- 奖励是否已经领取。

不能相信客户端 Activate 请求。

---

# 19. 小地图与探索指引

当前小地图：

- 按地图长宽保持比例；
- 显示已探索道路与建筑；
- 显示当前玩家和朝向；
- 显示撤离点与距离；
- 显示可用下一阶段出口；
- 只显示附近已知未清空容器；
- 未知区块不提前显示建筑 / 容器；
- 跟随当前 ActivePlayer；
- 不额外创建地图摄像机。

M：

- 展开 / 收起地图。
- 不暂停战斗。

探索记录区分：

- 未进入；
- 已进入仍有物资；
- 已检索但未拿完；
- 已清空；
- 空置。

N：

- 探索目标；
- 返回撤离点；
- 下一阶段入口。

探索提示：

- 背包 >= 85% 时建议返程。
- 生命 < 35% 时提示医疗 / 撤离。
- 只提示，不自动移动、不自动撤离、不自动拿物品。

探索状态属于当前地图：

- 切角色不清除；
- 换地图重置；
- 不写长期存档。

---

# 20. 资源接入与 FX 管线

## PRTS / 本地拆包

原则：

    本地资源源
      ↓
    Editor Importer / Prefab Builder
      ↓
    项目内 Art / Resources / Generated Presentation
      ↓
    Character Presentation
      ↓
    Gameplay 事件

运行时 Gameplay 不依赖：

- PRTS URL；
- 中文资源路径；
- 原始 atlas/skel 文件位置；
- char_xxx model id；
- 外部文件系统绝对路径。

## Extracted Frame FX

当前标准：

    D:\Effect\EffectExtractor.exe
      ↓
    frames/*.png
      ↓
    ArknightsACT > Assets > Import Extracted Frame FX
      ↓
    Animation / Controller / Prefab
      ↓
    角色 FX Controller

通用导入器当前只接受：

f + 纯数字 + .png

这样避免 f0030_flat.png 等辅助图误混入动画。

不要回退。

## OHMS

OHMS Effect Importer 当前定位：

- 研究；
- 节点分析；
- 依赖反查。

不是运行时主链。

---

# 21. 当前已经确认存在的关键资产

2026-09-23 通过 FolderBridge 核对：

- Assets/_Game/Resources/UI/Shell/home_chernobog.jpg
- Assets/_Game/Resources/UI/Currency/lmd.png
- Assets/_Game/Resources/Config/FrostNovaTuningProfile.asset
- Assets/_Game/Resources/Config/SchwarzFxTuningProfile.asset
- Assets/_Game/Data/Operators/Operator_Chen.asset
- Assets/_Game/Data/Operators/Operator_Schwarz.asset
- Assets/_Game/Data/Operators/Operator_FrostNova.asset
- FrostNova Extracted FX 目录已有 Animation / Controller / Frame / Prefab 资产
- Schwarz Extracted FX 目录已有对应三皮肤主要 FX 资产

注意：

FrostNovaWinterSkill3Frames.asset 仍存在，但属于旧排查方案遗留，不代表当前 runtime 使用逐帧 Skill3 角色覆盖。

---

# 22. 当前验证状态

已有多轮专项验证：

### Source / FolderBridge

多个最近 Handoff 记录：

- source test / build smoke exit 0；
- issues 0；
- Bee 日志未发现新增 error CS。

但 FolderBridge smoke 不等于 Unity Editor 真实完整编译与运行。

### 城市生成器

MobileCityGenerationValidation 已覆盖：

- 384 个阶段计划；
- 多种固定种子；
- 三阶段实际几何；
- 门洞；
- 搜索站位；
- 导航；
- 墙体穿插；
- 容器种子；
- 250000 次珍稀度抽取；
- 15000 个建筑计划；
- 28 种容器；
- 地图投影；
- 设施支付 / 延迟伤害 / 超时 / 防重复奖励；
- 垂直路线 CharacterController 实际走通。

报告：

Logs/MobileCityGenerationValidation.txt

预览：

Logs/CityVisuals/

### 仍没有被自动验证替代的内容

以下必须靠正式 PrototypeRun Play Mode：

- 最新角色视觉；
- 动画手感；
- 输入冲突；
- 完整一局搜打撤；
- 4K / 16:9 UI；
- 正式场景性能；
- 长时间运行；
- 多角色切换后的状态一致性；
- 实际帧率；
- 多人网络行为。

---

# 23. P0：下一步必须先做

P0 不应该再横向开发新大系统。

## P0-1 完整 Unity Run 验收

用 Unity 6000.0.23f1 实际完成：

1. 主页显示；
2. 行动准备；
3. 开始行动；
4. Stage 1 探索；
5. 搜建筑 / 容器；
6. 获得普通物资与藏品；
7. 验证藏品立即生效；
8. 背包拖拽 / 整理 / 丢弃 / 重拾；
9. 使用城市设施；
10. 切换角色；
11. 打 Boss；
12. 进入 Stage 2，不结算背包；
13. 使用地图和导航；
14. 登高塔；
15. 继续 Stage 3 或选择返回；
16. 回真实撤离点；
17. 撤离；
18. 结算；
19. 仓库看到物资；
20. 出售后 LMD 增加；
21. 返回主页；
22. 开第二局；
23. 检查所有 Run 状态是否正确重置。

验收条件：

- Console 无 error CS / Missing Prefab / Skeleton / Material 错误。
- 不出现旧 Player 引用。
- 切角色后 HUD、相机、搜刮、设施、敌人碰撞关系正确。
- 跨 Stage 未撤离物资仍保持风险状态。
- 第二局不继承第一局脏状态。

## P0-2 FrostNova 收尾

按第 5.3 节逐项做实机检查。

尤其：

- Winter Slot1 = Skill_1。
- Winter Slot2 = Skill_3。
- Buff05。
- 普攻左右弹道。
- 动画完整播放。
- 空放冰环。

## P0-3 Schwarz 收尾

- S3 Hit。
- S3 Trail。
- shot 动作。
- 三皮肤快速回归。
- 视觉完成后准备退出 debugInfiniteDuration。

## P0-4 UI

检查：

- 主页背景；
- LMD 图标；
- HUD 文本遮挡；
- 头像 / 技能图标长宽比；
- 4K / 16:9；
- 小地图与 HUD 是否互相遮挡；
- 角色切换面板与搜刮快捷键冲突。

---

# 24. P1：角色与战斗底层正式化

## P1-1 Defense / Armor / Status

**2026-09-23：核心代码已完成，当前进入 Unity 真编译 / 回归测试 / 数值与表现验收阶段。**

当前 source of truth：

- P1_COMBAT_STATUS_HANDOFF_2026_09_23.md

已完成：

1. CombatStats：PhysicalDefense / ArtsResistance / MoveSpeedMultiplier / AttackSpeedMultiplier。
2. DamageContext：Penetration + DamageTags。
3. DamageSystem：Physical / Arts / True 正式统一结算。
4. DamageResult：Raw / PreMitigation / Mitigated / Final / Effective DEF / RES。
5. ICombatTargetStatModifier：团队 / 藏品目标属性 Aura。
6. ICombatStatModifier：目标自身属性修改。
7. IDamagePenetrationModifier：单次攻击穿透。
8. 数据驱动 StatusController：Stack / Tag / Reaction / Periodic / Resistance / Damage Modifier。
9. Action Block + Interrupt：玩家、普通敌人、宝箱怪与旧兼容 Brain 均已接入。
10. Cold / Freeze / Disarm / Burn / Slow / AttackSlow / DefenseDown / ResistanceDown / Fragile / Weaken。
11. Burn 已从 LevelUpgradeInventory 独立 Coroutine 迁到 Status。
12. 8 条我方 DEF 藏品已迁为 PhysicalDefensePercent。
13. 4 条敌方 DEF Down 藏品已迁为 EnemyPhysicalDefensePercent，不再伪装成 PhysicalDamagePercent。
14. Schwarz 破甲箭头已接：普通 20%、暮眼锐瞳 50%、战术的终结 100%；触发攻击 ×1.6、目标 DEF -20% 5s。
15. Damage / Status / Schwarz / Scavenging 迁移均已增加 EditMode 回归测试。

当前剩余：

- Unity Editor 真编译。
- 实际运行全部 EditMode tests。
- Play Mode 验证控制中断、Burn tick、Schwarz 破甲和交互封锁。
- 正式玩家 / 敌人 DEF / RES 数据。
- Boss StatusResistanceProfile 配置。
- 状态图标 / 冻结 / 灼烧等表现。
- Damage debug panel。

架构约束：

- 角色不自行复制 DEF / RES 公式。
- DEF Down、团队 Aura、Penetration 三层语义分离。
- 新异常优先做 Status Definition。
- 不再用 SourceId 字符串猜 BasicAttack / Skill / DOT，优先 DamageTags。

## P1-2 调试角色恢复正式规则

视觉稳定后：

- Schwarz 关闭 debugInfiniteDuration。
- FrostNova 退出无限 SP / 无 CD 调试。
- 为 FrostNova 建立正式 SP / 持续时间 / 数值。
- 把冬痕当前调参值作为基准，再决定原皮是否完全共用。

## P1-3 角色切换 UI 产品化

当前底层已完成，输入仍是 Prototype。

后续可以：

- 正式角色卡；
- 更清楚的冷却/不可切换原因；
- 更好的头像和皮肤信息；
- 手柄支持。

但不要做成完整“编队系统”，除非产品方向明确改变。

---

# 25. P2：搜打撤深化

## P2-1 背包

- 旋转；
- 物品交换；
- 更清楚的合法/非法格反馈；
- 批量操作体验；
- 完整键鼠/手柄适配。

## P2-2 开局携带 / 安全箱

当前仓库只能存、买、卖。

未来可做：

- 行动准备页取出物资；
- 开局携带；
- 消耗品；
- 装备栏；
- 安全箱；
- 死亡后的安全箱保留规则。

所有这些必须建立清楚的物品所有权状态，不能绕过 ScavengingInventory 生命周期。

## P2-3 仓库

后续：

- 排序；
- 更多筛选；
- 收藏 / 锁定；
- 批量购买；
- 仓库容量；
- 更完整详情面板。

## P2-4 风险等级统一

当前：

- 行动准备页有风险 I-V。
- 城市 Core / Industrial 等区域已有自己的敌人和物资倍率。

后续应设计一套统一倍率模型，避免：

“行动风险 x 区域风险 x Stage 风险 x 遭遇风险”

无控制叠乘导致数值爆炸。

建议把它们定义为不同层：

- Operation Risk：全局基础倍率。
- Stage：流程深度倍率。
- Zone：局部区域修正。
- Encounter：单场类型修正。

最终所有倍率由一个通用上下文计算。

## P2-5 临时据点

已有方向但未实现：

罗德岛临时据点可考虑：

- 回血；
- 有限制地提前结算部分普通物资。

不要设计为免费全背包保险。

---

# 26. P3：长期成长

当前长期层只有：

- LMD；
- 系统仓库；
- 指挥官等级占位。

后续可考虑：

- 基地；
- 永久背包起始能力；
- 安全箱；
- 搜索速度；
- 商店能力；
- 初始局内资源；
- 长期角色解锁 / 能力。

当前 Run XP / Level 是否保留仍未最终决定。

原则：

**暂时不要继续让新功能依赖旧 Run XP。**

等局内成长重构时再明确：

- 删除；
- 弱化；
- 或转成辅助资源。

---

# 27. P4：网络 / 多人预留

当前不要误认为已经有联机。

如果未来做网络：

优先把已有设施 Authority 边界扩展到：

- 搜刮容器；
- 世界掉落；
- 撤离；
- 敌人生成；
- 奖励；
- 角色切换；
- 生命与状态；
- Stage 推进。

服务端必须作为奖励和状态权威。

不要通过当前单机 MonoBehaviour 状态直接“同步一下”就视为完成联机。

---

# 28. 技术债与可清理项

## 可以在确认无依赖后清理

- FrostNovaFrameSequenceAsset。
- FrostNovaWinterSkill3Frames.asset 及旧 ForceReimportWinterSkill3Frames 路径。
- 已确定不再使用的逐帧角色 Skill3 覆盖导入逻辑。
- ChenTrainingDummySpawner 名称可泛化为 TrainingDummySpawner。
- 部分历史 serialized 字段可逐步重命名，但必须保留序列化兼容。
- PlayerSkillPointHUD 最终可在所有场景升级后移除兼容壳。

## 暂时不要删

- OHMS 研究工具与分析数据：仍可用于资源反查。
- PHASE_* 文档：作为历史实现参考。
- legacy_roguelike 图标：仍是 importer 离线兜底。
- 原客户端研究目录：不要挂运行时，但可作为资源比对。

---

# 29. 文档状态

当前推荐阅读顺序：

1. MASTER_HANDOFF_2026_09_23.md
2. CHARACTER_SYSTEM_REFACTOR_HANDOFF_2026_09_22.md
3. 当前要改角色的最新 Handoff
4. CITY_ZONES_TOWER_HANDOFF_2026_09_23.md
5. CITY_EXPLORATION_POLISH_HANDOFF_2026_09_23.md
6. SCAVENGING_RELIC_RUNTIME_HANDOFF_2026_09_20.md
7. 需要专项历史时再看其他文档

已明确失效，不应再作为实现依据：

- SCAVENGING_SEARCH_UI_HANDOFF_2026_09_20.md
- RogueRelics/ROGUE_RELIC_DB_HANDOFF.md
- RogueRelics/RogueRelic_EconomyDesign.md
- SCHWARZ_CHARACTER_HANDOFF_2026_09_21.md
- CHERNOB0G_CITY_MAP_HANDOFF.md
- CHERNOB0G_CITY_MAP_HANDOFF_NEXT.md
- CITY_EXPLORATION_HANDOFF_2026_09_20.md

由于当前 FolderBridge 本地写接口不能物理 delete，这些文件可能仍存在磁盘；存在不代表有效。

---

# 30. 接手硬规则

后续任何人接手，请遵守：

1. 最终 RogueRelicDatabase_IS1_CurrentPool.xlsx 是 118 件数据唯一人工源。
2. 不自动覆盖用户已经确认过的 Excel 数据。
3. 藏品拿到立即生效，但严格只属于当前 Run。
4. 普通物资只有真实撤离后才进入长期仓库。
5. 跨 Stage 不结算。
6. 源石锭只属于本 Run。
7. LMD 是长期货币。
8. 通用 Gameplay 不写具体角色特判。
9. 局内切换统一走 PlayableOperatorSwitchController。
10. 角色表现不能直接改真实伤害。
11. FX 通过事件驱动，不负责 Gameplay 结算。
12. PRTS / 外部素材只经过 Editor 导入进入本地项目，不做 Runtime 外部依赖。
13. 不恢复原客户端 AssetBundle FX 为正式运行时链路。
14. ExtractedFrameFxImporter 不重新接受辅助 PNG。
15. FrostNova Winter Slot1 = Skill_1，Slot2 = Skill_3。
16. FrostNova Skill3 不恢复旧逐帧角色覆盖。
17. FrostNova Buff05 是常驻背部 FX，Skill3 时隐藏。
18. Schwarz S2 不复用 S3 动作。
19. Schwarz S3 不在进入状态时自动开枪。
20. authored S3 Trail 存在时不叠程序 tracer。
21. 不恢复 PlayerSkillPointHUD.OnGUI 或重复 HUD。
22. 不在主页 / 仓库 / 结算显示源石锭。
23. 主页左侧只保留指挥官等级。
24. 仓库保持灰白不透明方向。
25. 普通房屋采用当前规则排列，不恢复整片错落旋转。
26. 不新增平行城市生成器。
27. 设施的 Authority 只是联机预留，不代表已经网络安全。
28. FolderBridge smoke 不能替代 Unity Editor Play Mode。
29. 未经用户明确要求，不 commit / push。
30. 大改前优先读本文件和当前专项 Handoff，不要按历史文档倒推当前实现。

---

# 31. 推荐实际执行顺序

如果下一位接手者没有新的明确用户任务，默认按以下顺序：

### 第一阶段：完成当前角色与整局稳定性

- FrostNova 实机验收。
- Schwarz 实机验收。
- 完整 Run 闭环。
- 多角色切换回归。
- HUD / 小地图 / 4K UI。

### 第二阶段：战斗底层正式化

核心架构已于 2026-09-23 完成，当前剩余：

- Unity 真编译与 EditMode / Play Mode 验证。
- Chen / Schwarz / FrostNova 正式 DEF / RES。
- 敌人 / Boss 正式 DEF / RES 与控制抗性。
- 状态视觉和 HUD。
- 角色正式技能数值。
- Schwarz 第二天赋 / 模组等后续角色数据。

### 第三阶段：搜打撤体验深化

- 背包旋转 / 交换。
- 开局携带。
- 安全箱。
- 仓库深化。
- 风险等级正式联动。

### 第四阶段：长期成长

- 临时据点。
- 长期基地。
- 永久升级。
- 重新决定 XP / Level 的定位。

### 第五阶段：只有方向确认后再做

- 真正多人联机。
- 新城市。
- 大规模增量/挂机层。
- 完整编队系统。

---

# 32. 一句话状态

截至 2026-09-23：

**ArknightsACT 已经具备可工作的 ACT 角色框架、三名角色体系、正式 HUD、118 件搜刮数据库、真实二维背包、真撤离、长期仓库与交易、连续切城生成、四区探索、小地图、城市设施和可登高地标；P1 战斗底层的 DEF / RES / True、穿透、目标属性 Aura、数据驱动 Status、控制中断、DOT、Schwarz 破甲与防御藏品迁移已落地。当前最需要做的是在 Unity 中完成真编译 + EditMode / Play Mode 验收，同时收尾 FrostNova / Schwarz 表现，再进入正式数值与搜打撤深化。**
