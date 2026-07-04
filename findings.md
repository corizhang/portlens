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

## 阶段 4 结果

- 提取了统一的颜色/样式资源字典：
  - 新增 `Themes/PortLensColors.xaml`，集中定义所有主题色刷。
  - 新增 `Themes/PortLensStyles.xaml`，集中定义 `PortActionButton`、`SearchTextBox`、`MetaIcon` 等样式。
  - `App.xaml` 合并上述资源字典。
  - `MainWindow.xaml` 和 `SettingsDialog.xaml` 移除硬编码颜色，改用资源引用。
- 用设置版本迁移替代 `NormalizeEnabledFrameworks` 中的临时逻辑：
  - `DesktopSettings` 增加 `Version` 字段（当前为 1）。
  - `DesktopSettingsStore.Load` 在版本过期时迁移为默认框架列表。
  - `MainWindowViewModel.NormalizeEnabledFrameworks` 仅做有效性过滤，不再包含隐式迁移。
- 添加了日志记录：
  - 引入 `Microsoft.Extensions.Logging.Abstractions`（Core）和 `Microsoft.Extensions.Logging`（Desktop）。
  - 新增 `FileLogger` / `FileLoggerProvider`，日志写入 `%LocalAppData%\PortLens\logs\portlens-YYYYMMDD.log`。
  - `App.xaml.cs` 增加 Dispatcher / AppDomain / TaskScheduler 未处理异常记录。
- 统一异常处理：
  - 将 `ProcessCommandLineReader`、`ProcessTreeReader`、`ProcessCurrentDirectoryReader` 从静态类改为实例类，通过 DI 注入 `ILogger<T>`。
  - `ProcessInspector`、`PortScanner` 改为通过 DI 接收依赖。
  - 原先静默吞掉异常的 `Safe` 辅助方法和 `catch` 块现在记录 Warning 日志。
- 验证：
  - `dotnet build PortLens.sln` 成功，0 警告，0 错误。
  - `powershell.exe ./scripts/publish.ps1` 成功发布到 `outputs/PortLensMaterial`。
  - 发布后的 `PortLens.exe` 可正常启动。

## 阶段 5 结果

- 创建了 `work/PortLens.Core.Tests` xUnit 测试项目，加入 `PortLens.sln`。
- 将 `ProjectRootResolver` 从 Desktop 移动到 Core，使其可被核心测试覆盖。
- 将 `FrameworkDetector`、`ProjectNameResolver`、`TcpRow` 改为 public，提取 `PortScanner` 的纯过滤/排序逻辑到 `PortScannerFilters`，提高可测试性。
- 编写了 45 个单元测试，覆盖框架推断、项目名解析、项目根解析、地址过滤与排序、开发服务启用判断。
- 运行 `dotnet test PortLens.sln` 全部通过。
- 创建 `.github/workflows/ci.yml`，在 `windows-latest` 运行器上执行 restore/build/test/publish，并上传 `outputs/PortLensMaterial` 为 artifact。
- 运行 `powershell.exe ./scripts/publish.ps1` 验证发布成功。

## 遇到的问题

| 问题 | 解决方案 |
|------|---------|
| xunit 属性无法识别 | 在测试文件中添加 `using Xunit;` |
| 文件系统测试受环境祖先目录（如 `.git`）影响 | 将临时目录创建到驱动器根目录，避免扫描到无关的 marker |
| 发布时 `PortLens.exe` 正在运行锁定输出文件 | 使用 `Stop-Process -Name PortLens -Force` 结束后重新发布 |

## 阶段 6 结果

- 将标题栏隐藏到托盘按钮的图标从 `Close` 改为 `ChevronDown`，减少用户将其误认为关闭窗口的歧义。
- 在 `MainWindowViewModel` 增加 `IsLoading` 状态，首次扫描时空状态显示 "Scanning..."，扫描完成后再显示 "No development services found"。
- UDP 端口支持、端口冲突提示、历史图表、深色模式等列为后续可扩展项，未纳入本次调整。

## 阶段 7 后续修复

### 问题

用户报告发布后应用无法启动，且没有任何报错或提示。排查发现：

- 事件查看器中的旧错误（21:33:48）是之前 `Func<string, Task>` 无法解析的问题。
- 当前问题更严重：进程在运行，但主窗口没有创建（`MainWindowHandle=0`，`Visible=False`）。
- 通过文件诊断日志发现 `MainWindow` 构造函数被无限递归调用。

### 根因

在阶段 5/6 的优化中，我将 `ISnackbarService` 的实现指向 `MainWindow` 单例，导致 DI 循环依赖：

```
MainWindow (构造中)
  → MainWindowViewModel
    → ISnackbarService
      → MainWindow (再次请求，因为第一次还在构造中)
        → MainWindowViewModel
          → ...
```

DI 容器不断创建新的 `MainWindow` 实例，每个实例都调用 `InitializeComponent()`，但没有一个能完成构造，因此没有主窗口句柄，应用表现为“无反应”。异常被 `DispatcherUnhandledException` 处理（`e.Handled = true`），所以用户看不到报错。

### 修复

1. 移除 `ISnackbarService` 接口。
2. `MainWindowViewModel` 通过 `SnackbarRequested` 事件通知需要显示 snackbar，不再依赖 UI 服务。
3. `MainWindow` 订阅 `SnackbarRequested` 事件。
4. `TrayIconService` 和 `PortEntryActionService` 改为在 `MainWindow` 构造函数中手动创建，避免 DI 解析 `Window`、`Action<string>`、`Func<Task>` 等类型。
5. `ServiceRegistration` 简化为只注册 Core 服务和 `MainWindowViewModel`。
6. 恢复 `App.xaml.cs` 中 `e.Handled = true`，避免未处理异常导致崩溃。

### 验证

- `dotnet build PortLens.sln` 成功，0 警告，0 错误。
- `dotnet test PortLens.sln` 45 个测试通过。
- `scripts/publish.ps1` 发布成功。
- 使用 `EnumWindows` 检查：发布后的 `PortLens.exe` 进程拥有一个标题为 "PortLens" 的可见窗口。

## 最终状态摘要

| 检查项 | 状态 |
|--------|------|
| 构建 | `dotnet build PortLens.sln` 成功 |
| 测试 | 45 个单元测试全部通过 |
| 发布 | `outputs/PortLensMaterial/PortLens.exe` 生成且窗口正常显示 |
| CI/CD | `.github/workflows/ci.yml` 已配置 |
| 文档 | `README.md` / `CLAUDE.md` 已更新 |

## 资源

- [PortLens.Core 项目文件](work/PortLens.Core/PortLens.Core.csproj)
- [PortLens.Desktop 项目文件](work/PortLens.Desktop/PortLens.Desktop.csproj)
- [PortLensColors.xaml](work/PortLens.Desktop/Themes/PortLensColors.xaml)
- [PortLensStyles.xaml](work/PortLens.Desktop/Themes/PortLensStyles.xaml)
- [App.xaml](work/PortLens.Desktop/App.xaml)
- [MainWindow.xaml](work/PortLens.Desktop/MainWindow.xaml)
- [SettingsDialog.xaml](work/PortLens.Desktop/Dialogs/SettingsDialog.xaml)
- [DesktopSettings.cs](work/PortLens.Desktop/Settings/DesktopSettings.cs)
- [DesktopSettingsStore.cs](work/PortLens.Desktop/Settings/DesktopSettingsStore.cs)
- [FileLogger.cs](work/PortLens.Desktop/Services/FileLogger.cs)
- [App.xaml.cs](work/PortLens.Desktop/App.xaml.cs)
- [ServiceRegistration.cs](work/PortLens.Desktop/ServiceRegistration.cs)
- [ProcessCommandLineReader.cs](work/PortLens.Core/Services/ProcessCommandLineReader.cs)
- [ProcessTreeReader.cs](work/PortLens.Core/Services/ProcessTreeReader.cs)
- [ProcessCurrentDirectoryReader.cs](work/PortLens.Core/Services/ProcessCurrentDirectoryReader.cs)
- [ProcessInspector.cs](work/PortLens.Core/Services/ProcessInspector.cs)
- [PortScanner.cs](work/PortLens.Core/Services/PortScanner.cs)
- [MainWindowViewModel.cs](work/PortLens.Desktop/ViewModels/MainWindowViewModel.cs)
- [publish.ps1](scripts/publish.ps1)

## 视觉/浏览器发现

- 无

## 阶段 8：状态栏与设置持久化回归修复

### 问题

用户报告：
- 状态栏不再显示 CPU/内存信息和版本号。
- 设置似乎在应用重启后没有持久化。

### 根因

1. **状态栏绑定失效**
   - `MainWindow` 的 `DataContext` 被设置为 `MainWindowViewModel`。
   - 但 `AppResourceText`、`AppVersionText`、`ShowAppMetrics` 定义在 `MainWindow`（code-behind）上，导致 XAML 的 `{Binding ...}` 在 ViewModel 中找不到这些属性。
   - 特别是 `ShowAppMetrics` 绑定失败时，相关 Visibility 默认为 Collapsed，进一步隐藏了 CPU/内存区域。
   - `materialDesign:Snackbar` 的 `MessageQueue` 同样绑定到 `MainWindow.SnackbarMessageQueue`，也失效。

2. **设置未应用到 ViewModel**
   - `MainWindow` 构造函数先调用 `ApplyPersistedSettings()`，再创建 `MainWindowViewModel`。
   - `ApplyPersistedSettings` 只设置了本地字段（`_rememberWindowPlacement`、`_closeToTray`、`_showAppMetrics`），没有将 `_settings` 中的 `ShowSystemPorts`、`RefreshIntervalSeconds`、`GroupByProject`、`ExcludedPorts`、`EnabledFrameworks` 等应用到 ViewModel。
   - 结果 ViewModel 使用默认值，随后 `SaveSettings` 把这些默认值写回磁盘，覆盖用户之前的设置。

### 修复

1. 将 `AppResourceText`、`AppVersionText`、`ShowAppMetrics` 移到 `MainWindowViewModel`，并在 `MainWindow.UpdateAppMetrics` 中通过 `_viewModel.AppResourceText` 更新。
2. 使用 `RelativeSource={RelativeSource AncestorType=Window}` 修复 `Snackbar.MessageQueue` 绑定。
3. 调整 `MainWindow` 构造顺序：先创建 ViewModel、设置 `DataContext`，再调用 `ApplyPersistedSettings`。
4. 新增 `BuildStateFromSettings()`，将加载的设置转换为 `MainWindowState` 并调用 `_viewModel.ApplyState(...)`。
5. 统一通过 `_viewModel.ShowAppMetrics` 读写“显示应用指标”设置。

### 验证

- `dotnet build PortLens.sln` 成功，0 警告，0 错误。
- `dotnet test PortLens.sln` 45 个测试通过。
- `scripts/publish.ps1` 发布成功。
- UI 自动化读取到发布后的 `PortLens.exe` 状态栏文本：
  - `Development services`
  - `1 services`
  - `Scan 10:13:24`
  - `CPU 4.1%  Mem 183 MB`
  - `v0.1.0`
- 设置持久化验证：预修改 `ShowSystemPorts=true`、`RefreshIntervalSeconds=30`、`ShowAppMetrics=false`，重启应用后设置保持不变。

## 阶段 9：性能优化 A/B/C 实施总结

### 目标

延续阶段 8 的修复，按投入产出比进一步降低 PortLens 的 CPU、内存和子进程开销。

### 阶段 A：UI 虚拟化

**问题**：主列表使用 `ScrollViewer` 包裹 `ItemsControl`，默认面板是 `StackPanel`，所有条目一次性实例化可视树。端口多时布局/渲染开销大。

**修复**：
- 移除外部 `ScrollViewer`。
- 在 `ItemsControl.Template` 内放置 `ScrollViewer`。
- 设置 `ItemsPanel` 为 `VirtualizingStackPanel`，`ScrollUnit="Pixel"`。
- 启用 `VirtualizingPanel.IsVirtualizing`、`IsVirtualizingWhenGrouping`、`ScrollViewer.CanContentScroll`。

**结果**：列表仅实例化可视区域内条目，分组、展开/折叠、上下文菜单保持正常。

### 阶段 B：预计算搜索 haystack 与项目分组字段

**问题**：`PortEntryViewModel` 的 `ProjectRootDirectory`、`ProjectGroupKey`、`ProjectGroupTitle`、`ProjectGroupSubtitle` 每次属性访问都调用 `ProjectRootResolver.Resolve`，搜索过滤时还做 `string.Join` 分配新字符串。

**修复**：
- 在 `PortEntryViewModel` 构造函数和 `Update()` 中调用 `RecalculateDerivedValues()`，一次性计算并缓存这些字段。
- 新增 `SearchHaystack` 属性。
- `MainWindowViewModel.MatchesText` 直接使用 `entry.SearchHaystack.Contains(text)`。

**结果**：每次扫描只进行一次文件系统遍历和字符串拼接，搜索和分组刷新不再重复走目录。

### 阶段 C：跨扫描缓存 PowerShell/CIM 结果

**问题**：每次刷新都启动 PowerShell 读取命令行和进程树。默认 5 秒刷新一次，子进程开销高。

**修复**：
- 新增 `IProcessCommandLineReader` 和 `IProcessTreeReader` 接口。
- `ProcessCommandLineReader` 增加按 PID 的 TTL 缓存（60s），`ReadMany` 只查询缺失/过期的 PID。
- `ProcessTreeReader` 增加整个父子关系图的 TTL 快照缓存（60s），并支持 `CancellationToken`。
- `ProcessInspector.PruneCaches` 调用两个 reader 的 `Prune` 方法移除已退出进程的缓存。
- 更新 DI 注册使用接口。

**结果**：同一 PID 在短时间内不会被重复查询，后续刷新基本不再启动 PowerShell。

### 验证

- `dotnet build PortLens.sln` 成功，0 警告，0 错误。
- `dotnet test PortLens.sln` 45 个测试通过。
- `scripts/publish.ps1` 发布成功。
- UI 自动化验证发布后的 `PortLens.exe` 状态栏和列表显示正常。

### 进阶优化建议（阶段 D）

1. **原生 API 替代 PowerShell/CIM**：使用 `NtQuerySystemInformation` 读取进程命令行，彻底消除 PowerShell 开销。
2. **`PortEntry.Key` 使用 struct**：减少 `_entriesByKey` 字典 key 的字符串分配。
3. **`ApplyEntries` 批量 diff**：减少 `ObservableCollection` 逐个 `Remove`/`Move`/`Insert` 触发的 `CollectionChanged` 事件。
4. **进程快照字典**：在 `ProcessInspector.EnrichBasic` 中用一次 `Process.GetProcesses()` 快照替代多次 `Process.GetProcessById` 异常处理。

## 阶段 D1：原生命令行读取

### 问题
每次刷新都启动 PowerShell/CIM 读取进程命令行，子进程开销高；阶段 C 的缓存已减少部分开销，但仍依赖外部进程。

### 解决方案
将 `ProcessCommandLineReader` 改为原生 API 读取：

- **主路径**：使用 `NtQueryInformationProcess` 的 `ProcessCommandLineInformation`（info class 60）获取 `UNICODE_STRING`，再用 `ReadProcessMemory` 读取命令行字符串。
- **备用路径**：若主路径失败，使用 `ProcessBasicInformation` 获取 PEB，从 `RTL_USER_PROCESS_PARAMETERS.CommandLine`（x64 偏移 0x70）读取。
- 打开进程时使用 `PROCESS_QUERY_LIMITED_INFORMATION | PROCESS_VM_READ`。
- 保留 60 秒 TTL 缓存、按 live PID 的 Prune、CancellationToken 传播以及空白归一化。

### 验证

- `dotnet build PortLens.sln` 成功，0 警告，0 错误。
- `dotnet test PortLens.sln` 45 个测试通过。
- `scripts/publish.ps1` 发布成功。
- Smoke test 确认发布后的 `PortLens.exe` 主窗口正常显示，且未启动 `powershell.exe` 子进程。

### 风险与回滚

- 风险：受保护/系统进程无法打开或读取时返回 `null`，与之前行为一致。
- 风险：PEB 结构偏移仅限 x64；已在代码中通过 `IntPtr.Size != 8` 返回 `null` 保护。
- 回滚：还原 `ProcessCommandLineReader.cs` 到 PowerShell/CIM 版本。

## 阶段 D2：`PortEntry.Key` 使用 struct

### 问题
`PortEntry.Key` 使用字符串插值 `$"{Protocol}:{LocalAddress}:{LocalPort}:{ProcessId}"`，每次访问都会分配新字符串；`_entriesByKey`、`_matchingKeys` 等字典/集合也基于字符串比较。

### 解决方案
引入 `public readonly record struct PortEntryKey(string Protocol, string LocalAddress, int LocalPort, int ProcessId)`：

- `PortEntry.Key` 与 `PortEntryViewModel.Key` 返回 `PortEntryKey`。
- `MainWindowViewModel` 的 `_entriesByKey`、`_matchingKeys`、`liveKeys` 全部使用 `PortEntryKey`。
- 移除 `StringComparer.Ordinal`；struct 默认使用值相等。
- 新增单元测试验证相等性、哈希码、`HashSet` 去重、`Dictionary` 查找。

### 验证

- `dotnet build PortLens.sln` 成功，0 警告，0 错误。
- `dotnet test PortLens.sln` 50 个测试通过（新增 5 个）。
- `scripts/publish.ps1` 发布成功。
- Smoke test 确认主窗口正常显示。

### 风险与回滚

- 风险：遗漏某个仍使用 `string Key` 的地方导致编译或运行时错误；已通过全文搜索和测试覆盖。
- 回滚：还原 `PortEntry.cs`、`PortEntryViewModel.cs`、`MainWindowViewModel.cs` 到字符串 key。

## 阶段 D4：进程快照字典

### 问题
每次扫描中 `EnrichBasic` 对每个端口对应的 PID 调用 `Process.GetProcessById`，导致重复打开进程句柄；部分进程还会抛异常，增加开销。

### 解决方案
新增 `ProcessSnapshot` 并在扫描开始时一次性捕获：

- `ProcessSnapshot` 包含 `Id`、`ProcessName`、`StartTime`、`WorkingSet64`、`TotalProcessorTime`、`ExecutablePath`。
- `ProcessInspector.CaptureSnapshot` 调用一次 `Process.GetProcesses()`，逐个读取所需属性后立即 Dispose。
- `CpuSampler.CalculateCpu` 改为接收 `processId` 和 `TotalProcessorTime`。
- `EnrichBasic`/`EnrichDetails` 接收快照字典，直接取值；`ReadProcessDetails` 从快照获取 `ExecutablePath`。
- `PortScanner.Scan` 在 `PreloadProcessDetails` 后调用一次 `CaptureSnapshot`，并传入后续 enrich 调用。

### 验证

- `dotnet build PortLens.sln` 成功，0 警告，0 错误。
- `dotnet test PortLens.sln` 50 个测试通过。
- `scripts/publish.ps1` 发布成功。
- Smoke test 确认主窗口、状态栏 CPU/内存、版本号显示正常。

### 风险与回滚

- 风险：进程在快照后退出，数据稍旧；与之前 `Process.GetProcessById` 行为一致。
- 风险：`MainModule?.FileName` 在部分系统进程上失败，快照中 `ExecutablePath` 为 null，行为与之前一致。
- 回滚：还原 `ProcessInspector.cs`、`CpuSampler.cs`、`PortScanner.cs` 到按 PID 打开进程的实现。

## 阶段 D3：`ApplyEntries` 批量 diff

### 问题
`ApplyEntries` 对 `ObservableCollection` 逐个执行 `Remove`/`Move`/`Insert`，每次都会触发 `CollectionChanged` 事件，WPF 重新分组/过滤开销大。

### 解决方案
引入 `SuppressibleObservableCollection<T>` 并改为批量 diff：

- 自定义集合支持 suppression scope，暂停期间 `CollectionChanged` 和 `PropertyChanged` 不触发。
- `ApplyEntries` 先按目标顺序构建新的 `List<PortEntryViewModel>`（复用现有 VM）。
- 更新 `_entriesByKey` 与目标集合一致。
- 调用 `_entries.ResetTo(newEntries)`，在 suppression scope 内清空并批量添加，退出后触发单个 `Reset` 事件。
- 复用 VM 实例，保留 `IsExpanded` 状态。

### 验证

- `dotnet build PortLens.sln` 成功，0 警告，0 错误。
- `dotnet test PortLens.sln` 50 个测试通过。
- `scripts/publish.ps1` 发布成功。
- Smoke test 确认列表、分组、搜索行为正常。

### 风险与回滚

- 风险：`Reset` 事件会导致 WPF 重新生成容器，可能丢失滚动位置或产生闪烁；在虚拟化列表下影响有限。
- 风险：`_entriesByKey` 与新集合不一致；通过 diff 后统一赋值保证一致性。
- 回滚：还原 `MainWindowViewModel.cs`，移除 `SuppressibleObservableCollection`。

## 阶段 D5：同一项目下 frontend/backend 聚合分组

### 问题
用户截图显示：同一仓库 `feature-mvp` 下的 `frontend`（Vite）和 `backend`（Go）被识别为两个独立项目，而不是聚合在 `feature-mvp` 下。

### 根因
`ProjectRootResolver` 在子目录（如 `frontend`）发现 `.vscode` 等 root marker 时直接停在该子目录，没有考虑它与 `backend` 共享同一个父项目根。

### 解决方案
改进 `ProjectRootResolver.Resolve`：

- 当 marker root 的目录名属于 `frontend/backend/api/server/web` 等子项目名，且其父目录不是 `apps/packages/services` 等 workspace container 时，如果父目录也有 root marker，则提升到父目录作为项目根。
- 这样 `feature-mvp/frontend` 和 `feature-mvp/backend` 都会聚合到 `feature-mvp`。
- 保留 workspace container 语义：`apps/web` 仍识别为 `web`，不会越级到 workspace 根。

同时新增 `ComputeRelativeSubtitle`：

- 组标题显示共享父目录名（如 `feature-mvp`）。
- 副标题显示子项目相对于父目录的路径（如 `frontend` / `backend`），便于在同一组内区分。

### 验证

- `dotnet build PortLens.sln` 成功，0 警告，0 错误。
- `dotnet test PortLens.sln` 55 个测试通过（新增 5 个）。
- `scripts/publish.ps1` 发布成功。
- Smoke test 确认主窗口正常显示。

### 风险与回滚

- 风险：过度聚合，把本不该合并的项目合并在一起；目前仅对显式子项目名（frontend/backend 等）触发，且要求父目录也有 root marker。
- 风险：用户希望以子项目为单位查看；副标题保留了子项目路径，仍可区分。
- 回滚：还原 `ProjectRootResolver.cs` 和 `PortEntryViewModel.cs` 的 subtitle 计算。

---

*每执行2次查看/浏览器/搜索操作后更新此文件*
*防止视觉信息丢失*
