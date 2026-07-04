# 任务计划：PortLens 项目优化调整

## 目标

根据代码分析结果，按高、中、低优先级系统性地优化 PortLens 的代码结构、性能、可维护性和用户体验。

## 当前阶段

阶段 8

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
- [ ] 将主列表从 `ItemsControl + ScrollViewer` 替换为支持虚拟化的容器
  - 保持现有视觉样式和交互（ContextMenu、Expander、按钮等）
  - 启用 `VirtualizingStackPanel` 并设置 `ScrollViewer.CanContentScroll="True"`
  - 验证分组（`GroupStyle`）与虚拟化兼容
- [ ] 验证大量条目（模拟 100+/500+ 端口）时滚动和刷新仍流畅
- [ ] 更新 `progress.md` 和 `findings.md`
- **状态：** pending

### 阶段 9：性能优化 - 搜索与分组预计算
- [ ] 在 `PortEntryViewModel.Update()` 中预计算搜索 haystack 并缓存
- [ ] 在 `PortEntryViewModel.Update()` 中预计算并缓存 `ProjectRootDirectory`、`ProjectGroupKey`、`ProjectGroupTitle`、`ProjectGroupSubtitle`
- [ ] 调整 `MainWindowViewModel.MatchesText` 使用预计算缓存
- [ ] 验证分组显示和搜索过滤结果与优化前一致
- [ ] 添加/更新相关单元测试（如预计算缓存行为）
- [ ] 更新 `progress.md` 和 `findings.md`
- **状态：** pending

### 阶段 10：性能优化 - 跨扫描缓存 PowerShell/CIM 结果
- [ ] 在 `ProcessCommandLineReader` 中引入按 PID 的跨扫描缓存（带 TTL，如 60s）
  - 仅当 PID 在目标集合中且缓存未命中/过期时才启动 PowerShell
  - 扫描结束时按 live PIDs prune
- [ ] 将 `ProcessTreeReader.CountDescendants` 改为依赖同一进程快照，避免单独 PowerShell 调用
  - 先评估是否可与命令行读取合并；若不能，至少共享缓存
- [ ] 验证刷新间隔缩短时 CPU 占用明显下降
- [ ] 更新 `progress.md` 和 `findings.md`
- **状态：** pending

### 阶段 11：进阶性能优化（可选，视前阶段收益决定）
- [ ] 评估用 `NtQuerySystemInformation` 原生读取进程命令线，替换 PowerShell/CIM
- [ ] 评估 `PortEntry.Key` 使用 struct 替代字符串，减少字典 key 分配
- [ ] 评估 `ApplyEntries` 使用批量 diff 算法，减少 `CollectionChanged` 事件
- [ ] 评估 `ProcessInspector.EnrichBasic` 使用进程快照字典，减少 `Process.GetProcessById` 异常开销
- **状态：** pending

## 关键问题

1. 是否保留当前的 WPF 代码隐藏风格，还是全面迁移到 MVVM？
2. 是否引入第三方 DI 容器，还是使用轻量级的 `Microsoft.Extensions.DependencyInjection`？
3. 是否优先添加集成测试（需要管理员权限的真实进程/端口扫描），还是先做纯单元测试？
4. 深色模式是否纳入本次调整范围？

## 已做决策

| 决策 | 理由 |
|------|------|
| 分阶段执行，先工程基建再高优先级重构 | 降低一次性改动过大带来的回归风险，便于逐步验证 |
| 优先拆分 `ProcessInspector` 和 `MainWindow.xaml.cs` | 这两个文件是当前最大的可维护性瓶颈 |
| 使用 `Microsoft.Extensions.DependencyInjection` | 与 .NET 生态一致，学习成本低，不需要额外包 |
| 先做纯单元测试，再做 CI/CD | 单元测试不依赖管理员权限，执行稳定，可作为 CI 基础 |

## 遇到的错误

| 错误 | 尝试次数 | 解决方案 |
|------|---------|---------|
| 发布 exe 双击无反应 | 1 | `MainWindowViewModel` 改为手动注册，`ShowSnackbarAsync` 改为 internal |

## 备注

- 随着进度更新阶段状态：pending → in_progress → complete
- 做重大决策前重新读取此计划
- 记录所有错误，避免重复
- 每个阶段完成后更新 `progress.md`
