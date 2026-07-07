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

## 2026-07-05 全面评估：从原型到生产级应用的差距

### 当前状态

PortLens 已具备可用的核心功能：TCP 端口扫描、进程信息展示、框架推断、项目分组、搜索过滤、系统托盘、设置持久化、深色模式、中英文本地化、自动更新、CI/CD 发布。代码结构经过多轮重构，核心逻辑已拆分为单一职责类，UI 采用 MVVM + DI。

但距离真正的生产级桌面应用仍有明显差距，以下从功能、性能、健壮性、架构、测试、生产就绪六个维度分析。

### 1. 功能层面

| 问题 | 影响 | 建议 |
|------|------|------|
| 仅支持 TCP，无 UDP 扫描 | 无法发现 UDP 开发服务（如某些游戏服务器、QUIC、DNS 工具） | 扩展 `NativeTcp` 为 `NativeSocket`，调用 `GetExtendedUdpTable` |
| 无单实例限制 | 可同时启动多个 PortLens，造成设置/日志竞争和托盘图标重复 | 在 `App.OnStartup` 增加命名 Mutex + IPC 激活已运行实例 |
| 终止进程无反馈 | 用户无法确认子进程是否全部结束 | `Process.Kill` 后等待 `WaitForExit` 并报告结果 |
| 自动更新硬编码安装路径 | 自定义或便携版用户更新后无法重启 | 从 `Assembly.GetExecutingAssembly().Location` 获取真实路径 |
| 无管理员权限提示 | 部分命令行/进程无法读取，终止系统进程失败 | 检测权限并在 UI 提示，或提供“以管理员身份重启”入口 |
| 缺少导出/导入设置 | 用户换机或重装时配置丢失 | 支持 settings.json 导入导出 |
| 端口历史与趋势缺失 | 无法观察服务启停、资源变化趋势 | 在本地 SQLite/LiteDB 中保存快照，绘制趋势图 |

### 2. 性能层面

| 问题 | 影响 | 建议 |
|------|------|------|
| 每次刷新枚举所有进程 | `Process.GetProcesses()` 分配大量对象 | 仅对 TCP 表中的 PID 调用 `Process.GetProcessById` |
| 每次扫描新建大量集合 | 高刷新率下 GC 压力明显 | 使用对象池或增量更新，仅重建变更行 |
| 命令行读取未批量 | 每个 PID 单独打开句柄 | 研究 `NtQuerySystemInformation` 批量读取或保留更智能缓存 |
| 字体列表每次重建 | 设置对话框打开慢 | 缓存 `FontService.GetInstalledFontFamilies` 结果 |
| 搜索每次重建 HashSet | 快速输入时仍有分配 | 复用集合或改用位图/前缀树索引 |
| 文件日志同步写 | 高频日志阻塞 UI 线程 | 改为 `Channel` + 后台线程批量写入 |
| 无刷新暂停机制 | 隐藏窗口时仍按最低 30 秒扫描 | 最小化到托盘后可完全暂停，恢复时再刷新 |

### 3. 代码健壮性

| 问题 | 影响 | 建议 |
|------|------|------|
| 多处空 `catch` / 吞异常 | 权限/访问拒绝问题被隐藏 | 记录结构化警告，区分“权限不足”“进程已退出”“超时” |
| 原生内存读取无边界校验 | 可能读到无效内存或崩溃 | 对 `NtReadVirtualMemory` 返回值做严格校验 |
| `HttpClient` 无超时与重试 | 网络抖动导致更新检查卡死 | 使用 `IHttpClientFactory` + Polly 重试策略 |
| 托盘状态字符串解析脆弱 | 本地化或格式变化会破坏 tooltip | 在 ViewModel 中维护独立的 `TrayStatus` 对象 |
| MSI GUID 为占位符 | 升级逻辑可能异常 | 生成并固定 `UpgradeCode` 与组件 GUID |
| 自动更新无校验 | 下载的 MSI 可能被篡改 | 发布 SHA256 校验文件并在安装前验证 |
| 动态 UI 缺少自动化属性 | 屏幕阅读器无法导航 | 为 `TrayIconService`、`KillConfirmationDialog`、黑名单行添加 `AutomationProperties.Name` |

### 4. 架构层面

| 问题 | 影响 | 建议 |
|------|------|------|
| `MainWindow.xaml.cs` 仍过重 | 视图与协调逻辑混杂 | 拆出 `ShellCoordinator` 或 `AppLifecycleService` |
| `MainWindowViewModel` 职责过多 | 难以单元测试 | 拆分 `SearchService`、`GroupingService`、`StatusBarViewModel` |
| 核心服务为具体类 | ViewModel 无法脱离 OS 测试 | 引入 `IPortScanner`、`IProcessInspector` 接口 |
| 大量 UI 在代码中构建 | 设计器、本地化、可访问性差 | 将 `TrayIconService`、`KillConfirmationDialog` 改为 XAML UserControl |
| `ServiceRegistration` 把 `MainWindow` 注册进 DI | 容器与视图生命周期耦合 | 使用工厂模式或 `App` 显式创建窗口 |

### 5. 测试层面

| 问题 | 影响 | 建议 |
|------|------|------|
| 仅覆盖过滤/推断逻辑 | 核心扫描、进程读取、ViewModel 均无测试 | 为 `PortScanner`、`ProcessInspector`、`ProcessCommandLineReader`、`CpuSampler` 增加可测试抽象与单元测试 |
| 无 UI/集成测试 | 手动 smoke test 容易遗漏回归 | 引入 FlaUI/Appium 对设置、搜索、托盘做自动化断言 |
| 无性能基准 | 优化无法量化 | 增加 BenchmarkDotNet 基准，监控扫描耗时与内存分配 |

### 6. 生产就绪差距

| 问题 | 影响 | 建议 |
|------|------|------|
| 无崩溃报告与遥测 | 线上问题难以定位 | 集成 Sentry、AppCenter 或 Windows Error Reporting |
| 无代码签名 | SmartScreen 拦截，用户信任度低 | 申请证书并对 EXE/MSI 签名 |
| 仅支持两种语言 | 国际化用户受限 | 建立社区翻译流程，增加更多 resx |
| 无可访问性声明 | 企业/政府场景不可用 | 补齐自动化属性、高对比度支持、键盘导航 |
| 无离线文档/帮助 | 新用户上手成本高 | 增加内置快捷键说明、工具提示、FAQ 链接 |
| CI 只跑 Release | 某些问题在 Debug 下更早暴露 | PR 工作流同时跑 Debug + Release |

### 优先级建议

**高优先级（影响可用性或安全）**
1. 单实例限制
2. 自动更新路径与 MSI 校验
3. 稳定的 MSI GUID 与升级策略
4. 异常分类与日志增强
5. HttpClient 超时/重试

**中优先级（体验与维护）**
1. UDP 端口支持
2. 增量刷新与性能基准
3. 核心接口抽象与 ViewModel 拆分
4. 自动化 UI 测试
5. 导出/导入设置

**低优先级（锦上添花）**
1. 更多语言
2. 历史趋势图
3. 崩溃遥测
4. 代码签名

### 结论

PortLens 在功能上已经能满足个人开发者的日常需求，代码结构也比初期清晰很多。但要成为可广泛分发的生产级应用，还需要在**安装升级安全、权限处理、异常可观测性、UI 可访问性、自动化测试**五个方面补齐短板。建议先完成高优先级项，再逐步向中优先级扩展。

---

---

## 2026-07-07 极端性能优化分析

### 分析范围

本次分析从系统调用、内存分配、线程阻塞、UI 渲染、网络 I/O、缓存策略六个维度对 PortLens 进行深度审查，目标是在保持功能稳定的前提下进一步压低 CPU、内存、延迟和子进程开销。分析覆盖：

- `PortLens.Core/Services/PortScanner.cs` 扫描管线
- `ProcessInspector.cs` / `ProcessCommandLineReader.cs` / `ProcessCurrentDirectoryReader.cs` / `ProcessTreeReader.cs` 进程信息读取
- `FrameworkDetector.cs` / `ProjectNameResolver.cs` / `ProjectRootResolver.cs` 推断逻辑
- `MainWindow.xaml.cs` / `MainWindowViewModel.cs` / `TrayIconService.cs` UI 与调度
- `FileLogger.cs` / `UpdateCheckService.cs` / `AutoUpdateService.cs` I/O 与网络
- `SettingsDialog.xaml.cs` / `FontService.cs` 设置对话框与字体枚举

### 关键发现（按影响程度排序）

#### 1. 【致命】`ProcessTreeReader` 仍通过 PowerShell/CIM 读取进程树

- **位置**：`work/PortLens.Core/Services/ProcessTreeReader.cs:105-138`
- **问题**：每次缓存过期（60s）就启动 `powershell.exe`，执行 `Get-CimInstance Win32_Process | Select-Object ProcessId,ParentProcessId | ConvertTo-Json -Compress`，再 JSON 解析。一个 `powershell.exe` 启动就约 80-150ms、数十 MB 内存，且与命令行读取器分开执行，无法共享快照。
- **影响**：在 5s 刷新间隔下，每 12 次扫描会触发一次；若用户快速刷新或首次扫描，立刻spawn 子进程。Windows Defender/AMSI 还会进一步拖慢。
- **优化方向**：
  - 使用 `NtQuerySystemInformation` + `SystemProcessInformation`（info class 5）一次性读取系统中所有进程的 PID、Parent PID、句柄数等，0 子进程开销。
  - 或先用 P/Invoke 到 `CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS)` 遍历进程树，也比 PowerShell 快 1-2 个数量级。
  - 与 `ProcessCurrentDirectoryReader` 共享同一个进程枚举快照，避免重复系统调用。
- **预期收益**：消除最后一个外部子进程，扫描期间 `powershell.exe` 出现次数降为 0；首次扫描延迟降低 100-300ms。

#### 2. 【高】`FileLogger` 同步写文件且全局加锁

- **位置**：`work/PortLens.Desktop/Services/FileLogger.cs:47-78`
- **问题**：所有 Warning/Error 日志都走 `lock (Lock)` 后调用 `File.AppendAllText`。该锁是进程全局的，任何扫描线程或 UI 线程日志都会串行阻塞；高频扫描下竞争明显。
- **影响**：日志高峰时可能阻塞扫描线程数十毫秒，极端情况下拖慢刷新。
- **优化方向**：
  - 改用 `System.Threading.Channels.Channel<string>` + 单后台线程批量写入。
  - 后台线程每秒 flush 一次，或队列满 100 条时 flush，减少文件 I/O 次数。
  - 保留“日志不能抛异常”的兜底行为。
- **预期收益**：日志写入从同步阻塞变为异步排队，扫描线程零阻塞；I/O 次数降低 5-20 倍。

#### 3. 【高】`HttpClient` 默认 100s 超时且无重试

- **位置**：`work/PortLens.Desktop/ServiceRegistration.cs:29-30`、`UpdateCheckService.cs:22-53`、`AutoUpdateService.cs:34-55`
- **问题**：`AddHttpClient<UpdateCheckService>()` 未配置 `Timeout`、`PolicyHandler` 或 `IHttpClientFactory` 命名客户端。GitHub API 抖动或企业代理下，更新检查可能挂 100s；自动更新下载失败后没有重试。
- **影响**：首次启动后 `Loaded` 事件触发更新检查，若网络异常会导致启动窗口无响应 100s；下载 MSI 失败后用户体验差。
- **优化方向**：
  - 配置 `HttpClient.Timeout = TimeSpan.FromSeconds(15)`。
  - 引入 Polly 策略：更新检查重试 2 次（指数退避），下载重试 1 次。
  - `UpdateCheckService.CheckAsync` 始终携带 `CancellationToken` 并在 `App.OnStartup` 阶段超时可控。
- **预期收益**：网络异常时最长等待从 100s 降至 <30s，启动不会被卡死。

#### 4. 【高】`ProcessInspector.CaptureSnapshot` 枚举所有进程

- **位置**：`work/PortLens.Core/Services/ProcessInspector.cs:40-78`
- **问题**：每次扫描调用 `Process.GetProcesses()`，返回系统中全部 `Process` 对象并逐一 Dispose。虽然只保留 TCP 表中的 PID，但仍为所有进程分配对象和句柄。
- **影响**：在进程数 300+ 的系统上，每次扫描分配数万个对象，增加 GC 压力。
- **优化方向**：
  - 改为仅对 `liveProcessIds` 调用 `Process.GetProcessById`，并用 `try/catch` 处理已退出进程；这减少大部分对象分配。
  - 更进一步可改为 `NtQuerySystemInformation(SystemProcessInformation)` 一次性读取全部进程属性，避免 `Process` 对象和重复句柄。
  - 与进程树读取器共享同一个原生快照。
- **预期收益**：扫描内存分配降低 30-60%，GC 频率下降。

#### 5. 【中】`FontService.GetInstalledFontFamilies` 每次设置对话框都重新枚举

- **位置**：`work/PortLens.Desktop/Services/FontService.cs:9-17`、`SettingsDialog.xaml.cs:123-152`
- **问题**：打开设置对话框时创建 `InstalledFontCollection`、读取系统字体、排序，生成 ComboBoxItem。系统字体多时耗时 50-200ms。
- **影响**：设置对话框打开延迟明显，且每次都重复。
- **优化方向**：
  - 在 `FontService` 中缓存字体列表（系统字体在会话期间基本不变），或使用 `Lazy<IReadOnlyList<string>>`。
  - ComboBox 使用虚拟化（`VirtualizingStackPanel.IsVirtualizing="True"`），避免一次性实例化上千个 ComboBoxItem。
- **预期收益**：设置对话框打开时间从 100-300ms 降至 <20ms。

#### 6. 【中】`FrameworkDetector.InferFramework` 每次调用都分配大字符串

- **位置**：`work/PortLens.Core/Services/FrameworkDetector.cs:12`
- **问题**：`var text = $"{entry.ProcessName} {entry.CommandLine} {entry.WorkingDirectory} {entry.ExecutablePath}".ToLowerInvariant();` 把四条字符串拼接成一条大写字符串再 ToLower，每次 `EnrichDetails` 都执行。
- **影响**：长命令行（如 Spring Boot/Maven）时频繁分配大字符串，且包含大量不必检查的目录路径。
- **优化方向**：
  - 改为按优先级分段匹配：先匹配 `ProcessName`，再匹配 `CommandLine`，命中即返回，避免拼接和 ToLower。
  - 对已知框架使用 `ReadOnlySpan<char>` 和 `MemoryExtensions.Contains`（.NET 10 支持），减少分配。
  - 将框架规则预编译为 `SearchValues<string>` 或 `Aho-Corasick` 字典，一次扫描即可多模式匹配。
- **预期收益**：每个端口的框架推断分配从 O(n) 字符串降至接近 0；扫描吞吐量提升 10-20%。

#### 7. 【中】`ProjectRootResolver.HasRootMarker` 深度路径重复文件系统探测

- **位置**：`work/PortLens.Core/Services/ProjectRootResolver.cs:143-180`
- **问题**：对每个工作目录从叶子到根逐层检查 `.git`、`.idea`、`.vscode`、`pnpm-workspace.yaml`、`go.mod`、*.sln、package.json 等 marker。深层路径可能触发 10-20 次文件系统调用，且同一目录在不同条目间重复探测。
- **影响**：文件系统调用是扫描中除原生 API 外最慢的操作之一，尤其机械硬盘/网络驱动器。
- **优化方向**：
  - 添加目录级 TTL 缓存（如 30s），缓存 `DirectoryInfo.FullName -> DirectoryInfo? rootMarker`，避免重复探测。
  - 调整 marker 检查顺序，把 `.git`、`.idea`、`.sln` 等高命中 marker 放前面，低命中放后面。
  - 异步预热常用根目录（从上次扫描结果中复用）。
- **预期收益**：文件系统调用减少 50-80%，扫描延迟降低。

#### 8. 【中】`MainWindowViewModel.ApplyEntries` 仍新建 `List<PortEntryViewModel>` 和 `HashSet<PortEntryKey>`

- **位置**：`work/PortLens.Desktop/ViewModels/MainWindowViewModel.cs:336-363`
- **问题**：每次扫描都新建 `liveKeys` HashSet 和 `ordered` List。虽然 diff 逻辑已优化为批量 Reset，但仍产生大量中间集合。
- **优化方向**：
  - 复用静态字段或线程本地 `HashSet<PortEntryKey>`、`List<PortEntryViewModel>`，扫描结束后 `Clear()`。
  - 在 `ApplyEntries` 中先计算变更集，再调用 `ResetTo`，避免构建完整 `ordered` 列表。
- **预期收益**：5s 刷新下 GC 压力进一步下降，高频刷新更稳定。

#### 9. 【中】`ProcessCurrentDirectoryReader` 对父/祖父进程重复 WMI 查询

- **位置**：`work/PortLens.Core/Services/ProcessCurrentDirectoryReader.cs:135-179`
- **问题**：`ReadFromWmi` 和 `GetParentProcessId` 各触发一次 WMI/ManagementObjectSearcher 查询。读取当前目录时若进入父/祖父 fallback，最多 6 次 WMI 查询。
- **影响**：WMI 查询慢且有 COM 初始化开销，多个条目同时 fallback 时显著拖慢扫描。
- **优化方向**：
  - 与进程树读取器共享同一个原生进程快照，从快照中直接取 ParentProcessId，无需 WMI。
  - 对 WMI 查询结果添加 5-10s 进程级缓存。
- **预期收益**：Java/Spring Boot 等需要父目录回退的场景扫描延迟降低。

#### 10. 【低-中】UI 层面仍有可优化点

- **位置**：`TrayIconService.cs:94-140`、`SettingsDialog.xaml.cs:233-258`
- **问题**：
  - 每次右键托盘都重新构建 `ContextMenu` 和全部 `MenuItem`。
  - 设置对话框 About 页每次打开都重新下载 shields.io 徽章图片（BitmapImage 无缓存）。
- **优化方向**：
  - 托盘菜单缓存一份，仅在状态变化时更新可用性/文案。
  - 徽章图片本地缓存到 `%LocalAppData%/PortLens/badges/` 或应用内内存缓存。
- **预期收益**：UI 交互响应更快，减少网络请求。

#### 11. 【低】命令行空白归一化使用 Compiled Regex

- **位置**：`work/PortLens.Core/Services/ProcessCommandLineReader.cs:24、242-250`
- **问题**：`CommandLineRegex = new(@"\s+", RegexOptions.Compiled)` 对每个命令行执行 `Regex.Replace`。虽然 Compiled 已优化，但仍比简单循环慢。
- **优化方向**：改为自定义 `NormalizeCommandLine` 方法，使用 `StringBuilder` 或 `ValueStringBuilder` 手动合并连续空白。
- **预期收益**：命令行读取微优化，单条节省微秒级，扫描量大时累计可感。

#### 12. 【低】`AppMetricsTimer` 每秒运行，即使窗口隐藏

- **位置**：`work/PortLens.Desktop/MainWindow.xaml.cs:92-94`
- **问题**：CPU/内存指标计时器每秒触发，无论窗口是否可见或最小化到托盘。
- **优化方向**：在窗口隐藏/最小化时暂停 `_appMetricsTimer`，恢复时立即更新一次。
- **预期收益**：后台资源占用微降，笔记本电脑续航受益。

### 优先级矩阵

| 优先级 | 项目 | 预期收益 | 改动风险 | 建议实施顺序 |
|--------|------|----------|----------|--------------|
| P0 | 用原生 API 替换 `ProcessTreeReader` 的 PowerShell | 消除子进程、显著降低延迟 | 中（需保证 x64 稳定） | 1 |
| P0 | `FileLogger` 异步化 | 消除全局锁、避免阻塞扫描 | 低 | 2 |
| P1 | `HttpClient` 超时/重试策略 | 防止启动/更新卡死 | 低 | 3 |
| P1 | 进程快照改用原生枚举/按需 `GetProcessById` | 降低内存分配 | 中 | 4 |
| P1 | 字体列表缓存 + ComboBox 虚拟化 | 设置对话框秒开 | 低 | 5 |
| P2 | `FrameworkDetector` 避免大字符串拼接 | 降低分配、提升推断速度 | 低 | 6 |
| P2 | `ProjectRootResolver` 目录 marker 缓存 | 减少文件系统调用 | 低 | 7 |
| P2 | 复用 diff 集合对象 | 降低 GC 压力 | 低 | 8 |
| P2 | 共享进程快照减少 WMI 回退查询 | 降低 WMI 开销 | 中 | 9 |
| P3 | UI 托盘菜单/徽章缓存 | 交互响应优化 | 低 | 10 |
| P3 | 命令行归一化无 Regex | 微优化 | 低 | 11 |
| P3 | AppMetricsTimer 后台暂停 | 微降资源占用 | 低 | 12 |

### 实施建议

1. **先打两口深井**：P0 的进程树原生化和日志异步化会带来最显著的质变，建议先做。
2. **为性能建立度量**：新增 BenchmarkDotNet 基准（`PortLens.Core.Benchmarks`），重点测量 `PortScanner.Scan` 单次耗时、内存分配、GC 次数，避免“感觉优化”。
3. **保持向后兼容**：所有原生 API 读取失败时回退到现有行为；`ProcessTreeReader` 可保留 PowerShell 作为最终 fallback，默认走原生路径。
4. **分阶段提交**：每完成一个 P0/P1 项就构建/测试/提交，避免一次性大改难以回归定位。

### 风险与回滚

- **风险**：`NtQuerySystemInformation` 的 `SystemProcessInformation` 结构在不同 Windows 版本上有字段偏移差异，但 x64 上基本稳定；仍建议用单元测试和 smoke test 验证。
- **风险**：日志异步化后崩溃前最后几条日志可能丢失；可添加 `AppDomain.UnhandledException` 中强制 flush。
- **风险**：进程枚举改为按需后，某些系统进程无法打开导致数据缺失；行为与之前一致，可接受。
- **回滚**：保留旧实现文件副本或在 git 历史中回退。

---

*每执行2次查看/浏览器/搜索操作后更新此文件*
*防止视觉信息丢失*
