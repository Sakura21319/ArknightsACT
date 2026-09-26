# ArknightsACT 文档索引

更新时间：2026-09-24

本目录已经经历多轮原型、地图和角色迭代。后续接手不要按文件名时间顺序全部阅读；优先使用下面的“当前入口”，只有追查历史实现时才看阶段文档。

## 1. 当前接手入口

1. `PROJECT_STATUS_AND_ROADMAP_2026_09_24.md`
   - 当前进度、验收边界和 P0-P4 后续计划的简明总览。
2. `MASTER_HANDOFF_2026_09_23.md`
   - 详细项目交接快照，记录玩法、实现边界、角色状态、城市系统和长期规则；遇到进度变化时以本索引、最新专项文档和当前源码为准。
3. `P1_COMBAT_STATUS_HANDOFF_2026_09_23.md`
   - 当前战斗底层 source of truth。
   - DEF / RES / True、穿透、目标属性 Aura、DamageTags、数据驱动 Status、控制/中断、DOT、Schwarz 破甲与防御藏品迁移。
4. `CHARACTER_SYSTEM_REFACTOR_HANDOFF_2026_09_22.md`
   - `PlayerRuntimeContext`、`PlayableOperatorSwitchController`、局内角色切换和 Run 状态迁移。
5. `ARCHITECTURE.md`
   - Core / Combat / Gameplay / Character-specific 的依赖方向和禁止结构。

## 2. 当前角色文档

- `CHARACTER_IMPORT_WORKFLOW.md`
  - 当前角色素材绑定 source of truth；用户负责从游戏本体导出，Agent 负责直接修改工程并完成 Spine/动作/FX/Audio/UI/Gameplay 全绑定。
  - 记录统一 Spine 动作 2x、Straight Alpha、高清 atlas 复用、FX timing.json FPS、公共训练假人等长期规则。
- `WISADEL_FX_BINDING_2026_09_24.md`
  - Wisadel effects 79 组重新扫描结果；当前 Basic/S2/S3 复合 FX、game#9 替换、S1/Token 排除项和调参 slot 的明确绑定表。
- `CHARACTER_IMPORT_PIPELINE_REVIEW_2026_09_24.md`
  - Wisadel 导入流程审阅与 V2 重构记录；包含已落地项、仍待 Unity 验收项以及后续批量角色导入方向。
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

- `ASSET_INTEGRATION.md`：资源导入层与运行时表现层的边界。
- `PRTS_PROTOTYPE_ASSET_PACK.md`：本地资源接入与素材边界。

早期已完成的 Phase 计划、旧音频菜单流程与初始 Demo 说明已清理；当前行为以源码和上方专项文档为准。

## 6. 历史与专项资料

被后续实现和交接取代的旧计划、旧原型与重复交接已从目录清理。需要了解当前架构时查看 `ARCHITECTURE.md` 和当前专项文档，不再从旧阶段计划推断运行时行为。

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
