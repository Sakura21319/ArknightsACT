# 城市建筑质感与小地图交接

分支：`codex/mobile-city-map-generator`。延续移动城市区块生成器，保持种子可复现。

## 本次改动

- 12 种建筑继续使用各自的容器池、数量和空置概率；新增六组立面配色与程序混凝土纹理，窗框、玻璃、窗台、檐口、落水管、门灯、墙面修补和告示细节。
- 仓库使用坡屋顶，公寓与办公建筑有屋顶机房，商店有门头和遮阳棚。建筑位置、高度和容器槽位有受约束的随机变化；装饰随机数独立于掉落，避免视觉调整改变奖励。
- 街道增加路灯、排水格栅和住宅/商业区长椅。发光材质表现门灯与路灯，没有为每栋楼新增实时光源。
- 容器采用不同尺寸和材质；柜子、货架、保险柜等有内部结构，货架有不同疏密的物资。增加把手、锁扣、护角、通风孔、标签和磨损，保留开柜/抽屉搜索动作及五档珍稀度规则。
- 右上角新增小地图：随地图长宽保持比例，显示已探索道路与建筑、玩家位置和移动朝向、撤离点及距离、可用下一阶段出口、15 米内未清空容器。未知区块不显示建筑与容器。
- 小地图随换关刷新，跟随当前受控角色；基地、结算与背包界面隐藏，不拦截输入。每秒刷新 10 次，建筑与容器列表每 0.6 秒更新，无额外渲染摄像机。

## 入口

- `Assets/_Game/Scripts/Gameplay/Roguelite/World/RogueliteStageCityStreetsController.Detail.cs`：立面材质、建筑与街道细节。
- `Assets/_Game/Scripts/Gameplay/Roguelite/Treasure/SearchableContainer25D.cs` 与 `SalvageContainerProfiles.cs`：容器尺寸与造型。
- `Assets/_Game/Scripts/Gameplay/Roguelite/Routing/RogueliteMinimapController.cs` / `RogueliteMinimapGraphic.cs`：小地图界面和网格绘制。
- `Assets/_Game/Editor/MobileCityGenerationValidation.cs`：生成校验与 `RenderPreview` 诊断渲染入口。

## 验证与试玩

Game.Gameplay / Game.Editor 编译零错误。隔离 Unity 校验覆盖 250000 次珍稀度抽取、15000 个建筑计划、28 种容器、384 个地图布局、三阶段门洞/搜索站位/导航，以及四种地图尺寸的小地图投影。诊断渲染验证小地图 CanvasRenderer 与网格非空，并检查建筑近景、容器图集。

报告在 `Logs/MobileCityGenerationValidation.txt`，预览在 `Logs/CityVisuals/`。预览使用隔离场景和简化环境材质，不代表完整正式场景的最终光照，也没有代替正式场景帧率与角色实玩测试。

等待 Unity 编译结束后，重新进入 Play 并开始新行动即可体验；正在运行的旧地图不会自动替换。
