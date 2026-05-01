# AI 素材登记表

这份文件用于记录 X-TD 中使用的 AI 生成或 AI 辅助素材。后续如果准备上 Steam，需要根据平台要求如实披露 AI 内容来源和二次处理方式。

## 当前素材清单

| 素材路径 | 用途 | 来源工具 | 生成提示词或简述 | 是否二次处理 | 备注 |
| --- | --- | --- | --- | --- | --- |
| `Assets/_Project/Art/AI/SourceSheets/ai_card_sheet_raw.png` | 卡牌正面展示源素材表 | ChatGPT 图像生成 | 4 列 3 行，中式神话题材，包含建筑、士兵、敌人、法术、战术图标；卡牌视角正向玩家 | 是 | 原始图保留，便于后续重新裁切 |
| `Assets/_Project/Art/AI/SourceSheets/ai_battle_sheet_raw.png` | 战场单位源素材表 | ChatGPT 图像生成 | 3 列 3 行，上敌下我的战场视角；我方建筑和士兵朝上，敌人朝下 | 是 | 原始图保留，便于后续重新裁切 |
| `Assets/_Project/Art/AI/Cards/*.png` | 手牌和奖励界面的卡牌图 | ChatGPT 图像生成 + 本地裁切 | 从卡牌源素材表裁切，去除品红背景并居中到透明画布 | 是 | 卡牌图使用正面展示视角，不等同于战场落地图 |
| `Assets/_Project/Art/AI/Battle/*.png` | 战场建筑、士兵、敌人精灵 | ChatGPT 图像生成 + 本地裁切 | 从战场源素材表裁切，去除品红背景并居中到透明画布 | 是 | 我方单位和炮塔朝上，敌方单位朝下 |
| `Assets/_Project/Art/AI/FX/projectile_spirit_arrow.png` | 远程弹道 | AI 素材风格参考 + 本地绘制 | 依据灵弩坛、弓手和青玉灵光配色制作的上行灵箭弹道 | 是 | 同目录保留 `projectile_spirit_arrow_sheet.png` 作为后续动画帧 |
| `Assets/_Project/Art/AI/FX/fx_hit_jade_spark.png` | 命中特效 | AI 素材风格参考 + 本地绘制 | 青玉火花、金色冲击线，适合普通命中特效 | 是 | 同目录保留 `fx_hit_jade_spark_sheet.png` |
| `Assets/_Project/Art/AI/FX/fx_samadhi_fire_impact.png` | 三昧真火落点特效 | ChatGPT 图像生成 + 本地帧处理 | 基于 `card_fireball.png` 生成冲击帧和发光层 | 是 | 同目录保留 `fx_samadhi_fire_impact_sheet.png` |
| `Assets/_Project/Art/AI/Backgrounds/battlefield_honghuang_ai.png` | 战斗场景全屏背景 | imagegen CLI / OpenAI 兼容接口 | 中式神话洪荒风格开放战场，上方妖雾赤土、下方仙灵青玉气息；无单位、无建筑、无怪物虚影、无中路 | 否 | 已用 imagegen 重新生成；保留原 `.meta`，Unity 刷新后会沿用同一个资源引用 |
| `Assets/_Project/Art/AI/UI/card_frame_honghuang_raw.png` | 卡牌边框原始生成图 | imagegen CLI / OpenAI 兼容接口 | 中式神话洪荒风格竖版卡牌边框，朱红漆、鎏金纹、青玉嵌饰、云纹与符箓结构；外部和内部留纯品红抠图区域 | 否 | 原始图保留，便于后续重新抠图或重绘 |
| `Assets/_Project/Art/AI/UI/card_frame_honghuang.png` | 手牌 UI 的透明卡牌边框 | imagegen CLI + 本地抠图 | 从 `card_frame_honghuang_raw.png` 移除纯品红背景，保留透明内窗和透明外部区域 | 是 | 同步复制到 `Assets/Resources/UI/card_frame_honghuang.png`，供运行时 `Resources.Load` 加载 |

## 处理脚本

当前批量裁切和透明化脚本位于：

`W:\glwlg\game\X-TD\.codex-tmp\asset-generation\process_ai_assets.py`

脚本用途：

- 从源素材表裁切卡牌图和战场图。
- 移除品红背景并导出透明 PNG。
- 生成基础弹道、命中特效和三昧真火冲击帧。
- 不再生成战场背景，避免覆盖已经用 imagegen 单独生成的正式背景图。

## 记录规则

- 所有 AI 生成的单位图、卡牌图、图标、神器图、商店页图片、宣传图、视频截图都要记录。
- 提示词或简述要能说明素材是怎么来的。
- 如果生成后做了裁切、抠底、描边、调色、拼合或动画帧处理，需要标记。
- 正式素材不要使用第三方角色名、商标名、具体画师名或明显侵权风格提示词。
- 卡牌正面图和战场落地图要分别登记，因为二者视角不同。
