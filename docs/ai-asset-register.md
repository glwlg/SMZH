# AI 素材登记表

这份文件用于记录 神魔镇荒 中使用的 AI 生成或 AI 辅助素材。后续如果准备上 Steam，需要根据平台要求如实披露 AI 内容来源和二次处理方式。

## 当前素材批次

| 批次 | 资源范围 | 来源工具 | 生成说明 | 二次处理 |
| --- | --- | --- | --- | --- |
| v0.10-3D战场演示链 | 3D战场远景背景、战场地面、我方阵门、敌方魔门 | imagegen CLI / 本地 OpenAI 兼容接口（`gpt-image-2`） | 面向 2.5D/3D 战场演示链的环境补图；敌上我下；强调中式神话、厚重材质、可承载高密度特效的清晰战场分层 | 是 |
| v0.2 | 战斗单位、敌人、首领、卡牌图、迷宫节点图标、神器图标、战斗特效、战场背景 | imagegen CLI / OpenAI 兼容接口 | 中式神话、洪荒西游方向；战斗视角为敌方在上、我方在下；战斗落地图和卡牌正面图分开生成 | 是 |
| v0.3-HD | 战场背景、战斗单位、建筑、敌人、Boss、卡牌图、特效、神器图标、迷宫节点图标、卡框 | imagegen CLI / 本地 OpenAI 兼容接口（`gpt-image-2`） | 按单个素材逐张生成高清 3D 风格，不再使用多素材拼图或像素风源图；透明素材先生成纯色绿幕再本地抠底 | 是 |
| v0.8-补齐 | 新增卡牌图、职业选择图、职业神通特效、迷宫/商店背景、新增神器图标 | imagegen CLI / 本地 OpenAI 兼容接口（`gpt-image-2`） | 继续按单素材逐张生成，保持中式神话、洪荒西游、高精度 3D 风格；卡牌正向玩家，特效和神器使用绿幕抠底 | 是 |

## v0.10-3D 战场演示链生成记录

本批次用于把当前战斗从“2D 逻辑 + 平面底图”推进到“2D 逻辑 + 3D/2.5D 表现”的第一版演示链。

- 新增 1 张远景背景：`Assets/_Project/Art/AI/Backgrounds/battlefield_honghuang_3d_backdrop.png`
- 新增 1 张战场地面：`Assets/_Project/Art/AI/Backgrounds/battlefield_honghuang_3d_floor.png`
- 新增 1 张我方阵门：`Assets/_Project/Art/AI/FX/battlefield_player_portal_3d.png`
- 新增 1 张敌方魔门：`Assets/_Project/Art/AI/FX/battlefield_enemy_gate_3d.png`
- 原始生成输出位于：`.codex-tmp/asset-generation/v10-stage3d-generated/`
- 透明处理：
  - `battlefield_player_portal_3d.png` 先以纯绿背景生成，再通过 `remove_chroma_key.py` 抠底为透明 PNG
  - `battlefield_enemy_gate_3d.png` 先以纯绿背景生成，再通过 `remove_chroma_key.py` 抠底为透明 PNG
- 运行时接入范围：
  - `BattleStage3D` 优先加载新的远景背景、战场地面、我方阵门、敌方魔门
  - `XtdProjectBootstrapper` 重建 `BattlePrototype` 场景时会优先写入这一批新素材引用
  - 新图同时同步到 `Assets/Resources/Art/AI/`，用于运行时兜底加载

## v0.8-补齐生成记录

本批次用于补齐 MVP 后续新增内容中仍在复用或缺失的素材，并修正部分运行时 `Resources` 路径不一致的问题。

- 新增 7 张卡牌图：缚妖索符、甘霖咒、流云军令、破阵金令、瘴毒葫芦、推山印、聚宝符。
- 新增 4 个神器图标：万兵旗、雷火符匣、点将台、镇煞葫芦。
- 新增 3 张职业选择图：边境指挥官、万灵召使、雷火方士。
- 新增 3 张职业神通特效：边境战令、万灵开阵、九霄雷火。
- 新增 2 张界面背景：迷宫路线背景、商店/休息/神器背景。
- 资源生成清单位于 `output/imagegen/xtd-hd-assets/xtd_v08_needed_jobs.jsonl`。
- 生成原图位于 `output/imagegen/xtd-hd-assets/raw/`，处理后的最终图位于 `output/imagegen/xtd-hd-assets/final/`。
- Unity 资源已同步到 `Assets/_Project/Art/AI/` 和 `Assets/Resources/Art/AI/`；UI 节点和神器图标也同步保留 `Assets/Resources/UI/` 兼容路径。

## v0.3-HD 生成记录

本批次目标是替换当前 AI 美术为更接近商业游戏原型的高清 3D 资产。生成要求如下：

- 每个素材单独生成一张图，不使用多素材拼图、九宫格源图或像素风精灵表。
- 题材保持中式神话、洪荒、西游方向。
- 战斗场景为敌方在上、我方在下，建筑和我方单位的落地图朝上，敌方单位和 Boss 朝下。
- 卡牌图按玩家正面观看的方向生成，不直接复用战斗落地图。
- 透明素材使用纯 `#00ff00` 绿幕背景生成，再通过本地脚本抠底为透明 PNG。
- 本批次生成与处理脚本位于 `.codex-tmp/asset-generation/xtd_hd_asset_pipeline.py`，生成清单位于 `output/imagegen/xtd-hd-assets/xtd_hd_asset_jobs.jsonl`。

## v0.2 源图

| 源图路径 | 用途 | 说明 |
| --- | --- | --- |
| `Assets/_Project/Art/AI/SourceSheets/v02_player_units_sheet.png` | 我方单位和建筑源图 | 3x3 表，我方建筑与士兵朝上，用于战场落地 Sprite。 |
| `Assets/_Project/Art/AI/SourceSheets/v02_enemy_units_sheet.png` | 敌方怪物、精英、小首领源图 | 3x3 表，敌方单位朝下，精英和首领作为敌方核心。 |
| `Assets/_Project/Art/AI/SourceSheets/v02_final_boss_sheet.png` | 最终首领源图 | 单张混沌魔君核心图。 |
| `Assets/_Project/Art/AI/SourceSheets/v02_card_art_sheet.png` | 卡牌正面源图 | 4x3 表，正向玩家展示，不等同于战场落地图。 |
| `Assets/_Project/Art/AI/SourceSheets/v02_node_icon_sheet.png` | 迷宫节点和奖励图标源图 | 4x4 表，包含普通、精英、商店、休息、机遇、神秘、神器、Boss 等图标。 |
| `Assets/_Project/Art/AI/SourceSheets/v02_artifact_icon_sheet.png` | 神器图标源图 | 5x4 表，覆盖当前 MVP 神器和预留图标。 |
| `Assets/_Project/Art/AI/SourceSheets/v02_fx_sheet.png` | 战斗特效源图 | 4x2 表，包含弹道、命中、火焰、雷击、Boss 预警、士气、死亡烟雾等。 |
| `Assets/_Project/Art/AI/SourceSheets/v02_battlefield_background.png` | 战场背景源图 | 16:9 全屏背景，上方妖雾赤土，下方仙灵青玉阵地。 |

## v0.2 输出资源

| 输出路径 | 用途 | 说明 |
| --- | --- | --- |
| `Assets/_Project/Art/AI/Battle/*.png` | 战斗单位、建筑、怪物、精英和首领 | 从源图裁切、去品红底、居中或底部对齐到透明 PNG。 |
| `Assets/_Project/Art/AI/Cards/*.png` | 手牌、奖励和卡牌详情图 | 从卡牌源图裁切，保持正向玩家视角。 |
| `Assets/_Project/Art/AI/FX/*.png` | 弹道、命中、法术和反馈特效 | 从特效源图裁切，保留透明背景。 |
| `Assets/_Project/Art/AI/UI/Nodes/*.png` | 迷宫路线节点图标 | 同步复制到 `Assets/Resources/UI/Nodes/`，运行时可直接加载。 |
| `Assets/_Project/Art/AI/UI/Artifacts/*.png` | 神器图标 | 已写入当前 `DemoContentCatalog.asset` 的神器 icon 引用。 |
| `Assets/_Project/Art/AI/Backgrounds/battlefield_honghuang_ai.png` | 战斗场景背景 | 同步复制到 `Assets/Resources/UI/battlefield_honghuang_ai.png`。 |

## 处理脚本

当前 v0.2 批量处理脚本：

`<项目目录>\.codex-tmp\asset-generation\process_v02_generated_assets.py`

脚本用途：

- 从 `v02-generated` 源图按固定网格裁切素材。
- 去除纯品红背景并导出透明 PNG。
- 为新素材生成 Unity 可识别的 `.meta`。
- 同步更新 `DemoContentCatalog.asset` 中已有单位、卡牌和神器图标引用。
- 将迷宫节点图标和战场背景复制到 `Assets/Resources/UI/`，方便运行时加载。

## 记录规则

- 所有 AI 生成的单位图、卡牌图、图标、神器图、商店页图片、宣传图、视频截图都要记录。
- 提示词或简述要能说明素材是怎么来的。
- 如果生成后做了裁切、抠底、描边、调色、拼合或动画帧处理，需要标记。
- 正式素材不要使用第三方角色名、商标名、具体画师名或明显侵权风格提示词。
- 卡牌正面图和战场落地图要分别登记，因为二者视角不同。

