# Git 提交信息收集工具 - WPF 实现说明

## 项目结构

- `MainWindow.xaml`：主界面布局
- `MainWindow.xaml.cs`：主要逻辑实现
- `App.xaml`和`App.xaml.cs`：应用程序入口
- `CommitInfo.cs`：提交信息数据模型
- `Resources`目录：存放图标等资源文件
- `GitCommitsWPF.csproj`：项目文件
- `build.bat`：构建脚本

## 实现细节

### 1. UI 设计

界面采用 WPF 实现，主要布局分为三个部分：

- 顶部：设置区域（基本设置和高级设置两个标签页）
- 中部：结果显示区域
- 底部：按钮和进度条

### 2. Git 命令执行

使用 C#的`Process`类执行 Git 命令，获取仓库信息。主要命令有：

- `git log --reverse --format="%ad" --date=format:"%Y-%m-%d %H:%M:%S"`：获取仓库创建时间
- `git log --since=... --until=... --pretty=format:"..." --date=format:"..." --author="..."`：获取提交记录

### 3. 数据处理

- 遍历指定目录查找所有 Git 仓库
- 按照时间范围和作者等条件过滤提交
- 支持自定义格式化输出结果
- 支持多种保存格式（CSV、JSON、TXT、HTML、XML）

### 4. 主要功能实现

#### 扫描 Git 仓库

```csharp
var pathRepos = dir.GetDirectories(".git", SearchOption.AllDirectories)
    .Select(gitDir => gitDir.Parent)
    .Where(parent => parent != null)
    .ToList();
```

#### 执行 Git 命令

```csharp
var process = new Process
{
    StartInfo = new ProcessStartInfo
    {
        FileName = "git",
        Arguments = arguments,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true,
        StandardOutputEncoding = Encoding.UTF8,
        StandardErrorEncoding = Encoding.UTF8
    }
};

process.Start();
var output = process.StandardOutput.ReadToEnd();
process.WaitForExit();
```

#### 格式化输出

```csharp
// 替换占位符
line = line.Replace("{Repository}",
    (!ShowRepeatedRepoNamesCheckBox.IsChecked.Value && displayedRepos.ContainsKey(repoKey)) ?
    new string(' ', repoKey.Length) : commit.Repository);
```

## 打包为独立可执行文件

项目配置使用了以下选项：

```xml
<PropertyGroup>
  <OutputType>WinExe</OutputType>
  <TargetFramework>net472</TargetFramework>
  <RuntimeIdentifier>win-x86</RuntimeIdentifier>
  <PublishSingleFile>true</PublishSingleFile>
  <SelfContained>true</SelfContained>
  <PublishReadyToRun>true</PublishReadyToRun>
</PropertyGroup>
```

这些选项确保应用程序可以作为单独的 EXE 文件分发，无需安装.NET 运行时。

## 与原 PowerShell 脚本的异同

相同点：

- 保留了所有核心功能
- 支持相同的过滤条件和输出格式
- 输出文件格式选项保持一致

不同点：

- 使用图形界面代替命令行参数
- 提供实时查看结果的功能
- 增强了错误处理和用户提示
- 支持直接在界面上保存结果

## 已知问题和改进方向

- 对于非常大的 Git 仓库，扫描可能需要较长时间
- 界面操作会在处理大量数据时出现短暂卡顿
- 建议增加更多自定义选项和高级过滤功能
- 未来可考虑添加提交统计分析和图表展示
