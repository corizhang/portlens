# 任务计划：PortLens 项目优化调整

## 目标

根据代码分析结果，按高、中、低优先级系统性地优化 PortLens 的代码结构、性能、可维护性和用户体验。

## 当前阶段

阶段 3

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
- [ ] 优化 `ProcessCommandLineReader.ReadMany()` 的 PowerShell/CIM 查询，按 PID 过滤
- [ ] 搜索过滤增加防抖 + 后台处理
- [ ] 长时间操作添加 `CancellationToken` 支持
- [x] 缓存从 `Dictionary + lock` 迁移到 `ConcurrentDictionary`
- [x] 隐藏窗口时进一步降低扫描频率或暂停
- **状态：** in_progress

### 阶段 4：可维护性与质量（中优先级）
- [ ] 提取统一的颜色/样式资源字典
- [ ] 移除 `NormalizeEnabledFrameworks` 中的临时迁移逻辑，改用设置版本迁移
- [ ] 添加日志记录（`Microsoft.Extensions.Logging` 或简单文件日志）
- [ ] 统一异常处理，避免空 `catch` 块
- **状态：** pending

### 阶段 5：测试与 CI/CD
- [ ] 创建单元测试项目 `PortLens.Core.Tests`
  - 覆盖 `FrameworkDetector`
  - 覆盖 `ProjectNameResolver`
  - 覆盖 `ProjectRootResolver`
  - 覆盖 `PortScanner` 的核心过滤/排序逻辑
- [ ] 创建 GitHub Actions 工作流
  - PR 触发构建
  - 运行测试
  - 构建 Release 产物
- **状态：** pending

### 阶段 6：功能补充与体验优化（低优先级）
- [ ] 将关闭按钮图标语义改为隐藏到托盘（或添加提示）
- [ ] 首次扫描增加加载状态
- [ ] 考虑 UDP 端口支持
- [ ] 考虑端口冲突提示
- [ ] 考虑历史图表（CPU/内存/端口变化）
- [ ] 考虑深色模式
- **状态：** pending

### 阶段 7：验证与交付
- [ ] 完整构建验证
- [ ] 发布脚本验证
- [ ] 更新 `README.md` 和 `CLAUDE.md`
- [ ] 将所有变更整理为最终交付说明
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
