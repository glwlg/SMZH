# X-TD

X-TD 是一个肉鸽爬塔式卡牌对抗塔防游戏原型。

当前目标是跑通 v0.2 垂直切片：

- 上下正面对抗战场：敌方在上，我方在下
- 拖拽卡牌到战场释放：建筑只能放在下半场，法术可以越过中线
- 以生产士兵的建筑牌为主要铺场手段，直接出兵牌只承担救场或爆发
- 手牌用完后重新抽一手；用过的卡进入已用卡池
- 费用随时间恢复，统率限制场上己方单位规模
- 普通战斗打爆敌方基地胜利，精英和首领战击败核心敌人胜利
- 精英和首领核心有范围技能、派兵节奏、低血量狂暴和预警圈
- 3 层迷宫，每层 10 行，路线节点、商店、休息、机遇、神器和首领结算串成完整探索
- 金币、卡牌奖励三选一/多选、三合一升级、神器和永久神器会影响本局或局外成长
- 战斗内提供调试按钮：胜利、失败、加金币、抽奖励、加士气、跳节点

## 当前技术栈

- Unity 6.4 / 6000.4.5f1
- C#
- URP
- Windows 桌面版优先，后续面向 Steam

## 启动方式

1. 用 Unity 打开项目目录：`W:\glwlg\game\X-TD`
2. 等 Unity 编译完成。
3. 在顶部菜单执行：`X-TD > 初始化 > 重建 MVP 原型内容`
4. 打开场景：`Assets/_Project/Scenes/Boot.unity`
5. 点击 播放按钮。

从标题界面点击“开始探索”进入迷宫。选择战斗节点后进入战场，把底部扇形手牌拖到战场释放。建筑牌拖到下半场放置，法术牌可以拖到敌方区域。

如果只想调战斗手感，也可以在标题界面点击“只看战斗原型”，或直接打开 `Assets/_Project/Scenes/BattlePrototype.unity`。

## 如果画面还是旧的

请再次执行：

`X-TD > 初始化 > 重建 MVP 原型内容`

这个菜单会重建演示数据和三个基础场景：

- `Boot`
- `MainMenu`
- `BattlePrototype`

## 注意

项目不再依赖 TextMeshPro，避免首次运行时弹出 TMP 导入窗口。

当前原型不再使用 Kenney 免费素材包，也不再使用旧的程序化占位素材。单位、卡牌、战场和基础特效素材统一放在 `Assets/_Project/Art/AI`，素材来源和处理方式登记在 `docs/ai-asset-register.md`。

## MVP 校验

Unity 顶部菜单提供：

- `X-TD > 初始化 > 重建 MVP 原型内容`：重建演示内容、场景和构建列表。
- `X-TD > 验证 > MVP 内容校验`：检查卡牌数量、神器数量、敌人配置、迷宫结构和最终首领闭环。

当前实现状态记录在 `docs/mvp-implementation-status.md` 和 `docs/v0.2-implementation-status.md`。

下一阶段开发计划记录在 `docs/next-phase-plan.md`。

内容配置字段清单记录在 `docs/content-config-fields.md`。

## 另一台电脑试玩

如果只是想在另一台电脑上玩，推荐先在当前电脑用 Unity 打一个 Windows 包，然后把整个输出目录复制过去；另一台电脑不需要安装 Unity。

详细步骤见：`docs/playtest-on-another-pc.md`。

## 已知问题

当前外部 BGM `Assets/Resources/Audio/BGM/hyoshi_action_track_2.ogg` 在本机 Unity 中存在导入后运行时加载失败的问题。游戏目前会自动使用程序生成的临时战斗 BGM 兜底，但这不是根治方案。

问题记录和后续处理方向见：`docs/known-issues.md`。

