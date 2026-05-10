# 神魔镇荒 已知问题记录

更新时间：2026-05-08

## 1. 战斗 BGM 的 Resources 导入仍需跨端复验

状态：生产风险已降低，仍建议在正式出包前做跨端复验。

现象：

- 文件存在：`Assets/Resources/Audio/BGM/hyoshi_action_track_2.ogg`。
- 同源原始文件已额外放入：`Assets/StreamingAssets/Audio/BGM/hyoshi_action_track_2.ogg`。
- Unity `Editor.log` 中能看到该文件经过 `AudioImporter` 导入。
- 运行时 `Resources.Load<AudioClip>("Audio/BGM/hyoshi_action_track_2")` 返回空。
- 运行时 `Resources.LoadAll<AudioClip>("Audio/BGM")` 也没有拿到该音频。
- Editor 下使用 `AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Resources/Audio/BGM/hyoshi_action_track_2.ogg")` 兜底仍然返回空。

当前处理：

- `XtdProjectBootstrapper` 重建 `BattlePrototype` 场景时会优先把 BGM 直接序列化到 `BattleController.battleMusicClip`，减少运行时字符串路径加载风险。
- `BattleController` 保留兜底加载顺序：
  1. 场景序列化的 `battleMusicClip`
  2. `Resources.Load<AudioClip>("Audio/BGM/hyoshi_action_track_2")`
  3. Editor 下额外尝试 `AssetDatabase` 和同目录搜索
  4. 仍失败时，改走 `StreamingAssets/Audio/BGM/hyoshi_action_track_2.ogg`
  5. 上述都失败，最后才生成临时循环战斗 BGM
- 因此当前试玩与打包版本通常可以听到真正的外部 BGM，程序生成的临时 BGM 只作为最后保护。
- 若走到 `StreamingAssets` 分支，Console 会输出普通日志：`神魔镇荒使用 StreamingAssets 加载战斗 BGM。`
- 只有三段都失败时，才会输出警告并回落到程序生成的临时 BGM。

后续建议：

1. 检查该 ogg 是否被 Unity 6.4 / 6000.4.5f1 正确识别为主 `AudioClip` 资源。
2. 尝试删除该音频的 `.meta` 后让 Unity 重新生成导入配置。
3. 尝试把 BGM 转成 Unity 更稳定的 `.wav` 或 `.mp3`，并更新第三方素材登记。
4. 在 Unity 关闭后做一次真正的批处理验证，确认 Editor / Windows Build / WebGL 三端都稳定走外部 BGM 而不是最终兜底。

注意：

- 当前打击音效已经能正常播放，说明 `AudioSource`、`AudioListener` 和音频输出链路本身是通的。
- 这个问题不应视为“所有平台已经完全复验通过”；当前是从“常态依赖临时 BGM”收口成了“序列化引用优先，失败才最终兜底”。

