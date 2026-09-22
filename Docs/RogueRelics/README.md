# RogueRelics 当前入口

当前正式人工数据源：

`RogueRelicDatabase_IS1_CurrentPool.xlsx`

当前运行时同步：

- `../../Assets/_Game/Resources/ScavengingCatalog.json`
- `../../Assets/_Game/Resources/RogueRelics/RuntimeIcons/`

导入方式：

- Unity 菜单：`ArknightsACT > Scavenging > Import Curated IS1 Runtime Catalog`
- 手动：`ImportIS1RuntimeCatalog.bat`

完整当前机制、数据规模、技力系统、容器/背包行为与后续工作：

`../SCAVENGING_RELIC_RUNTIME_HANDOFF_2026_09_20.md`

## 维护规则

1. 不要自动重建或覆盖 `RogueRelicDatabase_IS1_CurrentPool.xlsx`。
2. 用户已经确认最终物品的类型、大小、价值和游戏效果。
3. 修改 XLSX 后重新运行 Runtime Importer，不手改 118 条 Runtime JSON。
4. `RogueRelicDatabase*.csv/json/xlsx` 旧文件只用于数据追溯或重新抓全版本数据库。
5. 旧的自动图标同步 / 客户端解包 / Excel 补图链路已退役。当前正式表与 RuntimeIcons 均为 118/118；后续如修改 XLSX，只运行 Runtime Importer。
