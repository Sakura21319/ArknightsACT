# ArknightsACT 当前项目总交接文档

更新时间：2026-09-21  
项目：`D:\WorkSpace\ArknightsACT`

> 本文件作为当前 **搜打撤主循环 / 肉鸽成长 / 藏品 / 背包 / 真撤离 / 主页 / 结算 / 系统仓库 / 买卖 / UI** 的统一交接入口。
>
> 后续接手优先阅读本文件，再按需要查：
>
> - `Docs/SCAVENGING_RELIC_RUNTIME_HANDOFF_2026_09_20.md`
> - `Docs/MAIN_SHELL_EXTRACTION_HANDOFF_2026_09_21.md`
> - `Docs/HUD_COMBAT_HANDOFF_2026_09_21.md`（正式战斗 HUD / Ready / 本地陈素材 / P3 战斗底层接续）
> - `Docs/SCHWARZ_CHARACTER_HANDOFF_2026_09_21.md`（黑：原版 + Snow + Striker，小人 / HUD / S1+S3 / 皮肤特效接入）
>
> 本文件若与旧文档有冲突，以 **本文件 + 当前源码** 为准。

---

## 1. 当前游戏方向

当前核心方向已经明确为：

> **搜打撤骨架 + 肉鸽局内构筑 + 明日方舟藏品体系**

不是旧的纯路线节点 Roguelite。

主要循环：

```
主页
  ↓
开始行动
  ↓
探索城市 / 搜刮容器 / 战斗 / 开箱
  ↓
藏品立即生效 + 物资进入未撤离背包
  ↓
继续深入下一 Stage 或返回真实撤离点
  ↓
主动撤离
  ↓
结算
  ↓
物资进入系统仓库
  ↓
仓库 / 交易中主动出售
  ↓
获得长期龙门币
  ↓
返回主页
```

失败：

```
行动中死亡
  ↓
全部未撤离物资丢失
  ↓
未撤离藏品持续效果撤销
  ↓
失败结算
  ↓
返回主页
```

---

# 2. 最终藏品 / 物资数据

唯一正式人工维护源：

`Docs/RogueRelics/RogueRelicDatabase_IS1_CurrentPool.xlsx`

用户已经人工确认：

- 保留物品；
- 藏品 / 收集品分类；
- 稀有度；
- 基础龙门币价值；
- 物品尺寸；
- 游戏效果；
- 图标。

当前 Runtime 导入结果：

- 总物品：**118**
- 藏品：**57**
- 收集品：**61**
- Runtime 图标：**118 / 118**
- 重复 ID：**0**
- 未解析效果：**0**

尺寸：

| 尺寸 | 数量 |
|---|---:|
| 1x1 | 82 |
| 1x2 | 12 |
| 1x3 | 2 |
| 2x1 | 10 |
| 2x2 | 12 |

正式运行时：

- `Assets/_Game/Resources/ScavengingCatalog.json`
- `Assets/_Game/Resources/RogueRelics/RuntimeIcons/`
- `Logs/RogueRelicRuntimeImport.json`

Importer：

- `Assets/_Game/Editor/Tools/import_is1_runtime_catalog.py`
- `Assets/_Game/Editor/RogueRelicRuntimeImportBootstrap.cs`

原则：

> **不要再用旧规则自动覆盖最终 XLSX。**

修改 XLSX 后使用：

`ArknightsACT > Scavenging > Import Curated IS1 Runtime Catalog`

---

# 3. 已退役的旧图标链路

此前会导致 Unity 出现：

`Hold on / Waiting for user code in Game.Editor.dll`

的旧自动同步已经退役。

不要恢复：

- 旧 RogueRelicIconPatch Python 自动同步；
- 旧 PRTS 批量补 Excel 图标；
- 旧本机客户端解包 8 个 prototype 图标；
- 旧 `Resources/Scavenging/Icons` 原型链路。

仍保留：

`Assets/_Game/Resources/RogueRelics/Icons/legacy_roguelike/`

用途仅为 Runtime Importer 在 XLSX 内嵌图偶发缺失时的**离线兜底**。

正常 Runtime 不直接读取它。

---

# 4. 藏品生命周期 —— 已完成

当前所有实体藏品来源已经统一：

- 搜刮容器；
- 普通战斗奖励；
- 紧急战斗奖励；
- Boss 奖励；
- 尖刺箱；
- 怪物箱；
- 商店藏品。

统一入口：

`ScavengingInventory25D.TryAcceptReward()`

当前规则：

> **藏品拿到就立即能用。**

流程：

```
奖励来源
  ↓
TryAcceptReward()
  ↓
进入未撤离背包
  ↓
CollectibleInventory.Acquire()
  ↓
效果立即生效
```

主动丢弃：

- 从未撤离背包删除；
- 调用 `CollectibleInventory.Release()`；
- 撤销持续型效果。

死亡：

- 撤销全部未撤离藏品持续型效果；
- 清空全部未撤离物资。

成功撤离：

- 普通物资进入系统仓库；
- 藏品不带出局、不进入系统仓库；
- Run 结束时统一清除藏品持续效果。

一次性效果：

- `InitialSkillPoints`
- `IngotOnAcquire`

已经在拾取时消费，丢弃时**不会倒扣**。

---

# 5. 藏品战斗效果 —— 已接入

核心：

`CollectibleInventory.cs`

已接：

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

当前 Defense / Armor 仍没有完整独立层。

临时兼容：

- 我方 DEF +X% → 承伤 -X%
- 敌方 ATK -X% → 承伤 -X%
- 敌方 DEF -X% → 物理增伤 +X%

未来如果实现正式 Armor / Resistance，需迁移这部分，避免双重计算。

---

# 6. 技力系统 —— 已完成基础迁移

旧技能 cooldown 已迁移为 SP / 技力池。

接口：

`IPlayerSkill.cs`

陈：

### 赤霄·拔刀

- Cost 20
- Initial 10
- Natural Recovery 1/s

### 赤霄·绝影

黑已新增为第二个可构建角色；当前黑使用 Slot 1 `暮眼锐瞳`（原版 S2）+ Slot 2 `战术的终结`（原版 S3）。黑的基础普攻使用明显长于近战的狙击判定，S3 再进一步延长射程并降低攻击频率。原版 / Snow / Striker 三套 Spine、头像与皮肤特效均从 `D:\\Ark\\_Unpacked\\shwaz` 本地接入。详见 `Docs/SCHWARZ_CHARACTER_HANDOFF_2026_09_21.md`。

- Cost 30
- Initial 20
- Natural Recovery 1/s

施法时不自然回技力。

旧：

`ReduceCooldown(seconds)`

仍保留兼容，语义改为等价技力恢复。

正式战斗 HUD 第一版已接入：

- `GameplayHUDController.cs`：正式 UGUI HP + 双技能 HUD；
- `SkillReadyWorldIndicator.cs`：角色头顶 Ready 标识；
- `PlayerSkillPointHUD.cs`：仅保留空壳兼容旧场景，原 `OnGUI()` 已删除。

当前规则：

- HP / SP 数字只按整数刷新与显示；
- SP 未真正充满时使用向下取整，避免出现“显示满值但实际未 Ready”；
- 技能行不显示 `READY` 文本，Ready 只通过角色头顶原版战斗 UI 标识反馈；
- Ready 资源由 Unity Editor 从 `D:\\Ark\\_Unpacked\\chen\\ui_assets\\battle_skill_ready` 本地导入；
- Ready 静态标记精确使用 `sprite_skill_ready__-3542339109505237889.png`（72×72）+ `sprite_skill_bg` 动效层；
- 动效按已解析原参数还原：47→100 尺寸、0.5 秒、Alpha 1→0、循环；
- 1 个技能 Ready 使用普通黄色态；2 个技能同时 Ready 使用增强橙红态；
- 正式 HUD 已缩小并改成半透明轻量布局；
- 技能行不再显示 `READY` 文本，Ready 只保留角色头顶提示；
- 头像读取 `Resources/UI/HUD/chen_avatar`，由 `D:\\Ark\\_Unpacked\\chen\\ui_assets\\avatars\\char_010_chen.png` 精确导入；
- 技能图标读取 `Resources/UI/Skills/chen_badao` 与 `Resources/UI/Skills/chen_jueying`；
- Unity Editor 会从 `D:\\Ark\\_Unpacked\\chen\\icons` 自动识别陈官方 S2（赤霄·拔刀）与 S3（赤霄·绝影）图标并复制到上述 Resources 路径；
- Ready Runtime 读取 `Resources/UI/HUD/BattleSkillReady/sprite_skill_ready` 与 `sprite_skill_bg`；
- 全流程只读本地拆包素材，不联网、不再使用 FX 帧占位。

---

# 7. 搜刮容器 —— 已完成基础系统

核心：

`SearchableContainer25D.cs`

容器类型：

- Residential
- Commercial
- Service
- Industrial
- Checkpoint

生成物品约：

- Residential / Service：5～7
- Commercial：6～8
- Industrial / Checkpoint：7～9

内容：

- deterministic weighted roll
- 固定 RewardSeed
- 重开不 reroll
- 尽量不重复同 ID

容器内部摆放：

> row-major / first-fit

不再随机乱摆。

搜索状态：

- Unsearched
- Searching
- Revealed
- Taken

关闭窗口后：

- Revealed 保留；
- Taken 保留；
- 正在搜索的一件重置。

---

# 8. 真实二维背包 —— 已完成第一版

核心：

`ScavengingInventory25D.cs`

初始：

> **4 × 5 = 20 格**

真实尺寸参与容量：

- 1x1
- 1x2
- 1x3
- 2x1
- 2x2

不是按物品件数判满。

摆放：

- 新拾取物默认使用 row-major / first-fit 找空位；
- 拾取后位置记录在 `ScavengingInventory25D` 数据层；
- 支持鼠标拖拽到合法格位；
- 支持 `[ R ]` 自动整理；
- 拖出背包网格或点击“丢弃到地面”会生成真实地面掉落物；
- 靠近地面掉落物按 `F` 可以重新拾取；
- 藏品重新拾取会恢复持续效果，但不会重复触发一次性源石锭 / 初始技力效果。

仍要求物品有完整连续矩形空间。

暂未做：

- 旋转；
- 交换两个占位冲突物品的快捷操作。

---

# 9. 局内背包扩容 —— 已完成

使用：

> **源石锭**

源石锭定位：

> 仅本 Run 使用的 Roguelite 资源。

背包扩容：

| Level | 尺寸 | 总格数 | 价格 |
|---|---:|---:|---:|
| Lv0 | 4x5 | 20 | 初始 |
| Lv1 | 4x6 | 24 | 4 |
| Lv2 | 5x6 | 30 | 7 |
| Lv3 | 5x7 | 35 | 10 |
| Lv4 | 6x7 | 42 | 14 |
| Lv5 | 6x8 | 48 | 18 |

升满总成本：

> **53 源石锭**

操作：

- B 打开背包；
- U 扩容；
- 或点击扩容按钮。

跨 Stage 保留扩容。

新 Run 重置回 4x5。

---

# 10. 真正撤离 —— 已完成第一版

核心：

`RogueliteStageRuntimeController.cs`

每个 Stage 起始区域附近都会生成：

`ExtractionPoint`

靠近：

> E → 打开撤离决策

玩家可以：

- 继续探索；
- 立即撤离。

最关键改动：

> **Stage 1 → 2 / Stage 2 → 3 不再自动 SecureHaul。**

因此跨 Stage 只是继续深入。

背包物资始终保持风险状态。

最终 Boss：

> 不再自动 FinishRun。

打完后仍要回撤离点。

---

# 11. 主流程状态机 —— 已完成

核心：

`RogueliteGameFlowController.cs`

状态：

- Home
- Running
- ExtractionDecision
- Settlement
- WarehouseTrade

流程：

```
Home
 ↓
Running
 ↓
ExtractionDecision
 ↓
Settlement
 ↓
Home
```

仓库：

```
Home
 ↓
WarehouseTrade
 ↓
Home
```

非 Running：

- GameplayInputBlocker 锁输入；
- GameplayPauseService 暂停局内模拟；
- 局内 HUD 隐藏。

---

# 12. 新 Run 重置 —— 已完成

点击“开始行动”会重置：

- Stage；
- 源石锭；
- 路线统计；
- 经验 / 等级；
- 未撤离物资；
- 背包回 4x5；
- 背包扩容等级；
- 藏品持续效果；
- LevelUpgradeInventory；
- CharacterSkillUpgradeInventory；
- 当前角色的技能运行时强化；
- 玩家生命；
- 当前角色实现 `IPlayerRunResettable` 的表现状态。

避免第二局继承第一局脏状态。

---

# 13. 系统仓库 —— 已完成第一版

核心：

`RogueliteMetaState.cs`

当前长期保存：

- 龙门币；
- 指挥官等级占位值；
- 系统仓库物品 ID → 数量。

当前存储：

`PlayerPrefs / ArknightsACT.MetaState.v1`

开发测试初始龙门币：

> 120000

成功撤离：

1. 快照未撤离背包中的普通物资；
2. 仅普通物资写入系统仓库；
3. 藏品不入库，随本 Run 结束；
4. SecureHaul；
5. 清理 Run 藏品效果；
6. 打开结算页。

重要：

> **撤离物资不会自动卖掉。**

必须在仓库 / 交易主动出售。

---

# 14. 仓库 / 交易 —— 已完成第一版

已经合并为同一个模块。

支持：

- 默认进入“我的物品”，不再默认展示商店；
- 我的物品 / 商店 Tab；
- 全部 / 低价值筛选；
- 名称 / ID / 描述搜索；
- 物品网格；
- 真实 RuntimeIcons；
- 持有数量；
- 仓库总价值统计；
- 详情；
- 单价；
- 单件购买；
- 单件出售；
- 批量出售当前搜索 / 筛选结果的全部持有数量；
- 一键出售低价值物资，当前阈值为单价 `<= 2000` 龙门币。

价格：

### 出售

`CollectionValue`

最低 1。

### 购买

`SellPrice × 1.25`

按 10 向上取整。

当前没有：

- 批量购买；
- 排序；
- 收藏 / 锁定；
- 动态市场；
- 每日商店库存；
- 仓库容量；
- 从仓库带物资入局。

---

# 15. 结算画面 —— 已完成第一版

成功结算：

- 成功撤离；
- 回收物资；
- 回收件数；
- 回收总价值；
- 到达 Stage；
- 探索区块；
- 战斗记录；
- 行动时间。

按钮：

- 返回主页；
- 前往仓库 / 交易。

失败：

- 不入库；
- 显示行动失败；
- 未撤离物资已遗失。

---

# 16. 主页 / UI 方向 —— 已确认并落地 UGUI

选定方向：

> **方案 B · 精简战术卡片**

核心：

`RogueliteShellUI.cs`

旧主界面：

`RogueliteGameFlowController.OnGUI()`

现在只作为兜底。

当 UGUI 正常创建后，不再显示旧界面。

## 主页规则

左侧：

> **只保留左下角“指挥官等级”**

不要新增：

- 公告；
- 邮件；
- 活动入口；
- 其他左侧信息栏。

右侧：

- 开始行动；
- 仓库 / 交易；
- 编队（预留，不实现）；
- 任务（预留，不实现）。

明确删除：

- 干员；
- 模组；
- 采购中心；
- 情报；

等底部横条。

不要恢复。

---

# 17. 主页背景 —— 最新状态

用户已经选定一张新生成的：

> **干净、低噪点、灰蓝工业切城 3D 背景**

要求：

- 不使用 Gameplay 场景作为主页背景；
- 不要过度废墟；
- 不要太脏；
- 不要高噪点；
- 不要重雾；
- 留足 UI 可读区域。

目标 Runtime 资源：

`Assets/_Game/Resources/UI/Shell/home_chernobog.jpg`

Runtime：

`RogueliteShellUI`

读取：

`Resources.Load<Sprite>("UI/Shell/home_chernobog")`

当前 Editor 转换脚本：

`Assets/_Game/Editor/HomeShellAssetBootstrap.cs`

临时 base64 源：

`Assets/_Game/Editor/Temp/home_chernobog_800.jpg.b64`

该脚本会：

1. base64 → JPG；
2. 导入为 Sprite；
3. 放到 Resources；
4. 成功后删除临时 b64。

### 当前需要注意

最近一次 FolderBridge 查看时：

`Assets/_Game/Resources/UI/Shell/home_chernobog.jpg`

**尚未确认已经在磁盘生成成功。**

因此下一位接手者应优先在 Unity Domain Reload 后检查：

- 文件是否生成；
- Sprite Import 是否成功；
- 首页是否实际显示新背景；
- Gameplay 场景是否完全不再透出。

这属于当前 UI 第一优先级验证项。

---

# 18. 龙门币图标 —— 最新状态

用户要求：

> 龙门币必须使用 PRTS 素材，不要继续用字母 L 占位。

目标本地资源：

`Assets/_Game/Resources/UI/Currency/lmd.png`

Editor：

`Assets/_Game/Editor/PrtsCurrencyIconBootstrap.cs`

当前下载源优先级：

1. `media.prts.wiki` 原图；
2. PRTS Special:Redirect/file 备用。

逻辑：

- 只有本地 Sprite 缺失时自动下载；
- 异步，不阻塞 Unity；
- 下载后导入 Sprite；
- Runtime 完全读取本地；
- Runtime 不联网。

菜单：

`ArknightsACT > UI > Refresh PRTS LMD Icon`

### 当前需要注意

最近一次 FolderBridge 查看：

`Assets/_Game/Resources/UI/Currency/`

只看到目录 meta，**尚未确认 lmd.png 已经真正落盘并导入成功**。

因此下一步优先：

1. Unity reload；
2. 检查 Console 是否有：
   `[ArknightsACT/UI] PRTS Longmen Coin icon imported`
3. 确认：
   `Assets/_Game/Resources/UI/Currency/lmd.png`
4. 如果仍失败：
   - 检查 PRTS 返回；
   - 可手动执行 Refresh 菜单；
   - 必要时把 PRTS 图直接下载成本地 Asset，不再依赖自动 bootstrap。

不要长期保留占位字母 L 作为最终表现。

---

# 19. 源石锭 UI 规则 —— 最新确认

源石锭：

> **局内货币**

因此不要在局外界面展示。

当前 UGUI 已按规则设置：

### 不显示源石锭

- Home；
- Warehouse / Trade；
- Settlement。

### 显示源石锭

- Running；
- Backpack 扩容；
- Extraction Decision 等仍属于当前 Run 的界面。

不要再把源石锭放回主页顶栏。

---

# 20. 仓库 UI 视觉规则

用户最新要求：

> 仓库不要背景图，纯灰白即可。

当前：

- 全屏不透明；
- 灰白主色；
- 左侧分类；
- 中间网格；
- 右侧详情 / 购买 / 出售；
- 使用正式 RuntimeIcons；
- 龙门币价格区域会使用本地 LMD 图标。

不要使用：

- 城市场景背景；
- 黑色大面积废墟图；
- 高噪点背景纹理。

---

# 21. 经验值 / 等级系统 —— 暂缓决定

当前 RunState 仍然有：

- Lv1～Lv10；
- XP；
- LevelIncreased；
- 原升级奖励链。

用户提出：

> 可能去掉经验值系统。

目前决定：

**先不要删。**

同时：

> 不继续扩大新功能对经验 / 等级的依赖。

等轮到“局内成长重构”时再决定：

1. 完全删除 XP / Level；
2. 只保留技能 / 事件成长；
3. XP 降级成辅助资源。

注意：

主页左下角的：

> 指挥官等级

是**局外等级占位**，与当前 Run 内经验等级不是一个系统。

---

# 22. 罗德岛临时据点 —— 已记录，未实现

未来世界 POI。

用户确认的候选功能：

- 回血 / 恢复；
- 提前结算指定容量的物资。

重点：

> 不应该无条件全背包保险。

后续需要设计：

- 单次可结算格数；
- 消耗；
- 次数；
- 藏品能否部分结算；
- 是否与正式撤离点共用规则。

当前先不要做。

---

# 23. 目前最重要的待做事项

## P0：验证并完成主页视觉资源

第一优先级。

检查：

- 新切城背景是否真的生成到 Resources；
- 主页是否显示正确；
- 是否完全不再显示 Gameplay 场景；
- LMD PRTS 图标是否真正落盘；
- 是否还存在空白图片；
- 主页 Source Ingot 是否已经完全消失；
- 4K / 16:9 下布局是否正常。

如果 Asset bootstrap 仍不稳定：

> 直接把背景 JPG 和 LMD PNG 固化成正式 Asset，不再依赖临时自动导入。

---

## P0：Unity Editor 真编译 / Play Mode 全流程验证

FolderBridge：

- build：issues=0
- test：issues=0

但这是源码 smoke validation。

必须实际在 Unity：

1. 打开 PrototypeRun；
2. 看主页；
3. 开始行动；
4. 搜刮；
5. 拿藏品；
6. 扩容；
7. 跨 Stage；
8. 确认不自动结算；
9. 回撤离点；
10. 撤离；
11. 查看结算；
12. 进入仓库；
13. 出售；
14. 龙门币增加；
15. 返回主页；
16. 再开第二局；
17. 检查局内数值是否完全重置。

---

## P1：正式背包交互 —— 第一版已完成

已完成：

- 拖拽；
- 手动位置；
- Inventory 数据层保存二维位置；
- 自动整理；
- 选中反馈；
- 丢弃到地面；
- 地面物品重新拾取；
- 藏品丢弃 / 重拾时正确撤销与恢复持续效果；
- 防止重拾藏品重复获得一次性奖励。

后续：

- 旋转；
- 占位冲突时的交换 / 替换交互；
- 更完整的拖拽落点高亮。

---

## P1：仓库深化 —— 本轮已完成主要出售侧功能

已完成：

- 默认展示“我的物品”；
- 搜索；
- 低价值筛选；
- 仓库总价值统计；
- 批量出售当前结果；
- 一键出售低价值物资。

后续：

- 批量购买；
- 排序；
- 更多筛选维度；
- 收藏 / 锁定；
- 仓库容量。

---

## P1：从仓库携带物资入局

当前仓库只能存 / 买 / 卖。

没有：

- 开局携带；
- 装备栏；
- 安全箱；
- 消耗品；
- 入局准备页。

后续如果做完整搜打撤，这是重要系统。

---

## P1：正式行动准备页 —— 第一版已完成

主页点击“开始行动”现在先进入正式行动准备页，再由玩家确认开始 Run。

当前只实现用户确认的两个模块：

- 切城地区选择：切城外围 / 切城核心区 / 切城工业区 / 切城南部废墟；
- 风险等级 I–V：低风险 / 标准 / 高风险 / 危险 / 极限。

规则：

- 当前地图范围只允许切城，不加入其他城市；
- 页面采用独立不透明 UGUI，Gameplay 摄像机不作为页面背景；
- 所选地区与风险会保留到当前行动，并在撤离页显示；
- 风险等级当前先作为选择与展示数据，敌人强度 / 战利品倍率的真实数值联动后续再接。

后续再考虑：

- 推荐等级；
- 当前携带物资；
- 当前仓库取出物资；
- 安全箱。

---

## P2：临时据点

实现此前记录：

- 回血；
- 指定容量部分结算。

---

## P2：长期经济 / 基地

长期资源建议：

### 龙门币

长期通用货币。

来源：

- 撤离物资出售。

用途：

- 仓库；
- 基地；
- 永久升级；
- 局外商店。

### 源石锭

坚持：

> 本局货币。

不要做成长期资产。

基地候选：

- 初始背包；
- 安全箱；
- 搜索速度；
- 商店能力；
- 初始局内资源。

---

## P2：正式主 HUD —— 1–10 第一版已完成

已完成：

- 正式 `GameplayHUDController` UGUI 框架；
- HP；
- 技能图标槽位与本地图标加载；
- 双技能 SP 技力条：未满绿色，真正满技后黄色；
- Ready 状态逻辑；
- 技能行不显示 READY 字样；
- 头顶 Ready 标识已按原版手动技能 Ready 结构还原：`sprite_skill_ready` + `sprite_skill_bg` 0.5 秒扩散淡出循环；单技能黄色、双技能增强橙红；
- HP / SP 整数显示与整数边界刷新；
- 当前实际快捷键文字已显示：Skill1 `L`，Skill2 `I / RMB`。

本轮继续完成：

- 藏品回技反馈：普攻 / 技能触发藏品回技时显示短暂 `+N SP` 提示；
- 当前背包提示：`B 背包 已用/总格`，80% 以上黄色、满包红色；
- 当前源石锭：并入正式 HUD，仅 Running 显示；
- `RogueliteProgressHUD` 的旧 Run Level / 源石锭 `OnGUI` 已退役；
- `ScavengingWindowUI` 右下角旧常驻背包状态条已移除，仅保留临时 `F` 检索 / 拾取提示；
- `PlayerSkillPointHUD` 继续保持无 `OnGUI` 的兼容空壳。

素材状态：技能图标、陈头像与 Ready 原素材都从用户本地 `D:\\Ark\\_Unpacked\\chen` 精确导入；Unity 脚本重编译后执行。HUD 已再次收窄：HP 独立顶行，两条技能行与底部背包/源石锭行逐级缩短。

---

## P3：Defense / Armor 正式化

当前还是兼容映射。

未来：

- Physical Defense；
- Arts Resistance；
- Armor penetration；
- DamageSystem 统一结算；
- 替换临时 IncomingDamage 映射。

---

# 24. 当前关键文件

## 总流程

- `Assets/_Game/Scripts/Gameplay/Roguelite/Routing/RogueliteGameFlowController.cs`
- `Assets/_Game/Scripts/Gameplay/Roguelite/Routing/RogueliteMetaState.cs`
- `Assets/_Game/Scripts/Gameplay/Roguelite/Routing/RogueliteShellUI.cs`
- `Assets/_Game/Scripts/Gameplay/Roguelite/Routing/RogueliteStageRuntimeController.cs`
- `Assets/_Game/Scripts/Gameplay/Roguelite/Routing/RogueliteRunState.cs`

## 主页资源

- `Assets/_Game/Editor/HomeShellAssetBootstrap.cs`
- `Assets/_Game/Editor/Temp/home_chernobog_800.jpg.b64`
- 目标：`Assets/_Game/Resources/UI/Shell/home_chernobog.jpg`

## 龙门币

- `Assets/_Game/Editor/PrtsCurrencyIconBootstrap.cs`
- 目标：`Assets/_Game/Resources/UI/Currency/lmd.png`

## 搜刮 / 背包

- `Assets/_Game/Scripts/Gameplay/Roguelite/Treasure/SearchableContainer25D.cs`
- `Assets/_Game/Scripts/Gameplay/Roguelite/Treasure/ScavengingInventory25D.cs`
- `Assets/_Game/Scripts/Gameplay/Roguelite/Treasure/ScavengingWindowUI.cs`
- `Assets/_Game/Scripts/Gameplay/Roguelite/Treasure/WorldSalvagePickup25D.cs`
- `Assets/_Game/Scripts/Gameplay/Roguelite/Treasure/ScavengingCatalog.cs`

## 藏品

- `Assets/_Game/Scripts/Gameplay/Roguelite/Collectibles/CollectibleDefinition.cs`
- `Assets/_Game/Scripts/Gameplay/Roguelite/Collectibles/CollectibleInventory.cs`

## 奖励

- `Assets/_Game/Scripts/Gameplay/Roguelite/Rewards/RogueliteRewardController.cs`
- `Assets/_Game/Scripts/Gameplay/Roguelite/Shop/RogueliteShopController.cs`
- `Assets/_Game/Scripts/Gameplay/Roguelite/Treasure/TreasureChest25D.cs`

## 技力 / 技能 / 正式 HUD

- `Assets/_Game/Scripts/Gameplay/Abilities/IPlayerSkill.cs`
- `Assets/_Game/Scripts/Gameplay/Abilities/PlayerSkillController.cs`
- `Assets/_Game/Scripts/Gameplay/Abilities/GameplayHUDController.cs`
- `Assets/_Game/Scripts/Gameplay/Abilities/SkillReadyWorldIndicator.cs`
- `Assets/_Game/Scripts/Gameplay/Abilities/PlayerSkillPointHUD.cs`（旧场景兼容空壳）
- `Assets/_Game/Editor/SkillHudAssetBootstrap.cs`
- `Assets/_Game/Scripts/Gameplay/Characters/Chen/ChenSkill1.cs`
- `Assets/_Game/Scripts/Gameplay/Characters/Chen/ChenSkill2.cs`

## 数据

- `Docs/RogueRelics/RogueRelicDatabase_IS1_CurrentPool.xlsx`
- `Assets/_Game/Resources/ScavengingCatalog.json`
- `Assets/_Game/Resources/RogueRelics/RuntimeIcons/`
- `Logs/RogueRelicRuntimeImport.json`

---

# 25. 接手硬规则

1. **最终 XLSX 是物品唯一正式人工源。**
2. 不自动重新生成 / 覆盖用户确认过的数据。
3. 藏品拿到立即生效。
4. 所有实体奖励必须进入未撤离背包，不要 direct Acquire 绕过。
5. 跨 Stage 不等于撤离。
6. 只有真实撤离 / 后续明确的部分结算设施才能把物资变安全。
7. 只有普通物资可以撤离进入仓库，不自动出售；藏品严格仅限本 Run。
8. 龙门币是局外长期货币。
9. 源石锭是局内货币，**主页 / 仓库 / 结算不要显示**。
10. 主页左侧只保留指挥官等级。
11. 不恢复主页底部“干员 / 模组 / 采购 / 情报”横条。
12. 仓库 UI 使用灰白不透明风格，不放场景背景。
13. 主页必须使用独立静态切城背景，不允许 Gameplay 摄像机透出。
14. 编队 / 任务只预留入口，暂不开发。
15. 不扩大对当前 XP / Run Level 的依赖。
16. 不恢复旧图标自动联网同步链。
17. 不 commit / push，除非用户明确要求。
18. FolderBridge build/test 不是 Unity 真编译，重大修改必须做 Play Mode 验证。

---

# 26. 推荐下一步执行顺序

### 第一步

Unity Play Mode 验证本轮 P1：

- 背包不同尺寸物品拖拽；
- 非法落点不能覆盖其他物品；
- 拖出网格后生成地面物品；
- 靠近地面物品按 `F` 能重新拾取；
- 藏品丢弃后持续效果撤销，重拾恢复且一次性奖励不重复；
- 跨 Stage 后留在旧 Stage 的地面物品会消失。

### 第二步

验证仓库：

- 默认进入“我的物品”；
- 搜索；
- 低价值筛选；
- 总价值；
- 批量出售当前结果；
- 一键出售低价值；
- 龙门币增量正确。

### 第三步

继续完善真实背包：

- 旋转；
- 占位冲突时交换 / 替换；
- 更明显的合法 / 非法拖拽落点反馈。

### 第四步

继续完善仓库：

- 批量购买；
- 排序；
- 更多过滤；
- 收藏 / 锁定；
- 仓库容量。

### 第五步

行动准备页第一版已完成；后续推进入局携带 / 安全箱，并把风险等级正式接入敌人强度与战利品倍率。

### 第六步

罗德岛临时据点：

- 回血；
- 指定容量部分结算。

---

## 27. 当前总体状态

已经不是“概念验证”。

当前已经具备：

- 正式 118 件搜刮数据库；
- 正式 RuntimeIcons；
- 藏品运行时效果；
- SP 技力；
- 城市场景探索；
- 搜刮容器；
- 真二维背包；
- 背包手动拖拽 / 自动整理；
- 丢弃到地面与重新拾取；
- 局内扩容；
- 统一奖励生命周期；
- 真撤离；
- 成功 / 失败结算；
- 长期系统仓库；
- 龙门币；
- 买卖；
- 仓库搜索 / 总价统计 / 批量出售 / 一键出售低价值；
- 主页；
- 正式行动准备页（仅切城地区 + 风险等级）；
- 正式战斗 HUD 第一版（HP / 技能图标 / SP / 快捷键 / 藏品回技反馈 / 背包状态 / 源石锭 / 头顶 Ready 标识）；
- UGUI 主壳。

当前最需要的不是继续横向加大量系统，而是：

> **继续修正 UI 边界 / 文本遮挡 / 长条图标等比显示，并确保完整 Run 闭环在 Unity Play Mode 中稳定。**

完成这一轮后，再进入背包拖拽 / 入局准备 / 临时据点等下一层系统。
