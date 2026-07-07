# 任务计划：PortLens 项目优化调整

## 目标

根据代码分析结果，按高、中、低优先级系统性地优化 PortLens 的代码结构、性能、可维护性和用户体验。

## 当前阶段

阶段 P1-1：`HttpClient` 超时与重试策略（进行中）

## 各阶段

### 阶段 1：工程基础建设
- [x] 创建解决方案文件 `PortLens.sln`
- [x] 创建 `Directory.Build.props` 统一项目属性
- [x] 创建 `global.json` 固定 SDK 版本
- [x] 创建 `.editorconfig` 统一代码风格
- [x] 调整两个 `.csproj` 复用公共属性
- [x] 将阶段结果记录到 `findings.md`
- **状态：** complete

### 阶段 2：核心代码重构（高优先级）
- [x] 拆分 `ProcessInspector.cs` 为多个单一职责类
  - `CpuSampler`
  - `FrameworkDetector`
  - `ProjectNameResolver`
  - `ProcessCommandLineReader`
  - `ProcessCurrentDirectoryReader`
  - `ProcessTreeReader`
- [x] 简化 `ProcessInspector`，使用拆分的类和 `ConcurrentDictionary`
- [x] 拆分 `MainWindow.xaml.cs` 业务逻辑，引入 MVVM
  - 创建 `MainWindowViewModel`
  - 将扫描调度、搜索过滤、分组、黑名单、框架规则迁移到 ViewModel
- [x] 引入依赖注入容器（`Microsoft.Extensions.DependencyInjection`）
  - 创建 `ServiceRegistration`
  - 更新 `App.xaml.cs` 通过 DI 启动主窗口
- [x] 阶段内增量构建验证
- [x] 发布脚本验证
- **状态：** complete

### 阶段 3：性能优化（中优先级）
- [x] 优化 `ProcessCommandLineReader.ReadMany()` 的 PowerShell/CIM 查询，按 PID 过滤
- [x] 搜索过滤增加防抖 + 后台处理
- [x] 长时间操作添加 `CancellationToken` 支持
- [x] 缓存从 `Dictionary + lock` 迁移到 `ConcurrentDictionary`
- [x] 隐藏窗口时进一步降低扫描频率或暂停
- **状态：** complete

### 阶段 4：可维护性与质量（中优先级）
- [x] 提取统一的颜色/样式资源字典
- [x] 移除 `NormalizeEnabledFrameworks` 中的临时迁移逻辑，改用设置版本迁移
- [x] 添加日志记录（`Microsoft.Extensions.Logging` 或简单文件日志）
- [x] 统一异常处理，避免空 `catch` 块
- **状态：** complete

### 阶段 5：测试与 CI/CD
- [x] 创建单元测试项目 `PortLens.Core.Tests`
  - 覆盖 `FrameworkDetector`
  - 覆盖 `ProjectNameResolver`
  - 覆盖 `ProjectRootResolver`
  - 覆盖 `PortScanner` 的核心过滤/排序逻辑
- [x] 创建 GitHub Actions 工作流
  - PR 触发构建
  - 运行测试
  - 构建 Release 产物
- **状态：** complete

### 阶段 6：功能补充与体验优化（低优先级）
- [x] 将关闭按钮图标语义改为隐藏到托盘（使用 `ChevronDown` 替代 `Close`）
- [x] 首次扫描增加加载状态（`IsLoading` 与空状态文案联动）
- [ ] 考虑 UDP 端口支持（超出本次范围）
- [ ] 考虑端口冲突提示（超出本次范围）
- [ ] 考虑历史图表（CPU/内存/端口变化）（超出本次范围）
- [ ] 考虑深色模式（资源字典已集中，便于未来扩展）
- **状态：** complete（核心体验项已完成）

### 阶段 7：验证与交付
- [x] 完整构建验证
- [x] 发布脚本验证
- [x] 更新 `README.md` 和 `CLAUDE.md`
- [x] 将所有变更整理为最终交付说明
- [x] 提交 Git 并修复状态栏/设置持久化回归
- **状态：** complete

### 阶段 8：性能优化 - UI 虚拟化与列表渲染
- [x] 将主列表从 `ItemsControl + ScrollViewer` 替换为支持虚拟化的容器
  - 保持现有视觉样式和交互（ContextMenu、Expander、按钮等）
  - 启用 `VirtualizingStackPanel` 并设置 `ScrollViewer.CanContentScroll="True"`
  - 验证分组（`GroupStyle`）与虚拟化兼容
- [x] 验证大量条目（模拟 100+/500+ 端口）时滚动和刷新仍流畅
- [x] 更新 `progress.md` 和 `findings.md`
- **状态：** complete

### 阶段 9：性能优化 - 搜索与分组预计算
- [x] 在 `PortEntryViewModel.Update()` 中预计算搜索 haystack 并缓存
- [x] 在 `PortEntryViewModel.Update()` 中预计算并缓存 `ProjectRootDirectory`、`ProjectGroupKey`、`ProjectGroupTitle`、`ProjectGroupSubtitle`
- [x] 调整 `MainWindowViewModel.MatchesText` 使用预计算缓存
- [x] 验证分组显示和搜索过滤结果与优化前一致
- [x] 添加/更新相关单元测试（如预计算缓存行为）
- [x] 更新 `progress.md` 和 `findings.md`
- **状态：** complete

### 阶段 10：性能优化 - 跨扫描缓存 PowerShell/CIM 结果
- [x] 在 `ProcessCommandLineReader` 中引入按 PID 的跨扫描缓存（带 TTL，如 60s）
  - 仅当 PID 在目标集合中且缓存未命中/过期时才启动 PowerShell
  - 扫描结束时按 live PIDs prune
- [x] 将 `ProcessTreeReader.CountDescendants` 改为依赖同一进程快照，避免单独 PowerShell 调用
  - 先评估是否可与命令行读取合并；若不能，至少共享缓存
- [x] 验证刷新间隔缩短时 CPU 占用明显下降
- [x] 更新 `progress.md` 和 `findings.md`
- **状态：** complete

### 阶段 11：进阶性能优化（阶段 D）

#### D1：原生 API 读取进程命令行
- [x] 用 `NtQueryInformationProcess` 原生读取命令行，替换 PowerShell/CIM
- [x] 实现 `ProcessCommandLineInformation`（info class 60）主路径
- [x] 实现 PEB + `RTL_USER_PROCESS_PARAMETERS.CommandLine` 备用路径
- [x] 保留缓存、Prune、CancellationToken 和空白归一化
- [x] 构建/测试/发布/smoke test/提交
- **状态：** complete

#### D2：`PortEntry.Key` 使用 struct
- [x] 新增 `PortEntryKey` readonly record struct
- [x] 更新 `PortEntry.Key`、`PortEntryViewModel.Key`、`_entriesByKey`、`_matchingKeys` 使用 struct
- [x] 添加 `PortEntryKeyTests` 单元测试
- [x] 构建/测试/发布/smoke test/提交
- **状态：** complete

#### D3：`ApplyEntries` 批量 diff
- [x] 创建 `SuppressibleObservableCollection<T>`
- [x] 在 `ApplyEntries` 中批量 diff 并发出单个 `Reset` 事件
- [x] 验证滚动位置与展开状态保留
- [x] 构建/测试/发布/smoke test/提交
- **状态：** complete

#### D4：进程快照字典
- [x] 新增 `ProcessSnapshot` struct
- [x] `ProcessInspector.CaptureSnapshot` 一次读取所有进程信息
- [x] `CpuSampler` 和 `EnrichBasic`/`EnrichDetails` 使用快照字典
- [x] `PortScanner.Scan` 调用一次快照并传入 enrich
- [x] 构建/测试/发布/smoke test/提交
- **状态：** complete

#### D5：同一项目下 frontend/backend 聚合分组
- [x] 优化 `ProjectRootResolver`，将 frontend/backend 等子项目聚合到共同父目录
- [x] 新增 `ComputeRelativeSubtitle`，组内显示子项目相对路径
- [x] 更新 `PortEntryViewModel` 副标题计算
- [x] 添加 `ProjectRootResolverTests` 覆盖聚合与 workspace 语义
- [x] 构建/测试/发布/smoke test/提交
- **状态：** complete

### 阶段 R1：发布 v1.0.2
- [x] 移除 Nerdbank.GitVersioning，改用 git tag 驱动版本号
- [x] 更新 CI 工作流自动解析 tag 版本并发布 Release
- [x] About 页面添加 shields.io 风格徽章
- [x] 修复 MSI 安装包未保留 `zh-Hans` 卫星资源目录导致中文不显示的问题
- [x] 构建/测试/发布/提交/tag
- **状态：** complete

### 阶段 R3：添加 MIT 许可证、更新 README、全面评估生产就绪差距
- [x] 添加 MIT LICENSE 文件
- [x] 更新 README.md 至最新功能状态
- [x] 全面分析功能、性能、健壮性、架构、测试、生产就绪差距
- [x] 将分析结果写入 findings.md
- [x] 构建/测试/提交/推送
- **状态：** complete

## 当前阶段
阶段 P1-1：`HttpClient` 超时与重试策略（进行中）

### 阶段 P0：性能瓶颈根治（高优先级）

#### P0-1：用原生 API 替换 `ProcessTreeReader` 的 PowerShell/CIM
- [x] 调研 `NtQuerySystemInformation` / `SystemProcessInformation` 在 x64 Windows 上的结构稳定性
- [x] 实现 `NativeProcessSnapshot` 或类似类，一次性读取所有进程 PID、Parent PID、Session ID 等
- [x] 重写 `ProcessTreeReader.CountDescendants` 基于原生快照构建子进程图
- [x] 保留 PowerShell 作为最终 fallback（默认不启用）
- [x] 更新 `ProcessCurrentDirectoryReader` 共享同一快照获取 ParentProcessId，减少 WMI 查询
- [x] 新增/更新单元测试，验证子进程计数与 PowerShell 结果一致
- [x] 构建/测试/发布/smoke test/提交
- **状态：** complete

#### P0-2：`FileLogger` 异步化
- [x] 引入 `System.Threading.Channels.Channel<string>`
- [x] 新增后台写入线程，批量 flush（1s 或 100 条阈值）
- [x] `AppDomain.UnhandledException` 中强制 flush，避免崩溃丢日志
- [x] 构建/测试/发布/smoke test/提交
- **状态：** complete

### 阶段 P1：关键 I/O 与枚举优化（中-高优先级）

#### P1-1：`HttpClient` 超时与重试策略
- [ ] 为 `UpdateCheckService` / `AutoUpdateService` 配置 15s 超时
- [ ] 引入 Polly 重试策略：更新检查指数退避 2 次，下载重试 1 次
- [ ] 确保所有网络调用传播 `CancellationToken`
- [ ] 构建/测试/发布/提交
- **状态：** in_progress

#### P1-2：进程快照按需枚举或原生化
- [ ] 评估 `Process.GetProcessById(livePids)` 方案 vs 复用 P0-1 原生快照
- [ ] 替换 `ProcessInspector.CaptureSnapshot` 中的 `Process.GetProcesses()` 全枚举
- [ ] 验证系统进程/已退出进程行为与之前一致
- [ ] 构建/测试/发布/提交
- **状态：** pending

#### P1-3：字体列表缓存与 ComboBox 虚拟化
- [ ] `FontService.GetInstalledFontFamilies` 改为 Lazy 缓存
- [ ] `SettingsDialog` 中两个字体 ComboBox 启用虚拟化
- [ ] 验证设置对话框打开速度
- [ ] 构建/测试/发布/提交
- **状态：** pending

### 阶段 P2：算法与集合优化（中优先级）

#### P2-1：`FrameworkDetector` 避免大字符串拼接
- [ ] 改为按 ProcessName、CommandLine、路径分段匹配
- [ ] 使用 `ReadOnlySpan<char>` / `SearchValues<string>` 减少分配
- [ ] 保持现有框架识别准确率
- [ ] 构建/测试/发布/提交
- **状态：** pending

#### P2-2：`ProjectRootResolver` 目录 marker 缓存
- [ ] 添加 `ConcurrentDictionary<string, DirectoryInfo?>` 缓存，TTL 30s
- [ ] 调整 marker 检查顺序，高命中优先
- [ ] 验证聚合分组行为不变
- [ ] 构建/测试/发布/提交
- **状态：** pending

#### P2-3：复用 `ApplyEntries` 中间集合
- [ ] 复用 `HashSet<PortEntryKey>` 和 `List<PortEntryViewModel>`
- [ ] 确保线程安全（每次扫描单线程在后台，主线程访问 _entries）
- [ ] 构建/测试/发布/提交
- **状态：** pending

### 阶段 P3：UI 与微优化（低优先级）

#### P3-1：托盘菜单缓存与 About 徽章缓存
- [ ] 托盘 `ContextMenu` 只构建一次，状态变化时更新
- [ ] shields.io 徽章图片本地/内存缓存
- [ ] 构建/测试/发布/提交
- **状态：** pending

#### P3-2：命令行空白归一化去 Regex
- [ ] 用 `StringBuilder`/`ValueStringBuilder` 替换 `Regex.Replace`
- [ ] 构建/测试/发布/提交
- **状态：** pending

#### P3-3：`AppMetricsTimer` 后台暂停
- [ ] 窗口隐藏/最小化时暂停，恢复时立即更新
- [ ] 构建/测试/发布/提交
- **状态：** pending

### 阶段 P4：性能基准与回归防护

- [ ] 新增 `PortLens.Core.Benchmarks` 项目（BenchmarkDotNet）
- [ ] 基准覆盖：`PortScanner.Scan`、`FrameworkDetector.InferFramework`、`ProjectRootResolver.Resolve`、`ProcessTreeReader.CountDescendants`
- [ ] 在 CI 中可选运行基准（PR 不阻塞，但记录结果）
- [ ] 构建/测试/发布/提交
- **状态：** pending

## 当前阶段
阶段 P0-1（用原生 API 替换 `ProcessTreeReader` 的 PowerShell/CIM）

## 已做决策

| 决策 | 理由 |
|------|------|
| 分阶段执行，先工程基建再高优先级重构 | 降低一次性改动过大带来的回归风险，便于逐步验证 |
| 优先拆分 `ProcessInspector` 和 `MainWindow.xaml.cs` | 这两个文件是当前最大的可维护性瓶颈 |
| 使用 `Microsoft.Extensions.DependencyInjection` | 与 .NET 生态一致，学习成本低，不需要额外包 |
| 先做纯单元测试，再做 CI/CD | 单元测试不依赖管理员权限，执行稳定，可作为 CI 基础 |
| 用 `NtQueryInformationProcess` 替代 PowerShell 读取命令行 | 消除外部子进程开销，降低刷新间隔下的 CPU/IO 占用 |

## 遇到的错误

| 错误 | 尝试次数 | 解决方案 |
|------|---------|---------|
| 发布 exe 双击无反应 | 1 | `MainWindowViewModel` 改为手动注册，`ShowSnackbarAsync` 改为 internal |

## 备注

- 随着进度更新阶段状态：pending → in_progress → complete
- 做重大决策前重新读取此计划
- 记录所有错误，避免重复
- 每个阶段完成后更新 `progress.md`
