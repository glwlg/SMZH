# X-TD 另一台电脑试玩说明

更新时间：2026-05-02

## 推荐方式：打 Windows 包后复制过去

这是给另一台电脑“只玩游戏”的方式，不需要在另一台电脑安装 Unity。

在当前开发电脑上操作：

1. 打开 Unity 项目：`W:\glwlg\game\X-TD`。
2. 等 Unity 编译完成。
3. 确认 Build Settings / Build Profiles 里包含这些场景：
   - `Assets/_Project/Scenes/Boot.unity`
   - `Assets/_Project/Scenes/MainMenu.unity`
   - `Assets/_Project/Scenes/BattlePrototype.unity`
4. 目标平台选择 Windows。
5. 架构选择 x86_64。
6. 点击 Build，输出到类似目录：`Builds/Windows/X-TD`。
7. 把整个输出目录复制到另一台电脑，不要只复制 `.exe`。
8. 在另一台电脑运行 `X-TD.exe`。

复制时至少要包含：

- `X-TD.exe`
- `X-TD_Data/`
- Unity 生成的运行时依赖目录和文件，例如 `MonoBleedingEdge/`、`UnityPlayer.dll` 等。

当前 BGM 注意：

- 外部 BGM 还存在导入/加载问题。
- 当前版本有程序生成的临时战斗 BGM 兜底，所以打包试玩不会完全没有背景音乐。
- 后续修复外部 BGM 后，需要重新打包再复制到另一台电脑。

## 开发方式：在另一台电脑打开 Unity 项目

这是给另一台电脑也要继续开发、调试、改代码时用的方式。

另一台电脑需要准备：

1. 安装 Unity 6.4 / 6000.4.5f1，或尽量接近的 Unity 6 LTS 版本。
2. 安装 Git。
3. 拉取或复制项目仓库。

如果用 Git：

```powershell
git clone <你的仓库地址> X-TD
```

如果直接复制目录，不建议复制这些 Unity 临时目录：

- `Library/`
- `Temp/`
- `Obj/`
- `Build/`
- `Builds/`
- `Logs/`

打开后操作：

1. 用 Unity Hub 打开项目根目录。
2. 等 Unity 自动重新导入资源和编译脚本。
3. 如画面或数据不对，执行：`X-TD > 初始化 > 重建 MVP 原型内容`。
4. 打开 `Assets/_Project/Scenes/Boot.unity`。
5. 点击 Play。

## 当前建议

如果只是想给自己另一台电脑玩，优先用“打 Windows 包后复制过去”。

如果另一台电脑也要参与开发，才用“打开 Unity 项目”的方式。
