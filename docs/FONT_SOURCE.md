# 中文字体来源

`Assets/Resources/HorizonSansSC.ttf` 来自 Google Fonts 的 Noto Sans SC：

- 上游：https://github.com/google/fonts/tree/main/ofl/notosanssc
- 原始文件：`NotoSansSC[wght].ttf`
- 授权：SIL Open Font License 1.1，完整授权在 `Assets/Resources/HorizonSansSC-OFL.txt`。
- 本仓库派生版名称：Horizon Sans SC Medium。使用 fontTools 4.61.1 将字重固定为 500，保留 GB2312 可解码字符、ASCII 及当前 C# 源码中的全部字符，再修改 name 表的家族与 PostScript 名称。包含 7548 个 Unicode 映射，约 2.3 MB。

内置字体避免 UI 在不同设备依赖系统中文字体。罕用汉字、emoji 和未来新增文本仍需要扩展字体覆盖，并重新检查字形。
