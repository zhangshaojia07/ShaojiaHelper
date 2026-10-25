# Changelog

## [1.0.1] - unreleased
> 发布时把下面这段（含标题）直接复制到 GameBanana 的 Update 内容里

- 目标框架由 net10.0 改为 net8.0，与 Everest / Celeste 1.4 的 .NET 8 运行时一致，避免运行时不兼容。
- 移除 CrusherKevin 上无效的 `[TrackedAs(typeof(CrushBlock))]`（不继承 CrushBlock，被 Everest 静默忽略）。
- 补齐标准 Module 骨架：新增 Settings / Session / SaveData，为后续配置项与存档做准备。
- 依赖由 Everest 改为 EverestCore（符合 core mod 规范）。

## [1.0.0] - First Release
- Loenn plugins support
- 新增 Crusher Kevins：四个方向上视线能看到玩家时激活。
