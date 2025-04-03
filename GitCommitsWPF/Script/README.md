# Git 提交信息收集工具

这是一个用于收集本地 Git 仓库提交信息的 PowerShell 脚本工具。该工具可以扫描指定路径下的所有 Git 仓库，并根据设定的时间范围获取提交记录。

## 功能特点

- 递归扫描指定路径下的所有 Git 仓库
- 支持多种时间范围筛选：日、周、月、自定义日期范围
- 支持按作者筛选提交记录
- 多种输出格式：控制台显示、CSV 文件、JSON 文件
- 显示进度条和统计信息

## 系统要求

- Windows 操作系统
- PowerShell 5.0 或更高版本
- Git 已安装并添加到 PATH 环境变量

## 使用方法

```powershell
.\Get-GitCommits.ps1 -Path <扫描路径> -TimeRange <时间范围> [其他参数]
```

### 必填参数

- `-Path`: 要扫描的根路径，脚本将在此路径下递归查找所有 Git 仓库
- `-TimeRange`: 时间范围选项，可选值：
  - `day`: 今天
  - `week`: 本周（从周一到当前日期）
  - `month`: 本月（从 1 号到当前日期）
  - `custom`: 自定义日期范围（需同时指定 StartDate 和 EndDate）

### 可选参数

- `-StartDate`: 当 TimeRange 为"custom"时使用的开始日期，格式为"yyyy-MM-dd"
- `-EndDate`: 当 TimeRange 为"custom"时使用的结束日期，格式为"yyyy-MM-dd"
- `-Author`: 按作者筛选提交记录
- `-OutputFormat`: 输出格式，可选值：
  - `console`: 控制台显示（默认）
  - `csv`: 输出为 CSV 文件
  - `json`: 输出为 JSON 文件
- `-OutputPath`: 当 OutputFormat 为"csv"或"json"时，输出文件的路径

## 使用示例

### 获取特定路径下所有仓库本周的提交记录

```powershell
.\Get-GitCommits.ps1 -Path "D:\Projects" -TimeRange "week"
```

### 获取特定路径下特定作者在自定义日期范围内的提交记录，并输出到 CSV 文件

```powershell
.\Get-GitCommits.ps1 -Path "D:\Projects" -TimeRange "custom" -StartDate "2023-01-01" -EndDate "2023-01-31" -Author "张三" -OutputFormat "csv" -OutputPath "D:\commits.csv"
```

### 获取特定路径下本月的所有提交记录，并输出到 JSON 文件

```powershell
.\Get-GitCommits.ps1 -Path "D:\Projects" -TimeRange "month" -OutputFormat "json" -OutputPath "D:\commits.json"
```

## 输出内容

脚本将收集以下信息：

- 仓库名称
- 仓库路径
- 提交 ID
- 作者
- 提交日期
- 提交消息

## 注意事项

- 确保 Git 已正确安装并添加到 PATH 环境变量中
- 当选择"custom"时间范围时，必须同时指定 StartDate 和 EndDate 参数
- 当 OutputFormat 为"csv"或"json"时，必须指定 OutputPath 参数
- 大型仓库或包含大量仓库的路径可能需要较长的处理时间

```powershell
PS D:\DevelopTools> .\Get-GitCommits.ps1 -Path "D:\NEW project" -TimeRange "week" -OutputFormat "json" -OutputPath "D:\commits.json"

.\Get-GitCommits.ps1 -Path "D:\NEW project\BP-APP_bzb\develop\BP-APP" -TimeRange "week"  -Author "longhai shen"  -OutputPath "D:\commits.json"
.\Get-GitCommits.ps1 -Paths  @("D:\NEW project") -TimeRange "week"  -Author "longhai shen"  -OutputPath "D:\commits1.txt" -Format "default"

```

# 获取今天的提交

.\Get-GitCommits.ps1 -Path "D:\YourPath" -TimeRange "day"

# 获取本周的提交并保存为 CSV

.\Get-GitCommits.ps1 -Path "D:\YourPath" -TimeRange "week" -OutputFormat "csv" -OutputPath "D:\commits.csv"

# 获取特定时间范围内的提交并保存为 JSON

.\Get-GitCommits.ps1 -Path "D:\YourPath" -TimeRange "custom" -StartDate "2023-01-01" -EndDate "2023-12-31" -OutputFormat "json" -OutputPath "D:\commits.json"
