# 发现与决策

## 需求

用户要求根据代码分析结果制定调整计划，并按优先级逐步执行优化。优化范围涵盖工程结构、代码质量、性能、可维护性、测试和用户体验。

## 研究发现

### 项目现状

- 这是一个 Windows 专属的 WPF 桌面应用，用于监控本地开发端口。
- 仓库根目录下没有 `.sln`、`.editorconfig`、`global.json` 或 `Directory.Build.props`。
- 项目分为两个部分：
  - `PortLens.Core`：核心扫描和进程检查逻辑。
  - `PortLens.Desktop`：WPF UI、托盘、设置和用户操作。
- 没有单元测试或 CI/CD。
- `outputs/` 目录在 `.gitignore` 中，不会进入版本控制。

### 主要问题

1. **`ProcessInspector.cs` 过大（742 行）**
   - 包含命令行读取、工作目录读取、进程树读取、框架推断、项目名解析、CPU 采样、缓存管理等多个职责。
   - 建议拆分为多个单一职责的类。

2. **`MainWindow.xaml.cs` 过于臃肿（947 行）**
   - 混合了视图逻辑、设置管理、扫描调度、托盘交互等。
   - 建议引入 MVVM 模式，将逻辑拆到 ViewModel。

3. **缺少工程基础设施**
   - 无 `.sln`、`.editorconfig`、`global.json`、`Directory.Build.props`。
   - 版本号硬编码在 `.csproj` 中。

4. **性能瓶颈**
   - `ProcessCommandLineReader.ReadMany()` 拉取全部 `Win32_Process` 再过滤。
   - 搜索过滤在主线程同步执行。
   - 缺少 `CancellationToken`。
   - 缓存使用 `Dictionary + lock`。

5. **可维护性问题**
   - 大量空 `catch` 块。
   - 颜色/样式硬编码。
   - `NormalizeEnabledFrameworks` 包含临时迁移逻辑。
   - 缺少依赖注入。

6. **功能缺失**
   - 无测试。
   - 无 CI/CD。
   - 仅支持 TCP。
   - 无日志/崩溃报告。
   - 关闭按钮图标语义不清晰。

## 技术决策

| 决策 | 理由 |
|------|------|
| 分阶段执行，先工程基建再高优先级重构 | 降低回归风险，便于逐步验证 |
| 拆分 `ProcessInspector` 为多个职责单一的类 | 提高可测试性和可维护性 |
| 引入 MVVM 拆分 `MainWindow.xaml.cs` | 解耦 UI 与业务逻辑，便于测试 |
| 使用 `Microsoft.Extensions.DependencyInjection` | 与 .NET 生态一致，无需额外学习成本 |
| 优先添加单元测试，再做 CI/CD | 单元测试不依赖管理员权限，执行稳定 |
| 统一颜色/样式到资源字典 | 便于维护和未来支持深色模式 |
| 使用 `ConcurrentDictionary` 替代 `Dictionary + lock` | 简化并发控制 |

## 阶段 1 结果

- 创建了 `PortLens.sln`，包含 `PortLens.Core` 和 `PortLens.Desktop` 两个项目。
- 创建了 `Directory.Build.props`，统一 `TargetFramework`、`Nullable`、`ImplicitUsings`、`LangVersion` 和版本号。
- 创建了 `global.json`，固定 SDK 版本为 `10.0.102`。
- 创建了 `.editorconfig`，统一 C#、XAML、XML、JSON、Markdown 等文件的代码风格。
- 简化了两个 `.csproj` 文件，移除重复属性，统一从 `Directory.Build.props` 继承。
- 验证 `dotnet build PortLens.sln` 成功，0 警告，0 错误。

## 遇到的问题

| 问题 | 解决方案 |
|------|---------|
| 无 | 无 |

## 资源

- [PortLens.Core 项目文件](work/PortLens.Core/PortLens.Core.csproj)
- [PortLens.Desktop 项目文件](work/PortLens.Desktop/PortLens.Desktop.csproj)
- [ProcessInspector.cs](work/PortLens.Core/Services/ProcessInspector.cs)
- [MainWindow.xaml.cs](work/PortLens.Desktop/MainWindow.xaml.cs)
- [publish.ps1](scripts/publish.ps1)

## 视觉/浏览器发现

- 无

---
*每执行2次查看/浏览器/搜索操作后更新此文件*
*防止视觉信息丢失*
