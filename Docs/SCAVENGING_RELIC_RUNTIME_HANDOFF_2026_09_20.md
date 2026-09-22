# ArknightsACT 搜刮 / 藏品 / 技力系统交接

更新时间：2026-09-20  
项目：`D:\WorkSpace\ArknightsACT`

> 本文件是当前搜刮、IS1 最终物品表、运行时藏品效果、技力机制的唯一主交接文档。  
> 旧的 `SCAVENGING_SEARCH_UI_HANDOFF_2026_09_20.md`、`RogueRelics/ROGUE_RELIC_DB_HANDOFF.md`、`RogueRelics/RogueRelic_EconomyDesign.md` 已被本文件取代。

---

## 1. 当前结论

当前链路已经从“自动预处理初稿”进入“人工确认后的正式运行时数据”阶段。

最终人工维护源：

`Docs/RogueRelics/RogueRelicDatabase_IS1_CurrentPool.xlsx`

用户已经确认：

- 保留哪些物品；
- 藏品 / 收集品分类；
- 稀有度；
- 龙门币基础价值；
- 物品大小；
- 游戏效果；
- 图标。

**后续不要再用旧预处理规则覆盖这些人工结果。**

当前运行时导入结果：

- 启用物品：**118**
- 藏品：**57**
- 收集品：**61**
- 本地图标：**118 / 118**
- 重复 ID：**0**
- 未解析藏品效果：**0**

当前尺寸分布：

| 尺寸 | 数量 |
|---|---:|
| 1x1 | 82 |
| 1x2 | 12 |
| 1x3 | 2 |
| 2x1 | 10 |
| 2x2 | 12 |

运行时导入报告：

`Logs/RogueRelicRuntimeImport.json`

运行时目录：

`Assets/_Game/Resources/ScavengingCatalog.json`

---

## 2. 数据源与自动导入

### 2.1 唯一人工源

正式运行时数据只认：

`Docs/RogueRelics/RogueRelicDatabase_IS1_CurrentPool.xlsx`

主要字段：

| Excel 列 | 用途 |
|---|---|
| 名称 | 游戏显示名 |
| 物品类型 | 藏品 / 收集品 |
| 游戏稀有度 | 普通 / 稀有 / 珍贵 / 绝世 |
| 基础龙门币价值 | CollectionValue |
| 物品大小 | 1x1 / 1x2 / 1x3 / 2x1 / 2x2 |
| IS1原编号 | 图标兜底 / 追溯 |
| 游戏效果 | 藏品运行时效果文本 |
| 官方描述 | 详情文本 |
| 启用 | 是否进入运行时池 |
| 内部ID | 稳定运行时 ID |

不要再把 `RogueRelicDatabase_IS1_CurrentPool.csv` 当成正式源。
CSV 和全版本数据库只保留用于追溯、重新抓取、数据研究。

### 2.2 Runtime importer

脚本：

`Assets/_Game/Editor/Tools/import_is1_runtime_catalog.py`

Editor 自动入口：

`Assets/_Game/Editor/RogueRelicRuntimeImportBootstrap.cs`

手动菜单：

`ArknightsACT > Scavenging > Import Curated IS1 Runtime Catalog`

手动 BAT：

`Docs/RogueRelics/ImportIS1RuntimeCatalog.bat`

Importer 做的事：

1. 直接读取最终 XLSX。
2. 只导入“启用=是”的现有行。
3. 不恢复被用户删除的行。
4. 不重新计算人工确认的价值、稀有度、大小。
5. 解析藏品的复合战斗效果。
6. 从 XLSX 内嵌图片提取运行时图标。
7. 生成：
   - `Assets/_Game/Resources/ScavengingCatalog.json`
   - `Assets/_Game/Resources/RogueRelics/RuntimeIcons/`
   - `Logs/RogueRelicRuntimeImport.json`
8. `AssetDatabase.Refresh()` 后游戏直接读取新目录。

Editor Bootstrap 会比较最终 XLSX / importer / Runtime JSON 的时间：
只有源文件更新时才自动重导，避免每次 reload 都重复做大量 IO。

---

## 3. 图标链路

### 当前正式运行时图标

目录：

`Assets/_Game/Resources/RogueRelics/RuntimeIcons/`

当前 XLSX 已有 **118 张内嵌图标**，运行时 importer 会按物品 ID 提取。

游戏 JSON 目前使用：

`RogueRelics/RuntimeIcons/<id>`

因此运行时不依赖 PRTS，不依赖 Excel 的外部 IMAGE URL。

### legacy_roguelike

目录：

`Assets/_Game/Resources/RogueRelics/Icons/legacy_roguelike/`

这里保留旧 IS1 编号缓存：

`is1_order_001.png ... is1_order_182.png`

它现在只作为 `import_is1_runtime_catalog.py` 的**离线兜底来源**：如果某一行未来缺少 XLSX 内嵌图标，Importer 仍可按 IS1 原编号从这里找到图。

日常运行时不直接读取这里，也不再自动联网补齐这套缓存。

2026-09-21 已退役并清理旧链路：

- `RogueRelicIconPatchBootstrap.cs` 的 Python 自动同步；
- `ScavengingPrtsIconSync.cs`；
- `ScavengingGameIconSync.cs`；
- `ScavengingIconImportPostprocessor.cs`；
- `Tools/sync_is1_legacy_icons.py`；
- `Tools/build_is1_icon_patch_bundle.py`；
- `Docs/RogueRelics/SyncIS1Icons.bat`；
- 旧 `Assets/_Game/Resources/Scavenging/Icons/` 8 图标目录及其本地提取缓存。

正式图标维护只走：

`最终 XLSX 内嵌图 -> Runtime Importer -> RogueRelics/RuntimeIcons/`

---

## 4. 运行时 Catalog

加载器：

`Assets/_Game/Scripts/Gameplay/Roguelite/Treasure/ScavengingCatalog.cs`

它会把 JSON 每一项动态创建为 `CollectibleDefinition`。

现在一个藏品不再只能有一条 `effect + value`。

`CollectibleDefinition` 已支持：

`CollectibleEffectModifier[] effects`

每条 modifier 有：

- `type`
- `value`
- `profession`
- `durationSeconds`

因此可以表示：

> 攻击力 +40%，防御力 -40%，攻击速度 +30

这种复合词条，而不是强行压成一个效果。

当前效果解析数量：

| Runtime effect | 条数 |
|---|---:|
| AllDamagePercent | 53 |
| IncomingDamagePercent | 12 |
| PhysicalDamagePercent | 8 |
| MaxHealthPercent | 8 |
| InitialSkillPoints | 7 |
| SkillPointRecoveryPerSecond | 8 |
| EnemyMaxHealthPercent | 5 |
| ArtsDamagePercent | 4 |
| IngotOnAcquire | 2 |
| TrueDamagePercent | 1 |

条数是 modifier 数，不是物品数；职业型效果会拆成多个 modifier。

---

## 5. 当前已真正接入战斗的藏品效果

核心：

`Assets/_Game/Scripts/Gameplay/Roguelite/Collectibles/CollectibleInventory.cs`

### 5.1 输出伤害

已经实际接入：

- `AllDamagePercent`
- `PhysicalDamagePercent`
- `ArtsDamagePercent`
- `TrueDamagePercent`

走：

`DamageSystem -> IDamageModifier`

### 5.2 最大生命

`MaxHealthPercent`

获得藏品后重新计算玩家 MaxHealth。

提高上限时会同步补上增加的生命值。

### 5.3 防御兼容映射

当前工程还没有独立 Defense / Armor 数值结算层。

因此临时映射：

- **我方防御 +X%**
  - 转为 `IncomingDamagePercent = -X%`
  - 即承伤降低。

- **敌方攻击力 -X%**
  - 同样转为玩家 `IncomingDamagePercent = -X%`。

- **敌方防御力 -X%**
  - 转为 `PhysicalDamagePercent = +X%`。
  - 即等效物理增伤。

这只是当前 ACT 的兼容实现。

**未来如果建立正式 Defense / Armor 系统，应该把这些映射迁回真实防御结算，不要叠加两套。**

### 5.4 敌方生命

`EnemyMaxHealthPercent`

`CollectibleInventory` 每 0.5 秒同步敌对 `CombatEntity.Health.MaxHealth`。

例如：

`迷迭香之拥`

当前会得到：

- EnemyMaxHealthPercent = -15%
- PhysicalDamagePercent = +30%

### 5.5 攻击速度

`AttackSpeedPercent`

已接：

`PlayerAttackController`

它影响普通攻击：

- startup
- total attack cycle

统一按：

`timingMultiplier = 1 / (1 + AttackSpeedBonus)`

处理。

当前最终 118 物品里没有生成该 modifier，但机制已经存在。

### 5.6 源石锭

`IngotOnAcquire`

接入：

`RogueliteRunState.Instance.AddIngots()`

当前最终表中有 2 条运行时藏品效果使用它。

注意（2026-09-21 已更新）：

**藏品现在在进入未撤离背包时立即进入 `CollectibleInventory` 并生效，不需要额外激活。**

- 搜刮容器、战斗奖励、尖刺箱、怪物箱的藏品奖励统一进入同一个未撤离背包入口。
- 未撤离藏品被主动丢弃时，会从 `CollectibleInventory` 移除对应层数并撤销持续型效果。
- 玩家死亡时，会先撤销所有未撤离藏品的持续型效果，再清空未撤离物资。
- `SecureHaul()` 现在只把未撤离风险变成已结算状态，不会再次 `Acquire()`，避免重复叠加。
- `InitialSkillPoints` / `IngotOnAcquire` 这类已经在拾取瞬间消费的一次性效果不会在丢弃时倒扣。

---

## 6. 技力系统

原技能机制已从“纯 cooldown 时间戳”升级为技力池。

接口：

`Assets/_Game/Scripts/Gameplay/Abilities/IPlayerSkill.cs`

关键字段：

- `SkillPoints`
- `SkillPointCost`
- `SkillPointRatio`
- `NaturalSkillPointPerSecond`
- `TickSkillPoints()`
- `GainSkillPoints()`

旧接口：

`ReduceCooldown(seconds)`

仍保留兼容。

现在它的语义是：

> 把旧的“减少 X 秒 CD”换算成等价技力恢复。

这样旧技能升级代码不会同时全部失效。

### 6.1 陈当前数值

#### 赤霄·拔刀

`ChenSkill1.cs`

- 消耗：20 技力
- 初始：10 技力
- 自然回复：1 / 秒

#### 赤霄·绝影

`ChenSkill2.cs`

- 消耗：30 技力
- 初始：20 技力
- 自然回复：1 / 秒

技能施放后直接扣除 SkillPointCost。

施法过程中不自然回复。

### 6.2 藏品技力效果

已经接入：

- `InitialSkillPoints`
- `SkillPointRecoveryPerSecond`
- `SkillPointRecoveryPercent`
- `SkillPointOnBasicHit`
- `SkillPointOnSkillCast`

最终表当前实际使用：

- InitialSkillPoints：7 条
- SkillPointRecoveryPerSecond：8 条

Importer 支持：

- `初始技力+6`
- `技力恢复+0.2/秒`
- `技力恢复+0.25/s`
- `所有技能每3.5秒回复1点技力`

“每 N 秒 +1”会换算成平均每秒恢复。

例如：

`皇家利口酒`

当前解析为：

- InitialSkillPoints +5
- SkillPointRecoveryPerSecond +0.666667

### 6.3 技力 UI

新增：

`Assets/_Game/Scripts/Gameplay/Abilities/PlayerSkillPointHUD.cs`

由：

`PlayerSkillController.Awake()`

自动挂到玩家。

当前是 OnGUI 实现，右下显示两条：

- 技能名
- 当前技力 / 技力上限
- 填充比例
- 满技力时改变表现

后续如果重做正式 HUD，可以替换表现层，不必再动技能接口。

---

## 7. 搜刮容器当前机制

### 7.1 容器

`SearchableContainer25D.cs`

容器类型：

- Residential
- Commercial
- Service
- Industrial
- Checkpoint

容器内部网格：

| 类型 | 网格 |
|---|---:|
| Residential | 10x7 |
| Service | 11x7 |
| Commercial | 11x7 |
| Industrial | 12x8 |
| Checkpoint | 12x8 |

实际生成物品件数：

- Residential / Service：5～7
- Commercial：6～8
- Industrial / Checkpoint：7～9

最大不会超过 10。

### 7.2 掉落随机

物品内容仍然是 deterministic weighted roll。

容器有固定 `RewardSeed`：

- 同一容器重新打开不会 reroll；
- 不消耗战斗随机流；
- 同一容器尽量不重复同 ID。

权重：

- 普通物资是主要掉落。
- 藏品明显更稀有。
- 当前职业可实际生效的藏品略高于不可用藏品。

### 7.3 容器内摆放

**已经取消随机格子起点。**

`TryPlace()` 现在固定：

> 从左到右 → 从上到下，找第一个能容纳该尺寸的位置。

即 row-major / first-fit。

物品 roll 仍然随机，但**位置不再随机乱摆**。

生成完以后 slots 还会按：

- GridY
- GridX

排序。

因此搜索顺序和显示顺序稳定。

### 7.4 搜索时间

按稀有度：

- 普通：1.70s
- 稀有：2.10s
- 珍贵：2.55s
- 绝世：3.00s

再加：

`slotIndex * 0.05s`

---

## 8. 搜刮交互

核心：

`ScavengingInventory25D.cs`

### 当前操作

- 靠近容器：显示候选容器。
- F：打开容器。
- 容器打开后：自动逐件搜索。
- F：再次关闭容器。
- B：打开 / 关闭背包。
- ESC：关闭当前搜刮 / 背包 UI。

打开搜刮或背包以后：

`GameplayInputBlocker`

会锁住正常战斗输入。

因此现在受击不会把搜刮 UI 强制关闭，只更新提示。

死亡会：

- 关闭界面；
- 撤销未撤离藏品的持续型运行时效果；
- 清掉全部未撤离物资。

暂停时搜索不会推进。

### 搜索状态

每件物品：

- Unsearched
- Searching
- Revealed
- Taken

关闭容器时：

- Revealed 状态保留；
- Taken 状态保留；
- 正在 Searching 的那一件会重置到 Unsearched，进度归零。

---

## 9. 背包与物品尺寸：真实二维占格（2026-09-21）

背包已经从“按件数容量”改成**真实二维格子容量**。

初始规格：

> **4 × 5 = 20 格**

物品继续使用最终 XLSX 已确认尺寸：

- 1x1
- 1x2
- 1x3
- 2x1
- 2x2

### 9.1 当前摆放规则

逻辑层和 UI 使用同一套规则：

> 从左到右 → 从上到下，row-major / first-fit。

- 每件物品必须找到与自身尺寸完全匹配的连续矩形空位；
- 不再存在“还有空格就一定能拿”的假设；
- 例如剩余 3 个分散空格时，1x1 可能还能放，但 2x2 会被拒绝；
- 新物品加入后按当前背包列表重新 first-fit；
- 丢弃物品后自动重新整理，现阶段不保留手工位置；
- **本轮不做拖拽和旋转**，物品严格按 XLSX 的原始宽高放置。

核心入口：

- `CanCarry()`：真实二维空间检测；
- `BuildBackpackLayout()`：生成与 UI 共用的实际布局；
- `TryAcceptReward()`：只有存在连续空间才允许进入未撤离背包。

### 9.2 局内源石锭扩容

背包扩容属于**本次 Run 内成长**，使用源石锭支付；跨 Stage 保留，下一次新 Run 从 4x5 重新开始。

| 等级 | 规格 | 总格数 | 本次升级价格 |
|---|---:|---:|---:|
| Lv0 | 4x5 | 20 | 初始 |
| Lv1 | 4x6 | 24 | 4 源石锭 |
| Lv2 | 5x6 | 30 | 7 源石锭 |
| Lv3 | 5x7 | 35 | 10 源石锭 |
| Lv4 | 6x7 | 42 | 14 源石锭 |
| Lv5 | 6x8 | 48 | 18 源石锭 |

全部升满总成本：**53 源石锭**。

设计意图：

- 第一次扩容价格低，允许玩家早期主动牺牲一部分商店资源换空间；
- 中期 7 / 10 锭开始与治疗、商店、技能成长形成明显竞争；
- 14 / 18 锭属于高投入，最大 6x8 不应成为每局默认必达状态；
- 20 格初始容量按当前 118 件物品平均占格约等于十余件物品，和原 12 件容量的实际携带量接近，但大件现在会真实挤占空间。

操作：

- B 打开背包；
- 点击底部扩容按钮，或按 `U`；
- UI 显示下一规格、升级价格和当前源石锭；
- 通过 `RogueliteRunState.TrySpendIngots()` 扣费。

---

## 10. 搜刮 UI

`ScavengingWindowUI.cs`

当前已经支持：

- 容器二维网格；
- 背包动态真实网格，初始 4x5、最高 6x8；
- 与逻辑层共用 first-fit 顺序摆放；
- 已占格 / 总格数显示；
- 源石锭局内扩容按钮与 U 快捷键；
- 1x3 等最终 Excel 尺寸；
- 藏品和收集品都显示本地图标；
- 详情面板；
- 物品名称；
- 稀有度；
- 龙门币价值；
- 官方描述；
- 藏品的“游戏效果”原文；
- 拾取；
- 指定背包物品丢弃；
- 搜索 spinner / scanning feedback。

旧版“收集品不显示图标”的限制已经删除。

---

## 11. 撤离 / 结算边界

未撤离物资：

`ScavengingInventory25D._pending`

调用：

`SecureHaul()`

之后：

### 藏品

藏品在**进入未撤离背包时**已经调用：

`CollectibleInventory.Acquire()`

并立即生效。

`SecureHaul()` 不再重复调用 `Acquire()`；它只代表该藏品已结算、后续不再因为普通丢弃/阶段风险从未撤离背包中损失。

当前统一入口：

`ScavengingInventory25D.TryAcceptReward()`

来源包括：

- 搜刮容器拾取；
- 普通/紧急/Boss 战斗的藏品选择奖励；
- 尖刺宝箱藏品奖励；
- 怪物箱额外藏品奖励；
- 商店购买藏品（旧商店链路也已收口到同一入口）。

`RogueliteRewardController` 已不再直接 `CollectibleInventory.Acquire()`，正式探索模式的奖励候选也改为读取当前 `ScavengingCatalog` 中的正式藏品，旧 prototype 8 件池不再作为正式探索奖励来源。

### 收集品

当前撤离成功后会：

- 加入 `_discoveredCommodities`；
- 把 `CollectionValue` 累加到 `_securedCollectionValue`；
- 同时由 `RogueliteGameFlowController` 将实际物品写入 `RogueliteMetaState` 的持久系统仓库。

`SecuredCollectionValue` 仍只是本局统计值；真正长期库存以 `RogueliteMetaState.Warehouse` 为准。

撤离物资**不会自动出售**。玩家要在“仓库 / 交易”页主动出售，才会增加龙门币。

当前还没有基地建设消耗指定收集品。

---

## 12. 当前经济 / Roguelite 已有基础

`RogueliteRunState.cs`

已有：

- 源石锭
- 路线深度
- StageIndex
- 战斗胜场
- 探索区块
- Emergency clear
- Lv1～Lv10
- 经验
- 升级事件

源石锭已经被：

- 战斗节点
- 路线
- 商店
- 宝箱
- 部分藏品

实际使用。

龙门币与长期仓库 / 买卖第一版已经接通；基地升级仍未实现。

---

## 13. 不要再使用的旧假设

以下旧文档中的说法已经失效：

- “只有 22 件藏品”
- “运行时还是手写 ScavengingCatalog”
- “IS1 当前池 190 件全部投放”
- “40 藏品 / 150 收集品”
- “还有约 91 个图标未补”
- “Runtime DB 还没接回搜刮系统”
- “背包只有 4 格”
- “每箱只有 3～4 件”
- “受击 / 移动自动关闭搜索”
- “收集品不显示图标”
- “技能力量仍使用普通 cooldownSeconds”
- “尺寸只支持到 2x2”
- “攻击速度 / 防御 / 技力尚未接入”

当前一律以本文件 + 当前源码为准。

---

## 14. 旧数据库工具的定位

### `RogueRelicDatabaseBootstrap.cs`

这是全版本数据库抓取 / 重建入口。

当前代码已经保护：

> 如果人工最终 `RogueRelicDatabase_IS1_CurrentPool.xlsx` 存在，不自动重建。

除非用户明确要求“重新抓全部 IS 数据”，否则不要运行：

`Rebuild Rogue Relic Data Schema`

避免把人工最终表重新生成。

### 旧图标同步工具

2026-09-21 已整体退役。原因是最终 XLSX 与 RuntimeIcons 已 118/118 完整，旧工具会在 Domain Reload 后启动 Python / 网络 / 本机客户端扫描，并造成 Unity `Hold on / Waiting for user code`。

不要恢复这些自动入口。

### 当前日常入口

真正应该用的是：

`RogueRelicRuntimeImportBootstrap.cs`

---

## 15. 验证状态

当前已确认：

- Unity 的 `#endif` 编译错误已修复。
- 最终 Runtime import 已实际运行。
- `Logs/RogueRelicRuntimeImport.json`：
  - rows_imported = 118
  - collectibles = 57
  - commodities = 61
  - embedded_icons_found = 118
  - runtime_icons_assigned = 118
  - unsupported_effect_count = 0
- Runtime JSON：
  - 118 条
  - 无重复 ID
  - 无空图标
  - 1x3 保留
- FolderBridge safe build 通过。

FolderBridge 的 `build` 是 source validation，不等于 Unity 真正完整 Build。

后续重大修改后仍建议在 Unity：

1. 等待脚本编译完成。
2. 打开 PrototypeRun。
3. 实际搜索多个类型容器。
4. 检查 1x3 / 2x2 排布。
5. 拿到技力藏品并撤离。
6. 检查技力数值和 HUD。
7. 检查伤害 / 生命效果。
8. 死亡确认未结算物资丢失。

---

## 16. 后续优先级

### 已完成：统一奖励生命周期（2026-09-21）

- 搜刮 / 战斗 / 尖刺箱 / 怪物箱 / 商店购买的藏品统一进入未撤离背包。
- 藏品拿到即生效，不需要安全屋激活。
- 丢弃 / 死亡会撤销未撤离藏品的持续型效果。
- 阶段结算不再重复叠加藏品。
- 正式探索奖励池读取 `ScavengingCatalog`，不再使用旧 prototype 藏品池直接发奖。

### 已完成：P0 真实格子背包基础（2026-09-21）

已经完成：

- 初始 4x5 真实占格；
- 物品真实尺寸参与容量；
- 连续空间检测；
- row-major / first-fit 自动整理；
- 丢弃后自动重排；
- 源石锭局内扩容；
- 最大 6x8；
- UI 与逻辑层共用同一布局结果。

暂未做、后续按需要再进入：

- 手工拖拽；
- 物品旋转；
- 固定位置记忆；
- 局外永久背包升级与本局临时扩容之间的叠加规则。

### 已完成：P1 局外仓库 / 龙门币第一版（2026-09-21）

已经建立：

- 成功撤离后的持久化仓库；
- 按物品 ID 堆叠；
- 售卖；
- 龙门币余额；
- 购买；
- 购买价 / 出售价差；
- 撤离结算页；
- 主页 / 仓库交易入口。

具体主流程见：

`Docs/MAIN_SHELL_EXTRACTION_HANDOFF_2026_09_21.md`

### P2：基地升级

建议继续使用之前确认的方向：

> 龙门币 + 指定收集品

候选：

- 背包扩容；
- 安全箱扩容；
- 初始源石锭；
- 商店能力；
- 搜索效率。

### P3：正式 Defense / Armor

如果后续角色成长需要防御数值：

1. 给战斗实体建立正式 Defense / Resistance 层。
2. DamageSystem 统一计算。
3. 把当前：
   - IncomingDamagePercent
   - Enemy defense -> PhysicalDamagePercent
   的兼容映射迁移。
4. 避免重复增益。

### P4：正式技能 HUD

当前 `PlayerSkillPointHUD` 是 OnGUI。

后续可以换为正式 UI：

- 两个技能槽；
- 技力环 / 条；
- 技能图标；
- Ready 高亮；
- 键位提示；
- 藏品回技反馈。

不要重新改 IPlayerSkill 技力接口。

---

## 17. 关键文件索引

### 最终数据

- `Docs/RogueRelics/RogueRelicDatabase_IS1_CurrentPool.xlsx`
- `Assets/_Game/Resources/ScavengingCatalog.json`
- `Assets/_Game/Resources/RogueRelics/RuntimeIcons/`
- `Logs/RogueRelicRuntimeImport.json`

### Runtime importer

- `Assets/_Game/Editor/Tools/import_is1_runtime_catalog.py`
- `Assets/_Game/Editor/RogueRelicRuntimeImportBootstrap.cs`
- `Docs/RogueRelics/ImportIS1RuntimeCatalog.bat`

### 搜刮

- `Assets/_Game/Scripts/Gameplay/Roguelite/Treasure/SearchableContainer25D.cs`
- `Assets/_Game/Scripts/Gameplay/Roguelite/Treasure/ScavengingInventory25D.cs`
- `Assets/_Game/Scripts/Gameplay/Roguelite/Treasure/ScavengingWindowUI.cs`
- `Assets/_Game/Scripts/Gameplay/Roguelite/Treasure/ScavengingCatalog.cs`

### 藏品

- `Assets/_Game/Scripts/Gameplay/Roguelite/Collectibles/CollectibleDefinition.cs`
- `Assets/_Game/Scripts/Gameplay/Roguelite/Collectibles/CollectibleInventory.cs`

### 技力 / 战斗

- `Assets/_Game/Scripts/Gameplay/Abilities/IPlayerSkill.cs`
- `Assets/_Game/Scripts/Gameplay/Abilities/PlayerSkillController.cs`
- `Assets/_Game/Scripts/Gameplay/Abilities/PlayerSkillPointHUD.cs`
- `Assets/_Game/Scripts/Gameplay/Characters/Chen/ChenSkill1.cs`
- `Assets/_Game/Scripts/Gameplay/Characters/Chen/ChenSkill2.cs`
- `Assets/_Game/Scripts/Gameplay/Combat/PlayerAttackController.cs`
- `Assets/_Game/Scripts/Combat/DamageSystem.cs`

### 当局 Roguelite

- `Assets/_Game/Scripts/Gameplay/Roguelite/Routing/RogueliteRunState.cs`

---

## 18. 接手规则

1. **不要自动重建最终 XLSX。**
2. 用户已经确认的大小 / 效果 / 类型 / 价值优先级最高。
3. 修改最终 XLSX 后，用 Runtime Importer 同步，不手改 118 条 JSON。
4. 新藏品需要多个效果时，继续使用 `effects[]`，不要退回单 effect。
5. 新效果枚举只追加，不重排旧枚举值。
6. 搜刮内容随机与位置排列是两个概念：
   - 内容可以 deterministic random；
   - 网格位置保持 row-major 顺序。
7. 不要把视觉网格误当成真实格子容量。
8. 不要把 `SecuredCollectionValue` 误当成已经完成的长期龙门币系统。
9. 不要 commit / push，除非用户明确要求。
10. 大改后需要 Unity 真编译 / Play Mode 验证，FolderBridge build 只能做源码级 smoke validation。

---

## 19. 2026-09-21 已确认设计方向 / 暂存待办

以下方向已经确认或提出，但**本轮先记录，不提前改动对应系统**：

### 19.1 藏品生命周期

已确认：

> 藏品拿到就能用，不设计“到安全屋后再激活”的额外步骤。

未撤离只代表“仍有损失风险”，不代表“尚未生效”。

### 19.2 经验值系统

当前 `RogueliteRunState` 仍保留：

- Lv1～Lv10；
- 经验；
- `LevelIncreased`；
- 现有升级奖励链路。

用户正在考虑**是否整体去掉经验值 / 等级系统**。本轮不删除，等轮到局内成长系统重构时再决定：

- 完全删除经验等级；或
- 只保留技能/事件驱动成长；或
- 将经验降级为辅助成长资源。

在正式决定前，不继续扩大依赖经验等级的新功能。

本次背包扩容直接消费源石锭，不依赖等级/经验系统。

### 19.3 罗德岛临时据点

后续可把安全屋/罗德岛临时据点设计为世界 POI，而不是旧路线选择 UI。

优先考虑的功能：

- 回血 / 恢复；
- **提前结算指定容量的物资**，而不是全背包无条件保险；
- 具体容量、费用、次数与是否允许结算藏品，等轮到据点/撤离系统时再设计。

本轮不实现临时据点，不修改经验系统。
