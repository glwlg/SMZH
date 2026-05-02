# X-TD 内容配置字段清单

更新时间：2026-05-02  
用途：记录当前 ScriptableObject 配置字段，方便后续改成 CSV/JSON 导入或继续扩展编辑器工具。

## ContentCatalog

内容总表，当前由 `DemoContentFactory` 生成，也可以保存为 `Assets/_Project/Content/DemoContentCatalog.asset`。

| 字段 | 说明 |
| --- | --- |
| `cards` | 卡牌定义列表 |
| `units` | 单位定义列表 |
| `artifacts` | 神器定义列表 |
| `encounters` | 遭遇定义列表 |
| `nodes` | 预留节点定义列表 |

## CardDefinition

| 字段 | 说明 |
| --- | --- |
| `id` | 卡牌唯一标识，同一张卡不同等级用 `_lv2`、`_lv3` 后缀 |
| `displayName` | 中文显示名 |
| `type` | 卡牌类型：士兵、精兵、英雄、法术、战术、建筑等 |
| `rarity` | 稀有度 |
| `level` | 卡牌等级，当前为 1-3 |
| `cost` | 释放费用 |
| `releaseRule` | 释放规则：无目标、我方半场、任意位置 |
| `description` | 卡牌说明 |
| `art` | 手牌/奖励界面卡图 |
| `unitSpawns` | 释放后生成的单位列表 |
| `effects` | 释放后触发的战斗效果列表 |

## CardUnitSpawn

| 字段 | 说明 |
| --- | --- |
| `unit` | 要生成的单位 |
| `count` | 生成数量 |
| `spacing` | 多单位生成间距 |
| `yJitter` | 生成位置纵向随机偏移 |

## UnitDefinition

| 字段 | 说明 |
| --- | --- |
| `id` | 单位唯一标识 |
| `displayName` | 中文显示名 |
| `faction` | 阵营：玩家或敌方 |
| `role` | 单位定位：士兵、精兵、英雄、建筑、怪物、首领 |
| `maxHp` | 最大生命 |
| `attack` | 单次攻击伤害 |
| `attackInterval` | 攻击间隔 |
| `range` | 攻击范围 |
| `moveSpeed` | 移动速度，建筑和首领核心通常为 0 |
| `commandCost` | 预留权重字段；当前战斗中的阵位按建筑数量计算，士兵不占阵位 |
| `projectileSpeed` | 预留弹道速度 |
| `blocksMovement` | 是否阻挡推进 |
| `art` | 战场精灵 |
| `tint` | 无贴图或调色时使用的颜色 |
| `prefab` | 预留单位预制体 |
| `producedUnit` | 建筑生产出的单位 |
| `productionInterval` | 建筑生产间隔 |
| `productionCount` | 每次生产数量 |
| `productionSpread` | 生产位置横向扩散 |

## BattleEffectDefinition

| 字段 | 说明 |
| --- | --- |
| `effectType` | 效果类型：伤害、范围伤害、治疗、护盾、攻击增益、攻速增益、抽牌、加费用、加士气等 |
| `targetRule` | 目标规则：敌方前排、敌方后排、全部敌人、我方前排、全部我方单位等 |
| `value` | 效果数值 |
| `duration` | 持续时间 |
| `radius` | 范围半径 |
| `statusId` | 预留状态标识 |

## ArtifactDefinition

| 字段 | 说明 |
| --- | --- |
| `id` | 神器唯一标识 |
| `displayName` | 中文显示名 |
| `rarity` | 稀有度 |
| `trigger` | 触发方式，当前大多数为被动 |
| `description` | 神器说明 |
| `icon` | 神器图标，后续素材升级时使用 |
| `effects` | 预留效果列表 |

## EncounterDefinition

| 字段 | 说明 |
| --- | --- |
| `id` | 遭遇唯一标识 |
| `displayName` | 中文显示名 |
| `nodeType` | 对应节点类型 |
| `playerBaseMaxHp` | 本场战斗我方基地最大生命 |
| `enemyBaseMaxHp` | 普通怪物节点的敌方基地最大生命 |
| `enemySpawnInterval` | 敌方派兵间隔 |
| `rewardGold` | 胜利金币奖励基础值 |
| `enemySpawns` | 敌方派兵池 |
| `coreEnemy` | 精英和首领节点的核心敌人；普通怪物节点应为空 |

## EnemySpawnEntry

| 字段 | 说明 |
| --- | --- |
| `unit` | 敌方派出的单位 |
| `count` | 每次派兵数量 |
| `interval` | 预留单条派兵间隔 |

## MapNodeRuntime

运行时迷宫房间，不是编辑器资产。

| 字段 | 说明 |
| --- | --- |
| `Floor` | 当前层 |
| `Row` | 当前房间进度 |
| `NodeIndex` | 同一行内的房间序号 |
| `NodeType` | 节点类型 |
| `EncounterId` | 预留遭遇标识 |
| `NextNodeIndices` | 可连接到的上一行房间序号 |
| `Key` | 房间唯一键 |

## RunState

| 字段 | 说明 |
| --- | --- |
| `floor` | 当前迷宫层 |
| `row` | 当前房间进度 |
| `gold` | 当前金币 |
| `playerHp` | 当前生命 |
| `heroExperience` | 本局获得主角经验 |
| `seed` | 本局随机种子 |
| `isComplete` | 是否通关 |
| `isDefeated` | 是否失败 |
| `lastMessage` | 最近一次流程提示 |
| `deckCardIds` | 当前卡组卡牌 ID |
| `artifactIds` | 本局神器 ID |
| `permanentArtifactIds` | 永久神器 ID |
| `availableNodeIndices` | 当前可选房间序号 |
| `selectedNodeKeys` | 已走房间 |
| `pendingCardRewardIds` | 待选择卡牌奖励 |
| `pendingCardRewardPickCount` | 待选择奖励张数 |
