# PortLens

PortLens 是一款轻量级的 Windows 桌面工具，用于监控本地开发端口。它可以自动发现本地运行的开发服务，展示端口、进程、框架、CPU、内存、运行时长等信息，并支持一键打开、复制、终止进程等操作。

## 功能特性

- 自动识别常见开发框架：Vite、Next.js、Nuxt、Django、FastAPI、Spring、.NET、Go、Docker、WSL
- 显示端口、项目名称、框架猜测、PID、CPU、内存、运行时长、监听地址和命令行
- 支持按端口、进程、框架、项目、命令或目录搜索
- 一键打开 `http://localhost:<port>`
- 展开查看 PID、命令行和目录详情
- 右键菜单：复制 URL / PID / 命令行、打开目录、在终端中打开、加入黑名单、终止进程树
- 系统托盘支持：最小化到托盘、暂停/恢复扫描、刷新、复制端口摘要、设置、退出
- 可按项目分组显示服务
- 支持端口黑名单和框架筛选规则
- 自动保存窗口位置、扫描间隔等设置

## 环境要求

- Windows 10/11
- .NET 10 SDK 或更高版本（运行时依赖，非独立部署）

## 快速开始

### 构建

```powershell
dotnet build work/PortLens.Desktop/PortLens.Desktop.csproj
```

### 运行

```powershell
dotnet run --project work/PortLens.Desktop/PortLens.Desktop.csproj
```

### 发布

```powershell
./scripts/publish.ps1
```

发布结果位于 `outputs/PortLensMaterial`，包含 `PortLens.exe`。双击即可运行。

## 项目结构

```
work/
  PortLens.Core/          # 核心类库：端口扫描、进程检查、框架推断
  PortLens.Desktop/       # WPF 桌面应用：UI、托盘、设置、用户操作
scripts/
  publish.ps1             # 发布脚本
outputs/                  # 构建输出目录（已加入 .gitignore）
```

## 使用说明

1. 启动后，PortLens 会自动扫描本地开发服务。
2. 开启右上角的 **System ports** 开关可显示所有本地监听端口（包括系统端口）。
3. 点击卡片上的 **Open** 按钮可在浏览器中打开对应服务。
4. 点击卡片上的 **>** 按钮可展开查看详细信息。
5. 右键卡片可使用更多操作。
6. 关闭窗口会隐藏到系统托盘，右键托盘图标可选择退出。

## 设置

设置保存在 `%APPDATA%/PortLens/settings.json`，包含：

- 是否显示系统端口
- 扫描间隔（3/5/10/30 秒）
- 是否记住窗口位置
- 关闭时是否隐藏到托盘
- 是否按项目分组
- 是否在状态栏显示应用资源占用
- 端口黑名单
- 启用的开发框架规则

## 注意事项

- 部分进程路径或命令行在没有管理员权限时可能无法获取。
- 终止进程树会弹出确认对话框。
- 首次 CPU 采样可能显示 `...`，第二次刷新后显示正常。
- 命令行查询已缓存以保持 UI 响应速度。

## 技术栈

- .NET 10
- WPF（Windows Presentation Foundation）
- MaterialDesignInXAML
- Windows Forms NotifyIcon（系统托盘）
- P/Invoke（`iphlpapi.dll`、`ntdll.dll`、`kernel32.dll`、`user32.dll`）

## 许可证

[待补充]
