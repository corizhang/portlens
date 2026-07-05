# PortLens

[![GitHub release](https://img.shields.io/github/v/release/corizhang/portlens)](https://github.com/corizhang/portlens/releases)
[![License](https://img.shields.io/github/license/corizhang/portlens)](LICENSE)
[![CI](https://github.com/corizhang/portlens/actions/workflows/ci.yml/badge.svg)](https://github.com/corizhang/portlens/actions/workflows/ci.yml)

PortLens 是一款面向 Windows 开发者的本地端口监控工具。它会自动发现当前运行的开发服务，集中展示端口、进程、框架、资源占用、运行时长等信息，并支持一键打开、复制、定位目录、终止进程等操作。

## 下载

从 [GitHub Releases](https://github.com/corizhang/portlens/releases/latest) 下载最新版：

- **MSI 安装包**：`PortLens-vX.Y.Z-win-x64.msi`（推荐，支持自动更新）
- **便携压缩包**：`PortLens-vX.Y.Z-win-x64.zip`（解压后直接运行 `PortLens.exe`）

## 功能特性

- **自动识别常见开发框架**：Vite、Next.js、Nuxt、Django、FastAPI、Spring Boot、.NET、Go、Docker、WSL 等
- **实时展示关键信息**：端口号、项目名称、框架猜测、PID、CPU、内存、运行时长、监听地址、命令行
- **按项目智能分组**：frontend/backend 等子项目会自动聚合到共同父目录
- **强大的搜索与过滤**：支持按端口、进程、框架、项目、命令或目录搜索，支持系统端口显示开关
- **丰富的快捷操作**：
  - 一键在浏览器打开 `http://localhost:<port>`
  - 复制 URL / PID / 命令行
  - 打开项目目录、在终端（Windows Terminal / PowerShell）中打开
  - 终止进程树（带确认对话框）
  - 加入黑名单，隐藏不需要关注的端口
- **系统托盘集成**：最小化到托盘、暂停/恢复扫描、手动刷新、复制端口摘要、设置、退出
- **个性化设置**：
  - 浅色 / 深色主题切换
  - 英文 / 简体中文界面
  - 自定义中文字体、英文字体
  - 扫描间隔（3 / 5 / 10 / 30 秒）
  - 记住窗口位置、关闭时最小化到托盘、按项目分组、状态栏资源占用显示
- **自动更新**：About 页面可手动检查更新，下载并安装最新 MSI（需要管理员权限）
- **端口黑名单与框架规则**：隐藏不关心的端口，自定义要识别的开发框架

## 环境要求

- Windows 10 版本 1809 或更高 / Windows 11
- 已安装 [.NET 10 桌面运行时](https://dotnet.microsoft.com/download/dotnet/10.0)（发布包为框架依赖）
- 部分功能（如读取所有进程命令行、终止系统进程）在管理员权限下更稳定

## 快速开始

### 构建

```powershell
dotnet build PortLens.sln
```

### 运行

```powershell
dotnet run --project work/PortLens.Desktop/PortLens.Desktop.csproj
```

### 测试

```powershell
dotnet test PortLens.sln
```

### 发布

```powershell
./scripts/publish.ps1
```

发布结果位于 `outputs/PortLensMaterial/PortLens.exe`。

## 项目结构

```
work/
  PortLens.Core/           # 核心类库：端口扫描、进程检查、框架推断
  PortLens.Desktop/        # WPF 桌面应用：UI、托盘、设置、用户操作
  PortLens.Core.Tests/     # xUnit 单元测试
scripts/
  publish.ps1              # 发布脚本
  generate-wix-files.ps1   # 生成 MSI 文件清单
.github/workflows/
  ci.yml                   # GitHub Actions CI/CD
installer/                 # WiX Toolset v5 安装工程
outputs/                   # 构建输出目录（已加入 .gitignore）
```

## 设置

设置保存在 `%APPDATA%/PortLens/settings.json`，包含：

- 是否显示系统端口
- 扫描间隔
- 是否记住窗口位置、关闭时隐藏到托盘、按项目分组、显示应用资源占用
- 浅色 / 深色主题
- 界面语言（en-US / zh-Hans）
- 中文字体、英文字体
- 端口黑名单
- 启用的开发框架规则

## CI/CD

项目配置了 GitHub Actions（`.github/workflows/ci.yml`）：

- 对 `main`/`master` 的 push 和 pull_request 触发构建与测试
- 推送 `v*.*.*` 标签时自动解析版本号，构建 MSI 安装包和 ZIP 压缩包，并创建 GitHub Release
- 构建产物保留 7 天

## 使用说明

1. 启动后 PortLens 会自动扫描本地 TCP 监听端口。
2. 开启右上角的 **System ports** 开关可显示所有监听端口（包括系统端口）。
3. 点击卡片上的 **Open** 按钮可在浏览器打开对应服务。
4. 点击卡片上的展开按钮可查看 PID、命令行、目录等详情。
5. 右键卡片可使用更多操作。
6. 关闭窗口会隐藏到系统托盘，右键托盘图标可选择退出。

## 贡献

欢迎提交 Issue 和 Pull Request。请确保：

- 代码遵循 `.editorconfig` 风格
- 通过 `dotnet test PortLens.sln` 单元测试
- 重大改动请先创建 Issue 讨论

## 许可证

本项目基于 [MIT 许可证](LICENSE) 开源。

---

> PortLens 不是安全或网络监控工具，仅用于本地开发调试场景。
