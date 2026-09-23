# 城市精修与探索流程交接

沿用 `codex/mobile-city-map-generator`。前置设计见 `CITY_INTERACTIONS_LAYER2_HANDOFF_2026_09_22.md`，其中第二层指行动的第二阶段动力维护层。

## 游玩变化

- 门前增加独立铺装、门垫和中文门牌；住宅偶尔出现信箱，维修铺/仓库增加装卸警示线。街道有铺装接缝和沥青修补，门前地坪高度避免与地面闪烁。
- 中文门牌使用深度测试文字材质，背面不显示，墙体/屋顶可以遮挡；字体共用并处理动态图集重建，避免标识透过房屋。
- 入室自动记录建筑名称和访问状态，但不会提前抽取物资或打开容器。区分待检索、已检索但未取走、已清空和空置房间。
- 小地图的未进入建筑为浅灰，已进入且仍有物资为青色，清空/空置房间暗化。显示当前建筑状态、目标方位和直线距离，金色方框表示指引目标。
- **M** 切换小地图展开尺寸，不暂停战斗；**N** 循环探索、返回撤离点、前往下一阶段入口，尚未开启下一阶段时跳过该项。
- 探索模式优先指向已走访区块中的未进入建筑，再指向相邻未知街区；不会显示远处未知建筑的名称或掉落。
- 背包占用达到 85% 时建议返程，生命低于 35% 时提示寻找医疗补给或撤离。返程显示抵达后按 E 确认撤离，下一阶段提示携带物资仍有风险。提示不会自动移动、撤离、领取物资或推进关卡。

## 实现

- `World/CityExplorationGuide.cs`：访问记录、探索/返程目标与状态提示。每 0.25 秒更新，房间列表最多每秒扫描一次，换关重置目标。
- `World/EnterableBuilding25D.cs`：建筑名称及搜索统计，保留原有导航和室内判定。
- `Routing/RogueliteMinimapController.cs` / `RogueliteMinimapGraphic.cs`：展开地图、探索信息与目标标识。
- `World/RogueliteStageCityStreetsController.Detail.cs` / `.Sectors.cs`：地面与门牌精修。
- `Assets/_Game/Resources/CityWorldText.shader`：可随构建保留的世界文字着色器。

除 Shader 完整路径外，脚本路径相对 `Assets/_Game/Scripts/Gameplay/Roguelite/`。

探索记录仅属于当次地图，不写入长期存档。切换角色不清除同地图访问记录，切换地图重新开始统计。所有附加地面细节不改变碰撞和导航。

## 验证

Game.Gameplay / Game.Editor 编译通过。自动校验包含：未搜索/已揭示/已领取/空房状态；指引不提前抽奖或暴露未知建筑；撤离目标与操作提示；低血量提示；换关重置；方位计算。保留 384 个地图计划及三个种子 × 三个阶段的实际几何、门口、搜索站位、设施效果、导航和墙体穿插检查。

图形检查输出到 `Logs/CityVisuals/`：`city-overview.png`、`street-detail.png`、`maintenance-overview.png`、`maintenance-detail.png`、`exploration-map-expanded.png`。这些是隔离生成器预览，正式场景实战帧率和操作节奏仍需试玩。

隔离校验工程现位于被 Git 忽略的 `Logs/MobileCityValidationProject/`，避免 Unity 重启清理 Temp 时删除工程。报告复制到 `Logs/MobileCityGenerationValidation.txt`。

Unity 编译完成后重新开始行动即可体验新生成内容。操作：F 搜索、按住 G 使用设施、M 展开地图、N 切换指引、E 在撤离点/已开启阶段入口交互。
