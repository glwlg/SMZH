# X-TD 已知问题记录

更新时间：2026-05-02

## 1. 外部 BGM 导入后运行时加载失败

状态：未根治，当前有临时兜底。

现象：

- 文件存在：`Assets/Resources/Audio/BGM/hyoshi_action_track_2.ogg`。
- Unity `Editor.log` 中能看到该文件经过 `AudioImporter` 导入。
- 运行时 `Resources.Load<AudioClip>("Audio/BGM/hyoshi_action_track_2")` 返回空。
- 运行时 `Resources.LoadAll<AudioClip>("Audio/BGM")` 也没有拿到该音频。
- Editor 下使用 `AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Resources/Audio/BGM/hyoshi_action_track_2.ogg")` 兜底仍然返回空。

当前处理：

- `BattleController` 会优先尝试加载外部 BGM。
- 外部 BGM 失败时，自动生成一段临时循环战斗 BGM，保证试玩时有背景音乐。
- Console 会出现普通日志：`X-TD 外部 BGM 暂未加载，使用程序生成的临时战斗 BGM。`

后续需要真正解决：

1. 检查该 ogg 是否被 Unity 6.4 / 6000.4.5f1 正确识别为主 `AudioClip` 资源。
2. 尝试删除该音频的 `.meta` 后让 Unity 重新生成导入配置。
3. 尝试把 BGM 转成 Unity 更稳定的 `.wav` 或 `.mp3`，并更新第三方素材登记。
4. 优先改成场景或配置中直接序列化引用 BGM，减少字符串路径加载风险。
5. 打 Windows 包后确认外部 BGM 是否被包含到 Player 中。

注意：

- 当前打击音效已经能正常播放，说明 `AudioSource`、`AudioListener` 和音频输出链路本身是通的。
- 这个问题不应视为“已经修好”，临时生成 BGM 只是为了不阻塞试玩。
