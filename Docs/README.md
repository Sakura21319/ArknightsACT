# ArknightsACT 文档索引

更新时间：2026-09-23

本目录已经经历多轮原型、地图和角色迭代。后续接手不要按文件名时间顺序全部阅读；优先使用下面的“当前入口”，只有追查历史实现时才看阶段文档。

## 1. 当前接手入口

1. `MASTER_HANDOFF_2026_09_23.md`
   - 当前唯一总交接入口。
   - 汇总项目方向、架构、三名角色、HUD、118 件搜刮数据、二维背包、真撤离、仓库、切城、设施、验证状态、技术债和 P0-P4 后续计划。
2. `P1_COMBAT_STATUS_HANDOFF_2026_09_23.md`
   - 当前战斗底层 source of truth。
   - DEF / RES / True、穿透、目标属性 Aura、DamageTags、数据驱动 Status、控制/中断、DOT、Schwarz 破甲与防御藏品迁移。
3. `CHARACTER_SYSTEM_REFACTOR_HANDOFF_2026_09_22.md`
   - `PlayerRuntimeContext`、`PlayableOperatorSwitchController`、局内角色切换和 Run 状态迁移。
4. `ARCHITECTURE.md`
   - Core / Combat / Gameplay / Character-specific 的依赖方向和禁止结构。
5. `PROJECT_HANDOFF_2026_09_21.md`
   - 搜打撤 / UI / 仓库等上一版总交接，现作为专项细节补充。
   - 若与 MASTER 冲突，以 MASTER + 当前源码为准。

## 2. 当前角色文档

- `FROSTNOVA_CHARACTER_HANDOFF_2026_09_23.md`
  - 当前最新角色接入。
  - 冬痕 Slot 1 = `Skill_1`，Slot 2 = `Skill_3`。
  - Buff05 是常驻背部 FX；Skill3 期间隐藏。
  - 当前仍有 Buff03/04 用途与实机位置需要继续核对。
- `SCHWARZ_CHARACTER_HANDOFF_2026_09_22.md`
  - 黑三套皮肤、S2 + S3、狙击镜、皮肤 FX 与调参。
  - 2026-09-23 P1 已补正式 DEF / 破甲箭头；战斗底层规则以 `P1_COMBAT_STATUS_HANDOFF_2026_09_23.md` 为准。
- `HANDOFF_CHEN_2D_ACT.md`
  - 陈基础 ACT 动作和 Presentation 设计背景。
- `CHEN_CUSTOM_FX_HANDOFF.md`
  - 陈 FX 当前约束。
- `EXTRACTED_FX_PIPELINE.md`
  - 外部提取 PNG 帧 → Unity Sprite/Prefab 的通用管线。
- `OHMS_EFFECT_IMPORTER.md`
  - 仅用于历史研究/依赖分析，不作为当前运行时 FX 主链。

## 3. 当前切城 / 探索文档

阅读优先级：

1. `CITY_ZONES_TOWER_HANDOFF_2026_09_23.md`
   - 当前最高优先级。
   - 四区差异、规则排列房屋、市政核心、高塔、风险设施和联机边界。
   - 明确取代 9/22 “错落建筑” 的布局结论。
2. `CITY_EXPLORATION_POLISH_HANDOFF_2026_09_23.md`
   - 入室状态、探索指引、M 展开地图、N 导航模式与返程提示。
3. `CITY_INTERACTIONS_LAYER2_HANDOFF_2026_09_22.md`
   - 保留设施交互和第二阶段动力维护层设计。
   - 其中“建筑错落/偏转”部分已经被 9/23 规则排列覆盖。
4. `CITY_VISUAL_MINIMAP_HANDOFF_2026_09_22.md`
   - 建筑/容器视觉与小地图。
5. `BUILDING_CONTAINER_TIERS_HANDOFF_2026_09_22.md`
   - 12 类建筑、28 类容器、五档掉落权重。
6. `MOBILE_CITY_GENERATOR_HANDOFF_2026_09_22.md`
   - 4×3 / 4×4 / 5×4 种子城区生成器和验证边界。

## 4. 当前搜刮 / 数据 / UI 文档

- `SCAVENGING_RELIC_RUNTIME_HANDOFF_2026_09_20.md`
  - 当前搜刮、118 件正式物品、藏品效果和技力机制。
- `RogueRelics/RogueRelicDatabase_IS1_CurrentPool.xlsx`
  - 唯一正式人工维护的数据源。
- `MAIN_SHELL_EXTRACTION_HANDOFF_2026_09_21.md`
  - 主页、仓库、撤离流程。
- `HUD_COMBAT_HANDOFF_2026_09_21.md`
  - 正式战斗 HUD、SP 和 Ready 表现。

## 5. 稳定参考文档

以下文档虽然较早，但仍包含独立实现约束，不应仅按日期删除：

- `ASSET_INTEGRATION.md`
- `PRTS_PROTOTYPE_ASSET_PACK.md`
- `PROTOTYPE_25D_DEMO.md`
- `PHASE_03_SPINE_PRESENTATION.md`
- `PHASE_04_ROGUELITE_R1_R2.md`
- `PHASE_05_ROGUELITE_R3_ROUTING.md`
- `PHASE_06_25D_MIGRATION.md`
- `PHASE_07_*.md`
- `PHASE_08_*.md`

这些属于设计/实现演进记录。若与当前 handoff 冲突，以当前 handoff + 当前源码为准。

## 6. 已明确失效的旧交接

以下文件不应再作为实现依据，当前内容已经被新文档或源码覆盖：

- `SCAVENGING_SEARCH_UI_HANDOFF_2026_09_20.md`
- `RogueRelics/ROGUE_RELIC_DB_HANDOFF.md`
- `RogueRelics/RogueRelic_EconomyDesign.md`
- `SCHWARZ_CHARACTER_HANDOFF_2026_09_21.md`
- `CHERNOB0G_CITY_MAP_HANDOFF.md`
- `CHERNOB0G_CITY_MAP_HANDOFF_NEXT.md`
- `CITY_EXPLORATION_HANDOFF_2026_09_20.md`

注意：`RogueRelic_EconomyDesign.md` 仍可能被旧预处理脚本生成，但它不是正式数据源，不能覆盖 `RogueRelicDatabase_IS1_CurrentPool.xlsx`。

## 7. 当前项目关键事实

- 游戏主方向：搜打撤骨架 + 轻度肉鸽局内构筑 + 明日方舟藏品体系。
- 地图：连续移动城区，默认三阶段 4×3 / 4×4 / 5×4。
- 搜刮数据库：118 件；正式源为 IS1 CurrentPool XLSX。
- 背包：真实二维格，初始 4×5，可局内扩容。
- 撤离：跨 Stage 不等于结算；只有真实撤离让普通物资进入长期仓库。
- 角色：通用 Gameplay 不应按角色名分支；陈、黑、霜星保持各自 Character-specific Presentation / Skill / FX 组合层。
- 战斗底层：已具备正式 DEF / RES / True、穿透、目标属性 Aura、DamageTags 与数据驱动 Status；后续不要恢复旧伤害近似。
- 局内切换：统一走 `PlayableOperatorSwitchController`，不要直接 `SetActive` 切 Player。
- 当前地图布局结论：普通房屋使用规则排列，不恢复 9/22 的整体错落/旋转方案。
