# 进度日志

## 会话：2026-07-07

### 阶段：极端性能优化分析
- **状态：** complete
- **开始时间：** 2026-07-07
- **完成时间：** 2026-07-07
- 执行的操作：
  - 读取 `ProcessTreeReader.cs`、`FileLogger.cs`、`PortScanner.cs`、`ProcessInspector.cs`、`MainWindowViewModel.cs`、`MainWindow.xaml.cs`、`ServiceRegistration.cs`、`FontService.cs`、`UpdateCheckService.cs`、`AutoUpdateService.cs`、`FrameworkDetector.cs`、`ProjectRootResolver.cs` 等关键文件
  - 识别出 12 项性能优化点，按影响程度排序
  - 更新 `findings.md`，新增“2026-07-07 极端性能优化分析”章节
  - 更新 `task_plan.md`，新增 P0/P1/P2/P3/P4 优化阶段
- 关键发现：
  - `ProcessTreeReader` 仍通过 PowerShell/CIM 读取进程树，是最大瓶颈
  - `FileLogger` 同步写文件并全局加锁，会阻塞扫描线程
  - `HttpClient` 默认 100s 超时且无重试，可能导致启动/更新卡死
  - `ProcessInspector.CaptureSnapshot` 全枚举所有进程对象
  - `FontService` 每次打开设置对话框都重新枚举系统字体
  - `FrameworkDetector` 每次推断都分配大字符串
  - `ProjectRootResolver` 对同一目录重复进行多次文件系统探测
- 下一步：
  - 实施 P0-2：`FileLogger` 异步化

### 阶段 P1-3：字体列表缓存与 ComboBox 虚拟化
- **状态：** complete
- **开始时间：** 2026-07-07
- **完成时间：** 2026-07-07
- 执行的操作：
  - 将 `FontService.GetInstalledFontFamilies` 改为 `Lazy<IReadOnlyList<string>>`，会话期间只枚举一次系统字体
  - 在 `SettingsDialog.xaml` 中为中文/英文字体 ComboBox 启用 `VirtualizingStackPanel.IsVirtualizing` 和 `VirtualizationMode="Recycling"`
  - 重写 `BuildFontCombo`：使用数据项列表（`FontOption` 记录）并设置 `ItemsSource`/`DisplayMemberPath`/`SelectedValuePath`，避免创建上千个 `ComboBoxItem`
  - 更新 `GetSelectedFont` 读取 `SelectedValue`
  - 运行 `dotnet build PortLens.sln` 验证（0 警告，0 错误）
  - 运行 `dotnet test PortLens.sln` 验证（61 个测试全部通过）
  - 运行 `scripts/publish.ps1` 发布
  - 运行 `scripts/smoke-test.ps1` 验证发布后的 `PortLens.exe` 窗口正常显示
- 创建/修改的文件：
  - `work/PortLens.Desktop/Services/FontService.cs`
  - `work/PortLens.Desktop/Dialogs/SettingsDialog.xaml`
  - `work/PortLens.Desktop/Dialogs/SettingsDialog.xaml.cs`
  - `task_plan.md`
  - `progress.md`
- 测试结果：
  | 测试 | 输入 | 预期结果 | 实际结果 | 状态 |
  |------|------|---------|---------|------|
  | 构建 | `dotnet build PortLens.sln` | 成功 | 0 警告 0 错误 | 通过 |
  | 单元测试 | `dotnet test PortLens.sln` | 全部通过 | 61 个测试通过，0 失败 | 通过 |
  | 发布 exe 启动 | `scripts/smoke-test.ps1` | 窗口正常显示 | PID=13896，Children=0，Smoke test passed | 通过 |
- 下一步：
  - 实施 P2-1：`FrameworkDetector` 避免大字符串拼接

### 阶段 P1-2：进程快照按需枚举或原生化
- **状态：** complete
- **开始时间：** 2026-07-07
- **完成时间：** 2026-07-07
- 执行的操作：
  - 扩展 `IProcessTreeReader` 接口：新增 `TryGetProcessName`，从进程树快照获取进程名
  - `ProcessTreeReader` 的 `Snapshot` 增加 `ProcessNames`，并在原生快照命中时提供名称
  - `PowerShellProcessTreeReader` 实现 `TryGetProcessName` 返回 false（无名称信息）
  - 重写 `ProcessInspector.CaptureSnapshot`：仅对 TCP 表中的 live PID 调用 `Process.GetProcessById`，失败时回退到进程树快照中的进程名
  - 彻底移除 `Process.GetProcesses()` 全枚举，减少对象分配
  - 运行 `dotnet build PortLens.sln` 验证（0 警告，0 错误）
  - 运行 `dotnet test PortLens.sln` 验证（61 个测试全部通过）
  - 运行 `scripts/publish.ps1` 发布
  - 运行 `scripts/smoke-test.ps1` 验证发布后的 `PortLens.exe` 窗口正常显示
- 创建/修改的文件：
  - `work/PortLens.Core/Services/ProcessInspector.cs`
  - `work/PortLens.Core/Services/ProcessTreeReader.cs`
  - `work/PortLens.Core/Services/PowerShellProcessTreeReader.cs`
  - `task_plan.md`
  - `progress.md`
- 测试结果：
  | 测试 | 输入 | 预期结果 | 实际结果 | 状态 |
  |------|------|---------|---------|------|
  | 构建 | `dotnet build PortLens.sln` | 成功 | 0 警告 0 错误 | 通过 |
  | 单元测试 | `dotnet test PortLens.sln` | 全部通过 | 61 个测试通过，0 失败 | 通过 |
  | 发布 exe 启动 | `scripts/smoke-test.ps1` | 窗口正常显示 | PID=7260，Children=0，Smoke test passed | 通过 |
- 下一步：
  - 实施 P1-3：字体列表缓存与 ComboBox 虚拟化

### 阶段 P1-1：`HttpClient` 超时与重试策略
- **状态：** complete
- **开始时间：** 2026-07-07
- **完成时间：** 2026-07-07
- 执行的操作：
  - 在 `PortLens.Desktop.csproj` 添加 `Microsoft.Extensions.Http.Polly` 包
  - 在 `ServiceRegistration.cs` 中为 `UpdateCheckService` 配置 15s 超时 + 2 次指数退避重试（1s/2s）
  - 为 `AutoUpdateService` 配置 60s 超时 + 1 次重试
  - 重试策略覆盖 `HttpRequestException` 与瞬态 HTTP 错误（5xx/408/429）
  - `UpdateCheckService.CheckAsync` 与 `AutoUpdateService.DownloadMsiAsync` 已支持 `CancellationToken`
  - 运行 `dotnet build PortLens.sln` 验证（0 警告，0 错误）
  - 运行 `dotnet test PortLens.sln` 验证（61 个测试全部通过）
  - 运行 `scripts/publish.ps1` 发布
  - 运行 `scripts/smoke-test.ps1` 验证发布后的 `PortLens.exe` 窗口正常显示
- 创建/修改的文件：
  - `work/PortLens.Desktop/PortLens.Desktop.csproj`
  - `work/PortLens.Desktop/ServiceRegistration.cs`
  - `task_plan.md`
  - `progress.md`
- 测试结果：
  | 测试 | 输入 | 预期结果 | 实际结果 | 状态 |
  |------|------|---------|---------|------|
  | 构建 | `dotnet build PortLens.sln` | 成功 | 0 警告 0 错误 | 通过 |
  | 单元测试 | `dotnet test PortLens.sln` | 全部通过 | 61 个测试通过，0 失败 | 通过 |
  | 发布 exe 启动 | `scripts/smoke-test.ps1` | 窗口正常显示 | PID=27032，Children=0，Smoke test passed | 通过 |
- 下一步：
  - 实施 P1-2：进程快照按需枚举或原生化

### 阶段 P0-2：`FileLogger` 异步化
- **状态：** complete
- **开始时间：** 2026-07-07
- **完成时间：** 2026-07-07
- 执行的操作：
  - 重写 `FileLoggerProvider`：使用 `Channel<string>` 将日志行入队，单后台线程批量写入文件
  - 后台写入循环每次最多批量 flush 100 条，使用异步 `FileStream` + `StreamWriter`
  - `Dispose` 时完成 Channel 并等待后台线程最多 2 秒，确保退出前 flush
  - 在 `App.xaml.cs` 的 `OnExit` 和 `AppDomain.CurrentDomain.UnhandledException` 中调用 `FlushLogs`，避免崩溃或正常退出时丢失日志
  - 在 `ServiceRegistration.cs` 中将 `FileLoggerProvider` 注册为单例，供 `App` 直接 flush
  - 运行 `dotnet build PortLens.sln` 验证（0 警告，0 错误）
  - 运行 `dotnet test PortLens.sln` 验证（61 个测试全部通过）
  - 运行 `scripts/publish.ps1` 发布
  - 运行 `scripts/smoke-test.ps1` 验证发布后的 `PortLens.exe` 窗口正常显示且未启动 `powershell.exe` 子进程
- 创建/修改的文件：
  - `work/PortLens.Desktop/Services/FileLogger.cs`
  - `work/PortLens.Desktop/App.xaml.cs`
  - `work/PortLens.Desktop/ServiceRegistration.cs`
  - `task_plan.md`
  - `progress.md`
- 测试结果：
  | 测试 | 输入 | 预期结果 | 实际结果 | 状态 |
  |------|------|---------|---------|------|
  | 构建 | `dotnet build PortLens.sln` | 成功 | 0 警告 0 错误 | 通过 |
  | 单元测试 | `dotnet test PortLens.sln` | 全部通过 | 61 个测试通过，0 失败 | 通过 |
  | 发布 exe 启动 | `scripts/smoke-test.ps1` | 窗口正常显示 | PID=13948，Children=0，Smoke test passed | 通过 |
- 下一步：
  - 实施 P1-1：`HttpClient` 超时与重试策略

### 阶段 P0-1：进程树读取原生 API 化
- **状态：** complete
- **开始时间：** 2026-07-07
- **完成时间：** 2026-07-07
- 执行的操作：
  - 新增 `NativeProcessSnapshot`，使用 `NtQuerySystemInformation(SystemProcessInformation)` 一次性读取所有进程 PID、Parent PID、进程名
  - 重写 `ProcessTreeReader`：默认走原生快照，失败时回退到 `PowerShellProcessTreeReader`
  - 新增 `PowerShellProcessTreeReader` 作为 fallback，保留原有 PowerShell/CIM 行为
  - 更新 `ProcessCurrentDirectoryReader`，新增传入 `IReadOnlyDictionary<int,int>` 的重载，优先从原生快照获取父进程 PID，减少 WMI 查询
  - 更新 `ProcessInspector`，在读取进程详情时传递父进程映射快照
  - 运行 `dotnet build PortLens.sln` 验证（0 警告，0 错误）
  - 运行 `dotnet test PortLens.sln` 验证（61 个测试全部通过）
  - 运行 `scripts/publish.ps1` 发布
  - 运行 `scripts/smoke-test.ps1` 验证发布后的 `PortLens.exe` 窗口正常显示且未启动 `powershell.exe` 子进程
- 创建/修改的文件：
  - `work/PortLens.Core/Services/NativeProcessSnapshot.cs`（新增）
  - `work/PortLens.Core/Services/PowerShellProcessTreeReader.cs`（新增）
  - `work/PortLens.Core/Services/ProcessTreeReader.cs`
  - `work/PortLens.Core/Services/ProcessCurrentDirectoryReader.cs`
  - `work/PortLens.Core/Services/ProcessInspector.cs`
  - `task_plan.md`
  - `progress.md`
- 测试结果：
  | 测试 | 输入 | 预期结果 | 实际结果 | 状态 |
  |------|------|---------|---------|------|
  | 构建 | `dotnet build PortLens.sln` | 成功 | 0 警告 0 错误 | 通过 |
  | 单元测试 | `dotnet test PortLens.sln` | 全部通过 | 61 个测试通过，0 失败 | 通过 |
  | 发布 exe 启动 | `scripts/smoke-test.ps1` | 窗口正常显示且无 PowerShell 子进程 | PID=4756，Children=0，Smoke test passed | 通过 |
- 下一步：
  - 实施 P0-2：`FileLogger` 异步化

---


### 阶段 1：工程基础建设
- **状态：** complete
- **开始时间：** 2026-07-03
- **完成时间：** 2026-07-03
- 执行的操作：
  - 创建 `PortLens.sln`
  - 创建 `Directory.Build.props`
  - 创建 `global.json`
  - 创建 `.editorconfig`
  - 简化 `work/PortLens.Core/PortLens.Core.csproj`
  - 简化 `work/PortLens.Desktop/PortLens.Desktop.csproj`
  - 运行 `dotnet build PortLens.sln` 验证构建成功
- 创建/修改的文件：
  - `PortLens.sln`
  - `Directory.Build.props`
  - `global.json`
  - `.editorconfig`
  - `work/PortLens.Core/PortLens.Core.csproj`
  - `work/PortLens.Desktop/PortLens.Desktop.csproj`

### 阶段 2：核心代码重构（高优先级）
- **状态：** complete
- **开始时间：** 2026-07-03
- **完成时间：** 2026-07-03
- 执行的操作：
  - 拆分 `ProcessInspector.cs` 为多个职责单一的类
  - 简化 `ProcessInspector` 并使用 `ConcurrentDictionary`
  - 创建 `MainWindowViewModel` 并迁移业务逻辑
  - 创建 `ServiceRegistration` 和 DI 容器
  - 更新 `App.xaml.cs` 使用 DI 启动
  - 修复编译错误（可访问性、事件处理器、歧义引用、DI 接口）
  - 运行 `dotnet build PortLens.sln` 验证
  - 运行 `powershell.exe ./scripts/publish.ps1` 验证发布
  - 修复发布后双击无反应的问题：DI 无法自动解析 `Func<string, Task>`，改为手动注册 `MainWindowViewModel`
- 创建/修改的文件：
  - `work/PortLens.Core/Services/CpuSampler.cs`
  - `work/PortLens.Core/Services/FrameworkDetector.cs`
  - `work/PortLens.Core/Services/ProjectNameResolver.cs`
  - `work/PortLens.Core/Services/ProcessCommandLineReader.cs`
  - `work/PortLens.Core/Services/ProcessCurrentDirectoryReader.cs`
  - `work/PortLens.Core/Services/ProcessTreeReader.cs`
  - `work/PortLens.Core/Services/ProcessInspector.cs`
  - `work/PortLens.Desktop/ViewModels/MainWindowViewModel.cs`
  - `work/PortLens.Desktop/ViewModels/PortEntryViewModel.cs`
  - `work/PortLens.Desktop/MainWindow.xaml.cs`
  - `work/PortLens.Desktop/App.xaml`
  - `work/PortLens.Desktop/App.xaml.cs`
  - `work/PortLens.Desktop/ServiceRegistration.cs`
  - `work/PortLens.Desktop/PortLens.Desktop.csproj`

### 阶段 3：性能优化（中优先级）
- **状态：** complete
- **开始时间：** 2026-07-03
- **完成时间：** 2026-07-03
- 执行的操作：
  - 优化 `ProcessCommandLineReader.ReadMany()`：CIM 查询按 PID 过滤，避免拉取全部进程
  - 为空 PID 集合增加短路返回
  - 为 `PortScanner.Scan`、`ProcessInspector` 公共方法添加 `CancellationToken` 参数
  - 为 `ProcessCommandLineReader` 注册取消回调，结束 PowerShell 子进程
  - 在 `MainWindowViewModel` 中实现搜索防抖（150ms）和后台匹配计算
  - 为 `PortEntry` 和 `PortEntryViewModel` 增加 `Key` 属性以支持预计算匹配
  - `RefreshAsync` 使用 `CancellationTokenSource` 取消重叠扫描
  - 运行 `dotnet build PortLens.sln` 验证
  - 运行 `powershell.exe ./scripts/publish.ps1` 验证发布
  - 验证发布后的 `PortLens.exe` 可正常启动
- 创建/修改的文件：
  - `work/PortLens.Core/Models/PortEntry.cs`
  - `work/PortLens.Core/Services/ProcessCommandLineReader.cs`
  - `work/PortLens.Core/Services/ProcessInspector.cs`
  - `work/PortLens.Core/Services/PortScanner.cs`
  - `work/PortLens.Desktop/ViewModels/MainWindowViewModel.cs`
  - `work/PortLens.Desktop/ViewModels/PortEntryViewModel.cs`

### 阶段 4：可维护性与质量（中优先级）
- **状态：** complete
- **开始时间：** 2026-07-03
- **完成时间：** 2026-07-03
- 执行的操作：
  - 创建 `Themes/PortLensColors.xaml` 和 `Themes/PortLensStyles.xaml`，集中管理颜色与样式
  - 在 `App.xaml` 中合并主题资源字典
  - 将 `MainWindow.xaml` 和 `SettingsDialog.xaml` 中的硬编码颜色替换为资源引用
  - 为 `DesktopSettings` 增加 `Version` 字段，移除 `NormalizeEnabledFrameworks` 中的临时迁移逻辑
  - 在 `DesktopSettingsStore` 中实现基于版本的设置迁移
  - 引入 `Microsoft.Extensions.Logging`，新增 `FileLogger` 写入本地日志文件
  - 在 `App.xaml.cs` 中注册全局未处理异常处理器
  - 将 `ProcessCommandLineReader`、`ProcessTreeReader`、`ProcessCurrentDirectoryReader` 改为实例类并通过 DI 注入
  - `ProcessInspector` 和 `PortScanner` 改为接收依赖注入
  - 为原先静默吞掉异常的 `Safe` 辅助方法添加 Warning 日志
  - 运行 `dotnet build PortLens.sln` 验证
  - 运行 `powershell.exe ./scripts/publish.ps1` 验证发布
  - 验证发布后的 `PortLens.exe` 可正常启动
- 创建/修改的文件：
  - `work/PortLens.Core/PortLens.Core.csproj`
  - `work/PortLens.Desktop/PortLens.Desktop.csproj`
  - `work/PortLens.Desktop/Themes/PortLensColors.xaml`
  - `work/PortLens.Desktop/Themes/PortLensStyles.xaml`
  - `work/PortLens.Desktop/App.xaml`
  - `work/PortLens.Desktop/MainWindow.xaml`
  - `work/PortLens.Desktop/Dialogs/SettingsDialog.xaml`
  - `work/PortLens.Desktop/Settings/DesktopSettings.cs`
  - `work/PortLens.Desktop/Settings/DesktopSettingsStore.cs`
  - `work/PortLens.Desktop/Services/FileLogger.cs`
  - `work/PortLens.Desktop/App.xaml.cs`
  - `work/PortLens.Desktop/ServiceRegistration.cs`
  - `work/PortLens.Desktop/ViewModels/MainWindowViewModel.cs`
  - `work/PortLens.Core/Services/ProcessCommandLineReader.cs`
  - `work/PortLens.Core/Services/ProcessTreeReader.cs`
  - `work/PortLens.Core/Services/ProcessCurrentDirectoryReader.cs`
  - `work/PortLens.Core/Services/ProcessInspector.cs`
  - `work/PortLens.Core/Services/PortScanner.cs`

### 阶段 5：测试与 CI/CD
- **状态：** complete
- **开始时间：** 2026-07-03
- **完成时间：** 2026-07-03
- 执行的操作：
  - 将 `ProjectRootResolver` 从 `PortLens.Desktop` 移到 `PortLens.Core`，并更新 `PortEntryViewModel` 引用
  - 将 `FrameworkDetector`、`ProjectNameResolver`、`TcpRow` 改为 public 以支持测试
  - 从 `PortScanner` 提取纯过滤/排序逻辑到 `PortScannerFilters`
  - 创建 `work/PortLens.Core.Tests` xUnit 测试项目
  - 添加 `FrameworkDetectorTests`、`ProjectNameResolverTests`、`ProjectRootResolverTests`、`PortScannerFiltersTests`
  - 将测试项目加入 `PortLens.sln`
  - 运行 `dotnet test PortLens.sln`，45 个测试全部通过
  - 创建 `.github/workflows/ci.yml`，实现 PR/Push 触发构建、测试、发布产物并上传 artifact
  - 运行 `powershell.exe ./scripts/publish.ps1` 验证发布
- 创建/修改的文件：
  - `work/PortLens.Core/Services/ProjectRootResolver.cs`
  - `work/PortLens.Core/Services/PortScannerFilters.cs`
  - `work/PortLens.Core/Services/FrameworkDetector.cs`
  - `work/PortLens.Core/Services/ProjectNameResolver.cs`
  - `work/PortLens.Core/Services/NativeTcp.cs`
  - `work/PortLens.Core/Services/PortScanner.cs`
  - `work/PortLens.Core.Tests/PortLens.Core.Tests.csproj`
  - `work/PortLens.Core.Tests/FrameworkDetectorTests.cs`
  - `work/PortLens.Core.Tests/ProjectNameResolverTests.cs`
  - `work/PortLens.Core.Tests/ProjectRootResolverTests.cs`
  - `work/PortLens.Core.Tests/PortScannerFiltersTests.cs`
  - `work/PortLens.Desktop/ViewModels/PortEntryViewModel.cs`
  - `PortLens.sln`
  - `.github/workflows/ci.yml`

### 阶段 6：功能补充与体验优化（低优先级）
- **状态：** complete（核心体验项）
- **开始时间：** 2026-07-03
- **完成时间：** 2026-07-03
- 执行的操作：
  - 将标题栏隐藏到托盘按钮的图标从 `Close` 改为 `ChevronDown`，降低与窗口关闭的语义混淆
  - 在 `MainWindowViewModel` 中增加 `IsLoading` 状态
  - 空状态文案根据 `IsLoading` 在 "Scanning..." 与 "No development services found" 之间切换
  - 运行 `dotnet build PortLens.sln` 验证
  - 运行 `dotnet test PortLens.sln` 验证
  - 运行 `powershell.exe ./scripts/publish.ps1` 验证发布
  - 启动发布后的 `PortLens.exe` 验证可正常运行
- 创建/修改的文件：
  - `work/PortLens.Desktop/MainWindow.xaml`
  - `work/PortLens.Desktop/ViewModels/MainWindowViewModel.cs`

### 阶段 7：验证与交付
- **状态：** complete
- **开始时间：** 2026-07-03
- **完成时间：** 2026-07-03
- 执行的操作：
  - 最终 `dotnet build PortLens.sln` 验证成功（0 警告，0 错误）
  - 最终 `dotnet test PortLens.sln` 验证成功（45 个测试全部通过）
  - 最终 `powershell.exe ./scripts/publish.ps1` 验证发布成功
  - 更新 `README.md`：增加解决方案构建、测试命令、CI/CD 说明、项目结构、新功能描述
  - 更新 `CLAUDE.md`：反映解决方案结构、DI/MVVM、测试、CI、日志、资源字典、提取的 `PortScannerFilters` 等
- 创建/修改的文件：
  - `README.md`
  - `CLAUDE.md`

### 阶段 8：修复状态栏与设置持久化回归
- **状态：** complete
- **开始时间：** 2026-07-04
- **完成时间：** 2026-07-04
- 执行的操作：
  - 将 `AppResourceText`、`AppVersionText`、`ShowAppMetrics` 从 `MainWindow` 移到 `MainWindowViewModel`，使状态栏绑定与 `DataContext` 一致
  - 移除 `MainWindow` 中反射刷新 `AppResourceText` 的 hack
  - 修复 `materialDesign:Snackbar` 的 `MessageQueue` 绑定，使用 `RelativeSource={RelativeSource AncestorType=Window}` 指向 `MainWindow.SnackbarMessageQueue`
  - 调整 `MainWindow` 构造顺序：先创建 `MainWindowViewModel` 并设置 `DataContext`，再调用 `ApplyPersistedSettings`
  - 在 `ApplyPersistedSettings` 中通过 `BuildStateFromSettings()` 将加载的设置应用到 `MainWindowViewModel`
  - 运行 `dotnet build PortLens.sln` 验证
  - 运行 `dotnet test PortLens.sln` 验证
  - 运行 `powershell.exe ./scripts/publish.ps1` 验证发布
  - 使用 UI 自动化验证发布后的 `PortLens.exe` 状态栏显示 `CPU 4.1%  Mem 183 MB` 和 `v0.1.0`
  - 验证设置持久化：修改 `ShowSystemPorts`、`RefreshIntervalSeconds`、`ShowAppMetrics` 后重启应用，值保持不变
- 创建/修改的文件：
  - `work/PortLens.Desktop/ViewModels/MainWindowViewModel.cs`
  - `work/PortLens.Desktop/MainWindow.xaml.cs`
  - `work/PortLens.Desktop/MainWindow.xaml`

### 阶段 9：性能优化 A/B/C
- **状态：** complete
- **开始时间：** 2026-07-04
- **完成时间：** 2026-07-04
- 执行的操作：
  - 阶段 A：在 `MainWindow.xaml` 启用主列表 UI 虚拟化（`VirtualizingStackPanel`、`IsVirtualizingWhenGrouping`、`ScrollUnit="Pixel"`）
  - 阶段 B：在 `PortEntryViewModel` 预计算并缓存 `ProjectRootDirectory`、`ProjectGroupKey`、`ProjectGroupTitle`、`ProjectGroupSubtitle`、`SearchHaystack`；`MainWindowViewModel.MatchesText` 使用缓存 haystack
  - 阶段 C：为 `ProcessCommandLineReader` 添加按 PID 的 TTL 缓存；为 `ProcessTreeReader` 添加父子关系图快照缓存；新增 `IProcessCommandLineReader` / `IProcessTreeReader` 接口；`ProcessInspector` 和 DI 注册适配接口
  - 每个阶段独立 `dotnet build` / `dotnet test` / `scripts/publish.ps1` 验证，并做 UI 自动化 smoke test
  - 每个阶段独立提交 Git
- 创建/修改的文件：
  - `work/PortLens.Desktop/MainWindow.xaml`
  - `work/PortLens.Desktop/ViewModels/PortEntryViewModel.cs`
  - `work/PortLens.Desktop/ViewModels/MainWindowViewModel.cs`
  - `work/PortLens.Core/Services/ProcessCommandLineReader.cs`
  - `work/PortLens.Core/Services/ProcessTreeReader.cs`
  - `work/PortLens.Core/Services/ProcessInspector.cs`
  - `work/PortLens.Desktop/ServiceRegistration.cs`

### 阶段 D1：原生命令行读取
- **状态：** complete
- **开始时间：** 2026-07-04
- **完成时间：** 2026-07-04
- 执行的操作：
  - 将 `ProcessCommandLineReader` 从 PowerShell/CIM 改为原生 API 读取
  - 主路径使用 `NtQueryInformationProcess` 的 `ProcessCommandLineInformation`（info class 60）
  - 备用路径通过 PEB + `RTL_USER_PROCESS_PARAMETERS.CommandLine`（偏移 0x70）读取
  - 保留 TTL 缓存、Prune、CancellationToken 语义和空白归一化
  - 运行 `dotnet build PortLens.sln` 验证
  - 运行 `dotnet test PortLens.sln` 验证（45 个测试通过）
  - 运行 `scripts/publish.ps1` 验证发布
  - 使用 `scripts/smoke-test.ps1` 验证发布后的 `PortLens.exe` 窗口正常显示，且未启动 `powershell.exe` 子进程
  - 提交 Git
- 创建/修改的文件：
  - `work/PortLens.Core/Services/ProcessCommandLineReader.cs`
  - `scripts/smoke-test.ps1`

### 阶段 D2：`PortEntry.Key` 使用 struct
- **状态：** complete
- **开始时间：** 2026-07-04
- **完成时间：** 2026-07-04
- 执行的操作：
  - 新增 `PortEntryKey` readonly record struct（`Protocol`、`LocalAddress`、`LocalPort`、`ProcessId`）
  - 将 `PortEntry.Key` 从字符串改为 `PortEntryKey`
  - 将 `PortEntryViewModel.Key` 改为 `PortEntryKey`
  - 将 `MainWindowViewModel` 中的 `_entriesByKey`、`_matchingKeys`、`liveKeys` 改为 `PortEntryKey` 类型，移除 `StringComparer.Ordinal`
  - 新增 `PortLens.Core.Tests/PortEntryKeyTests.cs`，覆盖相等性、哈希码、`HashSet` 去重、`Dictionary` 值相等查找
  - 运行 `dotnet build PortLens.sln` 验证
  - 运行 `dotnet test PortLens.sln` 验证（50 个测试通过）
  - 运行 `scripts/publish.ps1` 验证发布
  - 使用 `scripts/smoke-test.ps1` 验证发布后的 `PortLens.exe` 窗口正常显示
  - 提交 Git
- 创建/修改的文件：
  - `work/PortLens.Core/Models/PortEntryKey.cs`
  - `work/PortLens.Core/Models/PortEntry.cs`
  - `work/PortLens.Desktop/ViewModels/PortEntryViewModel.cs`
  - `work/PortLens.Desktop/ViewModels/MainWindowViewModel.cs`
  - `work/PortLens.Core.Tests/PortEntryKeyTests.cs`

### 阶段 D4：进程快照字典
- **状态：** complete
- **开始时间：** 2026-07-04
- **完成时间：** 2026-07-04
- 执行的操作：
  - 新增 `ProcessSnapshot` readonly record struct（`Id`、`ProcessName`、`StartTime`、`WorkingSet64`、`TotalProcessorTime`、`ExecutablePath`）
  - 在 `ProcessInspector` 中新增 `CaptureSnapshot` 方法，每次扫描只调用一次 `Process.GetProcesses()`
  - 将 `CpuSampler.CalculateCpu` 改为接收 `processId` 和 `TotalProcessorTime`
  - `EnrichBasic` 和 `EnrichDetails` 接收 `IReadOnlyDictionary<int, ProcessSnapshot>` 快照，避免重复打开进程
  - `ReadProcessDetails` 从快照获取可执行路径，不再单独调用 `Process.GetProcessById`
  - `PortScanner.Scan` 在 `PreloadProcessDetails` 后调用一次 `CaptureSnapshot`，并传入所有 enrich 调用
  - 运行 `dotnet build PortLens.sln` 验证
  - 运行 `dotnet test PortLens.sln` 验证（50 个测试通过）
  - 运行 `scripts/publish.ps1` 验证发布
  - 使用 `scripts/smoke-test.ps1` 验证发布后的 `PortLens.exe` 窗口正常显示
  - 提交 Git
- 创建/修改的文件：
  - `work/PortLens.Core/Models/ProcessSnapshot.cs`
  - `work/PortLens.Core/Services/CpuSampler.cs`
  - `work/PortLens.Core/Services/ProcessInspector.cs`
  - `work/PortLens.Core/Services/PortScanner.cs`

### 阶段 D3：`ApplyEntries` 批量 diff
- **状态：** complete
- **开始时间：** 2026-07-04
- **完成时间：** 2026-07-04
- 执行的操作：
  - 新建 `SuppressibleObservableCollection<T>`，支持在 suppression scope 内暂停通知，退出时发出单个 `Reset` 事件
  - 将 `MainWindowViewModel._entries` 改为 `SuppressibleObservableCollection<PortEntryViewModel>`
  - 重写 `ApplyEntries`：先 diff 并复用现有 VM，再按目标顺序构建新列表，最后调用 `ResetTo` 一次性更新
  - 保留 `IsExpanded` 状态（VM 实例复用）
  - 运行 `dotnet build PortLens.sln` 验证
  - 运行 `dotnet test PortLens.sln` 验证（50 个测试通过）
  - 运行 `scripts/publish.ps1` 验证发布
  - 使用 `scripts/smoke-test.ps1` 验证发布后的 `PortLens.exe` 窗口正常显示
  - 提交 Git
- 创建/修改的文件：
  - `work/PortLens.Desktop/Collections/SuppressibleObservableCollection.cs`
  - `work/PortLens.Desktop/ViewModels/MainWindowViewModel.cs`

### 阶段 D5：同一项目下 frontend/backend 聚合分组
- **状态：** complete
- **开始时间：** 2026-07-04
- **完成时间：** 2026-07-04
- 执行的操作：
  - 优化 `ProjectRootResolver.Resolve`：当子项目目录（frontend/backend/api/server/web 等）自身带有 root marker，且父目录也具有 root marker 时，将项目根提升到父目录
  - 保留 workspace container（apps/packages/services）语义：`apps/web` 仍分组在 `web`
  - 新增 `ProjectRootResolver.ComputeRelativeSubtitle`，在组标题显示共享父目录名的同时，副标题显示子项目相对路径（如 `frontend` / `backend`）
  - 更新 `PortEntryViewModel.RecalculateDerivedValues` 使用新的副标题计算
  - 新增 `ProjectRootResolverTests` 覆盖聚合行为、workspace 语义、相对副标题
  - 运行 `dotnet build PortLens.sln` 验证
  - 运行 `dotnet test PortLens.sln` 验证（55 个测试通过）
  - 运行 `scripts/publish.ps1` 验证发布
  - 使用 `scripts/smoke-test.ps1` 验证发布后的 `PortLens.exe` 窗口正常显示
  - 提交 Git
- 创建/修改的文件：
  - `work/PortLens.Core/Services/ProjectRootResolver.cs`
  - `work/PortLens.Desktop/ViewModels/PortEntryViewModel.cs`
  - `work/PortLens.Core.Tests/ProjectRootResolverTests.cs`

## 最终交付说明

本次会话完成了 PortLens 项目的系统性优化，涵盖阶段 1 至阶段 9 以及阶段 D1-D4：

1. **工程基础建设**：创建 `PortLens.sln`、`Directory.Build.props`、`global.json`、`.editorconfig`，统一版本与代码风格。
2. **核心代码重构**：拆分 `ProcessInspector` 为单一职责服务，引入 MVVM 与依赖注入。
3. **性能优化**：CIM 按 PID 过滤、搜索防抖后台化、添加 `CancellationToken`、使用 `ConcurrentDictionary`。
4. **可维护性与质量**：集中主题资源、设置版本迁移、添加文件日志、统一异常处理。
5. **测试与 CI/CD**：创建 `PortLens.Core.Tests` 并编写 50 个单元测试，全部通过；创建 GitHub Actions 工作流。
6. **体验优化**：关闭按钮图标改为 `ChevronDown`，首次扫描增加加载状态。
7. **验证与交付**：完整构建、测试、发布均验证通过；更新文档。
8. **回归修复**：修复状态栏 CPU/内存/版本不显示、设置不持久化的问题。
9. **性能优化 A/B/C**：UI 虚拟化、搜索/分组预计算、跨扫描 PowerShell/CIM 缓存，分别独立提交并通过验证。
10. **阶段 D1**：原生命令行读取，移除 PowerShell/CIM 命令行依赖。
11. **阶段 D2**：`PortEntryKey` struct，消除字符串 key 分配。
12. **阶段 D4**：进程快照字典，减少重复进程打开。
13. **阶段 D3**：`ApplyEntries` 批量 diff，减少 `CollectionChanged` 事件。
14. **阶段 D5**：frontend/backend 等子项目聚合到共同父目录分组。

最终状态：
- `dotnet build PortLens.sln` 成功
- `dotnet test PortLens.sln` 55 个测试通过
- `outputs/PortLensMaterial/PortLens.exe` 可正常启动
- 所有规划文件已同步

## 测试结果

| 测试 | 输入 | 预期结果 | 实际结果 | 状态 |
|------|------|---------|---------|------|
| 发布 exe 启动 | 双击 `outputs/PortLensMaterial/PortLens.exe` | 应用窗口正常显示 | 应用窗口正常显示 | 通过 |
| 单元测试 | `dotnet test PortLens.sln` | 全部通过 | 55 个测试通过，0 失败 | 通过 |

## 错误日志

| 时间戳 | 错误 | 尝试次数 | 解决方案 |
|--------|------|---------|---------|
| 2026-07-03 | 发布 exe 双击无反应 | 1 | `MainWindowViewModel` 手动注册，`ShowSnackbarAsync` 改为 internal |
| 2026-07-03 | 发布后应用进程运行但窗口不显示 | 2 | 移除 `ISnackbarService`，改用 `SnackbarRequested` 事件；`TrayIconService` 和 `PortEntryActionService` 改为在 `MainWindow` 中手动创建，打破 DI 循环依赖 |

## 五问重启检查

| 问题 | 答案 |
|------|------|
| 我在哪里？ | 阶段 D3 已完成；阶段 D 全部完成 |
| 我要去哪里？ | 根据用户反馈决定是否继续优化（如 UDP 支持、深色模式、历史图表等） |
| 目标是什么？ | 系统性地优化 PortLens 的代码结构、性能、可维护性和用户体验 |
| 我学到了什么？ | 见 findings.md |
| 我做了什么？ | 已完成阶段 1 至阶段 9，以及阶段 D1/D2/D4/D3，均已独立提交并通过验证 |

## 会话：2026-07-05

### 阶段 R1：发布 v1.0.2
- **状态：** complete
- **开始时间：** 2026-07-05
- **完成时间：** 2026-07-05
- 执行的操作：
  - 移除 Nerdbank.GitVersioning，改用 git tag 驱动版本号
  - 更新 CI 工作流，在 push tag 时自动解析版本并构建/发布 Release
  - 在设置 About 页面添加 shields.io 风格徽章（项目地址、版本、最新版本、许可证）
  - 修复简体中文在安装版中仍显示英文的问题：MSI 生成脚本未保留 `zh-Hans` 卫星资源目录结构
  - 运行 `dotnet build PortLens.sln` 验证
  - 运行 `dotnet test PortLens.sln` 验证
  - 运行 `scripts/publish.ps1` 验证发布
  - 构建 MSI 并反编译验证 `zh-Hans/PortLens.resources.dll` 位于正确子目录
  - 提交并推送至 GitHub 与 Gitea
  - 删除旧 v1.0.1 tag，创建并推送 v1.0.2 tag
  - GitHub Actions 工作流成功完成，Release v1.0.2 已生成并包含 MSI 与 ZIP 资产
- 创建/修改的文件：
  - `.github/workflows/ci.yml`
  - `global.json`
  - `scripts/generate-wix-files.ps1`
  - `version.json`（已删除）
  - `work/PortLens.Desktop/Dialogs/SettingsDialog.xaml`
  - `work/PortLens.Desktop/Dialogs/SettingsDialog.xaml.cs`

### 测试结果

| 测试 | 输入 | 预期结果 | 实际结果 | 状态 |
|------|------|---------|---------|------|
| 单元测试 | `dotnet test PortLens.sln` | 全部通过 | 55 个测试通过，0 失败 | 通过 |
| 发布 exe 启动 | 双击 `outputs/PortLensMaterial/PortLens.exe` | 应用窗口正常显示 | 应用窗口正常显示 | 通过 |
| 语言切换 | 安装版运行后切换为简体中文 | 设置页面显示中文 | 中文正常显示 | 通过 |
| GitHub Release | `curl` 检查 v1.0.2 资产 | 包含 MSI 与 ZIP | 两个资产均存在且大小正确 | 通过 |

### 错误日志

| 时间戳 | 错误 | 尝试次数 | 解决方案 |
|--------|------|---------|---------|
| 2026-07-05 | 安装版切换语言无效 | 1 | 修复 `generate-wix-files.ps1` 保留 `zh-Hans` 子目录结构 |
| 2026-07-05 | Gitea 首次推送认证失败 | 1 | 重试后成功（远端未提供具体原因） |

### 阶段 R2：发布 v1.0.3（shields.io PNG 修复）
- **状态：** complete
- **开始时间：** 2026-07-05
- **完成时间：** 2026-07-05
- 执行的操作：
  - 发现 v1.0.2 About 页 shields.io 徽章未显示：WPF `BitmapImage` 不支持 SVG
  - 将所有 shields.io URL 改为 `.png` 格式
  - 构建验证
  - 提交并推送至 GitHub 与 Gitea
  - 创建并推送 v1.0.3 tag
  - GitHub Actions 工作流成功完成，Release v1.0.3 已生成
- 创建/修改的文件：
  - `work/PortLens.Desktop/Dialogs/SettingsDialog.xaml.cs`

### 测试结果

| 测试 | 输入 | 预期结果 | 实际结果 | 状态 |
|------|------|---------|---------|------|
| 构建 | `dotnet build PortLens.sln` | 成功 | 成功，0 警告 0 错误 | 通过 |
| GitHub Release | `curl` 检查 v1.0.3 资产 | 包含 MSI 与 ZIP | 两个资产均存在且大小正确 | 通过 |

### 阶段 R3：添加 MIT 许可证、更新 README、全面评估生产就绪差距
- **状态：** complete
- **开始时间：** 2026-07-05
- **完成时间：** 2026-07-05
- 执行的操作：
  - 在仓库根目录新增 `LICENSE` 文件，采用 MIT 许可证
  - 重写 `README.md`：补充下载链接、shields.io 徽章、最新功能（深色模式、本地化、自动更新、分组等）、环境要求、项目结构、CI/CD、贡献指南
  - 使用 Explore agent 全面扫描代码库，从功能、性能、健壮性、架构、测试、生产就绪六个维度整理优化建议
  - 将分析结果写入 `findings.md`
  - 运行 `dotnet build PortLens.sln` 验证
  - 运行 `dotnet test PortLens.sln` 验证（55 个测试通过）
  - 提交并推送至 GitHub 与 Gitea
- 创建/修改的文件：
  - `LICENSE`
  - `README.md`
  - `findings.md`

### 测试结果

| 测试 | 输入 | 预期结果 | 实际结果 | 状态 |
|------|------|---------|---------|------|
| 构建 | `dotnet build PortLens.sln` | 成功 | 成功，0 警告 0 错误 | 通过 |
| 单元测试 | `dotnet test PortLens.sln` | 全部通过 | 55 个测试通过，0 失败 | 通过 |

---
*每个阶段完成后或遇到错误时更新此文件*
