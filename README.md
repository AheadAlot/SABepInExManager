# SABepInExManager

基于AvaloniaUI的跨平台（Windows/macOS/Linux）工具，用于管理《学生时代》游戏创意工坊里使用[BepInEx前置包](https://steamcommunity.com/sharedfiles/filedetails/?id=3660837353)的 Mod。


## 杀毒软件报毒说明
如果装了杀软大概率报毒，我自己电脑的360也报了。原因是我没做签名，而且有BepInEx文件很容易就被标记病毒，直接添加信任即可。若有顾虑可以手动拉取代码本地编译运行。


## 功能

- 参考[群芳更多恋人自动更新](https://github.com/lincexu/LinceAutoUpdater)，通过名为AutoUpdater的BepInEx patcher，在游戏启动时自动检测所有BepInEx模组并更新。只需勾选需要的模组并排序，其他步骤全自动完成
- 一键安装BepInEx与ConfigurationManager（默认启用`Logging.Console`避免第一次Hook失败）
- 自动探测游戏根目录，也可手动填写/调整。
- 自动探测创意工坊目录，也可手动指定。
- 扫描并展示BepInEx模组（仅处理工坊条目中包含 `BepInEx/` 的 Mod）。
- 拖拽排序调整优先级：列表**靠后**的 Mod 优先级更高，发生同路径冲突时会覆盖靠前的 Mod。
- 冲突预览：在应用前预览可能的文件冲突与最终生效项。
- 基线备份/回滚：
  - 创建备份（每次创建都会生成一个以 Unix 时间戳命名的新目录）
  - 恢复备份

## 下载

打开[Releases 页面](https://github.com/AheadAlot/SABepInExManager/releases/latest)下载所用操作系统`.zip`文件即可。

运行：
   - Windows：下载解压后直接双击运行 `.exe`
   - macOS：下载解压后，首次运行可能受 Gatekeeper 限制；可在“系统设置 -> 隐私与安全性”中允许，或右键打开。
   - Linux/macOS：未测试，下载解压后确保`chmod +x`再运行。

## 部分程序截图

### 主页
<img width="1426" height="792" alt="image" src="https://github.com/user-attachments/assets/9908cd24-cb11-4ce8-8370-e6ca22bf67c3" />

### Mod管理
<img width="1906" height="1016" alt="image" src="https://github.com/user-attachments/assets/ac6725ce-9fc8-4860-ac88-ef316db22c9b" />

### 备份
<img width="1906" height="1016" alt="image" src="https://github.com/user-attachments/assets/cc9cd169-f230-4062-98ff-7715f4b9c566" />

### 设置
<img width="1906" height="1016" alt="image" src="https://github.com/user-attachments/assets/3af87944-cd7e-45c2-9110-557c2f1f4798" />



## 本地构建

### 环境要求

- .NET SDK：项目目标框架为 `net10.0`（见 [`SABepInExManager.csproj`](src/SABepInExManager/SABepInExManager.csproj:4)）。

### 构建Debug

在仓库根目录执行：

```bash
dotnet build ./SABepInExManager.slnx -c Debug
```

### 发布Release

以下命令会将产物输出到根目录：`./dist/SABepInExManager/<rid>/`。

Windows x64：

```bash
dotnet publish ./src/SABepInExManager/SABepInExManager.csproj -c Release -r win-x64 --self-contained true
```

macOS Apple Silicon：

```bash
dotnet publish ./src/SABepInExManager/SABepInExManager.csproj -c Release -r osx-arm64 --self-contained true
```

Linux x64：

```bash
dotnet publish ./src/SABepInExManager/SABepInExManager.csproj -c Release -r linux-x64 --self-contained true
```

## 使用的开源软件信息与声明

### 随仓库分发（third_party）

以下内容在本仓库中以备选包形式提供（用于网络异常时的安装回退），对应许可证文件也随仓库提供：

- BepInEx：LGPL-2.1，见 [`third_party/BepInEx/LICENSE`](third_party/BepInEx/LICENSE:1)
- BepInEx.ConfigurationManager：LGPL-3.0，见 [`third_party/BepInEx.ConfigurationManager/LICENSE`](third_party/BepInEx.ConfigurationManager/LICENSE:1)
