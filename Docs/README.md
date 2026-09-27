# ArknightsACT 文档索引

更新时间：2026-09-26

Docs 已从“按日期堆 handoff”改为“长期系统文档”。后续 Agent 不要从旧 Handoff 推断当前行为。

后续再做功能交接时，要再新建日期型 XXX_HANDOFF_2026_xx_xx.md，直接更新对应的 SYSTEM_0X_*.md。这样 Docs 数量以后基本不会重新膨胀。
## 必读顺序

1. `SYSTEM_01_PROJECT_ARCHITECTURE.md`
   - 项目定位、依赖规则、Run/Meta 边界、当前角色范围、验证入口。
2. `SYSTEM_02_CHARACTERS_COMBAT_NUMERICS.md`
   - 角色、正式战斗、E2/专精、范围、Modifier、P9/P10。
3. `SYSTEM_03_SCAVENGING_META_UI.md`
   - 搜刮、藏品、二维背包、撤离、仓库、局外养成、主页和战斗 HUD。
4. `SYSTEM_04_CITY_WORLD_EXPLORATION.md`
   - 切城生成、分区、建筑/容器、设施、小地图、探索和垂直地标。
5. `SYSTEM_05_ASSET_IMPORT_PRESENTATION_AUDIO.md`
   - 资源边界、Spine、FX、Audio、BGM、角色导入基础设施。
6. `CHARACTER_IMPORT_WORKFLOW.md`
   - 只有在新增/重做角色素材绑定时再读；这是实际角色导入操作流程。

## 保留的数据/工具说明

- `RogueRelics/README.md`
- `RogueRelics/RogueRelicDatabase_IS1_CurrentPool.xlsx`
- `BGM_Preview/`：试听工具/资源，不是交接文档。

## 已删除的旧文档（2026-09-26 执行）

标题以 `OUT —` 开头的 38 份旧文档已按 `OUT_DELETE_LIST.md` 清单全部删除（该清单本身也已删除），内容已并入上述 SYSTEM 文档。Docs 中已不存在这些文件，不要再从旧 handoff 推断当前行为。

当时的合并原则：

- dated handoff / phase 文档：删除；
- 旧角色专项交接：删除；
- 旧城市阶段交接：删除；
- P1-P10 阶段 handoff：删除；
- 被当前本地角色导入流程取代的旧 PRTS/OHMS/FX 说明：删除；
- 不删除 `CHARACTER_IMPORT_WORKFLOW.md` 和 `RogueRelics/README.md`。

备份（Temp 会被清理，需要时尽快取用）：`Temp/docs_out_backup_20260926/`，含 38 份已删文件与 `OUT_DELETE_LIST.md` 副本。

## 当前关键事实

- 正式产品方向：搜打撤 + 轻度肉鸽 + 2.5D ACT + 切尔诺伯格连续城区。
- 正式角色范围：陈、黑、斯卡蒂、维什戴尔、霜星冬痕。
- 正式玩法不依赖局内 TAB 切换；现有切换代码主要保留给 Prototype/测试兼容。
- 普通可玩干员数值走 E2 Lv1/30/60/90 + Skill7/M1/M2/M3。
- 霜星使用 WinterTrace 项目适配数据，不伪装成普通官方 E2 干员。
- P9 负责找数据/硬编码问题；P10 负责验证 Runtime 最终计算。
- 跨 Stage 不等于撤离；只有真实撤离使普通物资安全进入仓库。
- 普通房屋保持规则排列，不恢复早期整体错落/随机旋转方案。
