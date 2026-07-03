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

## 阶段 2 结果

- 拆分了 `ProcessInspector.cs`：
  - 新增 `CpuSampler`：负责 CPU 采样和缓存管理。
  - 新增 `FrameworkDetector`：负责框架推断。
  - 新增 `ProjectNameResolver`：负责工作目录推断和项目名称解析。
  - 新增 `ProcessCommandLineReader`：负责读取进程命令行。
  - 新增 `ProcessCurrentDirectoryReader`：负责从 PEB 读取当前工作目录。
  - 新增 `ProcessTreeReader`：负责统计子进程数量。
- 简化了 `ProcessInspector`，移除内嵌的静态类，改用拆分的组件，并将 `Dictionary + lock` 缓存替换为 `ConcurrentDictionary`。
- 引入 MVVM：
  - 新增 `MainWindowViewModel`，承载扫描调度、搜索过滤、项目分组、黑名单、框架规则等状态。
  - `MainWindow` 仅保留视图相关逻辑、窗口管理、设置持久化、应用资源监控和事件处理。
- 引入依赖注入：
  - 新增 `ServiceRegistration`，注册 `PortScanner`、`MainWindowViewModel`、`PortEntryActionService`、`TrayIconService`、`MainWindow`。
  - 更新 `App.xaml.cs` 通过 `ServiceProvider` 启动主窗口。
  - `MainWindow` 构造函数接收 `IServiceProvider` 并解析依赖。
- 验证：
  - `dotnet build PortLens.sln` 成功，0 警告，0 错误。
  - `powershell.exe ./scripts/publish.ps1` 成功发布到 `outputs/PortLensMaterial`。
  - 修复发布后双击无反应的问题：DI 容器无法自动解析 `Func<string, Task>` 参数，改为手动注册 `MainWindowViewModel` 后启动正常。

## 遇到的问题

| 问题 | 解决方案 |
|------|---------|
| `MainWindowViewModel` 为 internal 导致属性可访问性不一致 | 将 `MainWindowViewModel`、`MainWindowState`、`PortEntryViewModel` 改为 public |
| `StatusText` setter 为 private，MainWindow 无法赋值 | 将 setter 改为 public |
| 删除了 `SettingsButton_Click` 和 `StatusBar_MouseLeftButtonUp` 事件处理器 | 重新添加 |
| `ButtonBase`/`TextBox` 在 WinForms 和 WPF 之间存在歧义 | 使用完全限定名 `System.Windows.Controls.Primitives.ButtonBase` 和 `System.Windows.Controls.TextBox` |
| `ServiceProvider` 与 `IServiceProvider` 转换错误 | `MainWindow` 构造函数改为 `IServiceProvider`，注册也改为 `AddSingleton<MainWindow>` |
| 发布 exe 双击无反应 | `MainWindowViewModel` 改为在 `ServiceRegistration` 中手动注册，`ShowSnackbarAsync` 改为 internal，避免 DI 无法解析 `Func<string, Task>` |

## 阶段 3 结果

- 优化了 `ProcessCommandLineReader.ReadMany()`：
  - CIM 查询改为通过 `-Filter` 按 PID 过滤，避免拉取全部 `Win32_Process`。
  - 为空集合增加短路返回。
- 为长时间操作添加了 `CancellationToken` 支持：
  - `PortScanner.Scan` 接受 `CancellationToken` 并在关键点抛出取消。
  - `ProcessInspector.PreloadProcessDetails` / `EnrichBasic` / `EnrichDetails` 传播 `CancellationToken`。
  - `ProcessCommandLineReader.Read` / `ReadMany` 注册取消回调以结束 PowerShell 子进程。
- 在 `MainWindowViewModel` 中实现搜索防抖与后台过滤：
  - 搜索文本变化时启动 150ms 防抖计时器。
  - 计时器触发后在后台线程计算匹配项的 key 集合，再刷新 `CollectionView`。
  - 为 `PortEntry` 和 `PortEntryViewModel` 增加稳定的 `Key` 属性。
- `MainWindowViewModel.RefreshAsync` 使用 `CancellationTokenSource` 取消上一次的刷新，避免重叠扫描。
- 验证：
  - `dotnet build PortLens.sln` 成功，0 警告，0 错误。
  - `powershell.exe ./scripts/publish.ps1` 成功发布到 `outputs/PortLensMaterial`。
  - 发布后的 `PortLens.exe` 可正常启动。

## 资源

- [PortLens.Core 项目文件](work/PortLens.Core/PortLens.Core.csproj)
- [PortLens.Desktop 项目文件](work/PortLens.Desktop/PortLens.Desktop.csproj)
- [ProcessCommandLineReader.cs](work/PortLens.Core/Services/ProcessCommandLineReader.cs)
- [ProcessInspector.cs](work/PortLens.Core/Services/ProcessInspector.cs)
- [PortScanner.cs](work/PortLens.Core/Services/PortScanner.cs)
- [MainWindowViewModel.cs](work/PortLens.Desktop/ViewModels/MainWindowViewModel.cs)
- [PortEntry.cs](work/PortLens.Core/Models/PortEntry.cs)
- [publish.ps1](scripts/publish.ps1)

## 视觉/浏览器发现

- 无

---
*每执行2次查看/浏览器/搜索操作后更新此文件*
*防止视觉信息丢失*
