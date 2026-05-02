# AI 素材登记表

这份文件用于记录 X-TD 中使用的 AI 生成或 AI 辅助素材。后续如果准备上 Steam，需要根据平台要求如实披露 AI 内容来源和二次处理方式。

## 当前素材批次

| 批次 | 资源范围 | 来源工具 | 生成说明 | 二次处理 |
| --- | --- | --- | --- | --- |
| v0.2 | 战斗单位、敌人、首领、卡牌图、迷宫节点图标、神器图标、战斗特效、战场背景 | imagegen CLI / OpenAI 兼容接口 | 中式神话、洪荒西游方向；战斗视角为敌方在上、我方在下；战斗落地图和卡牌正面图分开生成 | 是 |

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

`W:\glwlg\game\X-TD\.codex-tmp\asset-generation\process_v02_generated_assets.py`

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
