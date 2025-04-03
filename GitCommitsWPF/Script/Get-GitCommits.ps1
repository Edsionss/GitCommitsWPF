#!/usr/bin/env pwsh
<#
.SYNOPSIS
    获取指定时间范围内指定路径下的所有Git仓库的提交信息。

.DESCRIPTION
    扫描指定路径下的所有Git仓库，并根据指定的时间范围参数获取提交记录。
    支持按日、周、月或自定义日期范围获取提交记录。
    支持选择多个路径和自定义提取字段。
    使用格式模板时，默认情况下，相同仓库的提交记录中，仓库名称只会在第一条记录中显示，
    后续记录中仓库名称会被替换为空字符串，以避免重复显示。
    可以使用-ShowRepeatedRepoNames参数控制此行为，指定该参数后，将在每条记录中显示仓库名称。

.PARAMETER Paths
    要扫描的根路径数组，脚本将在这些路径下递归查找所有Git仓库。

.PARAMETER TimeRange
    时间范围选项，可选择：'day'(今天), 'week'(本周), 'month'(本月), 'custom'(自定义), 'all'(所有)。
    默认为'all'，获取仓库所有提交记录。

.PARAMETER StartDate
    当TimeRange为'custom'时使用的开始日期，格式为'yyyy-MM-dd'。

.PARAMETER EndDate
    当TimeRange为'custom'时使用的结束日期，格式为'yyyy-MM-dd'。

.PARAMETER Author
    可选参数，按作者筛选提交记录。

.PARAMETER AuthorFilter
    可选参数，按作者筛选提交记录。

.PARAMETER OutputPath
    输出文件的路径，根据文件后缀自动判断输出格式（.csv 或 .json）。

.PARAMETER Fields
    要提取的字段列表，可选值有：'Repository', 'RepoPath', 'RepoFolder', 'CommitId', 'Author', 'Date', 'Message'。
    其中'RepoFolder'表示仓库的文件夹名称，例如从路径'D:\Project\YForum V2.0\git\yforum_git'中提取出'yforum_git'。
    默认提取所有字段。

.PARAMETER GitFields
    要从Git提交中提取的字段列表，可选值有：'CommitId', 'Author', 'Date', 'Message'。
    默认提取所有字段。

.PARAMETER Format
    输出格式模板，用于控制输出的排版。例如："{Date} - {Author}: {Message}"
    当设置为"default"时，将使用内置的默认模板"{Repository} : {Message}"。
    默认情况下，当使用格式模板时，相同仓库的提交记录中，仓库名称(Repository或RepoFolder)只会在第一条记录中显示，
    后续记录中仓库名称会被替换为空字符串，以避免重复显示。
    可以使用-ShowRepeatedRepoNames参数控制此行为，指定该参数后，将在每条记录中显示仓库名称。

.PARAMETER TemplateFile
    格式模板文件的路径，脚本将从此文件读取格式模板。
    文件内容应为一行文本，包含格式模板。例如："{Date} - {Author}: {Message}"
    注意：如果同时指定了Format和TemplateFile参数，将优先使用TemplateFile中的模板。
    {Repository} - 仓库名称
    {RepoPath} - 仓库的完整路径，例如：D:\Project\YForum V2.0\git\yforum_git
    {RepoFolder} - 新添加的特殊占位符，仅仓库的文件夹名称，例如：yforum_git
    {CommitId} - 提交ID
    {Author} - 提交作者
    {Date} - 提交日期
    {Message} - 提交消息
    
.PARAMETER ShowRepeatedRepoNames
    可选参数，用于控制是否在每条提交记录中都显示仓库名称。
    默认情况下（未指定此参数时），相同仓库的提交记录中，仓库名称只会在第一条记录中显示。
    指定此参数后，每条提交记录都会显示仓库名称，即使是来自同一仓库的连续记录。

.EXAMPLE
    .\Get-GitCommits.ps1 -Paths @("D:\Projects", "E:\Work")
    获取D:\Projects和E:\Work路径下所有Git仓库从创建开始到今天的所有提交信息。

.EXAMPLE
    .\Get-GitCommits.ps1 -Paths "D:\Projects" -TimeRange "custom" -StartDate "2023-01-01" -EndDate "2023-01-31" -Author "Zhang San" -OutputPath "D:\commits.csv"
    获取D:\Projects路径下所有Git仓库在2023年1月由Zhang San提交的信息，并输出到CSV文件

.EXAMPLE
    .\Get-GitCommits.ps1 -Paths @("D:\Projects", "E:\Work") -TimeRange "week" -Fields @("Date", "Author", "Message") -Format "{Date} - {Author}: {Message}"
    获取本周的提交，并按照自定义格式输出日期、作者和提交信息

.EXAMPLE
    .\Get-GitCommits.ps1 -Paths "D:\Projects" -GitFields @("Author", "Message")
    只获取D:\Projects路径下所有Git仓库提交的作者和提交信息

.EXAMPLE
    .\Get-GitCommits.ps1 -Paths "D:\Projects" -TemplateFile "D:\template.txt"
    使用D:\template.txt中定义的格式模板来格式化输出

.EXAMPLE
    .\Get-GitCommits.ps1 -Paths "D:\Projects" -Format "{RepoFolder} - {Author}: {Message}"
    获取仓库提交信息，并使用仓库文件夹名称、作者和提交信息进行格式化输出

.EXAMPLE
    .\Get-GitCommits.ps1 -Paths "D:\Projects" -Format "{RepoFolder} : {Message}"
    获取仓库提交信息，使用仓库文件夹名称和提交信息进行格式化输出。
    对于来自同一仓库的多条提交记录，仓库名称只会在第一条记录中显示，如：
    "yforum_git : 修复App.vue中的过渡效果，更新transition属性值"
    " : 优化代码格式，简化组件结构，调整路由配置，更新请求基础URL"

.EXAMPLE
    .\Get-GitCommits.ps1 -Paths "D:\Projects" -Format "{RepoFolder} : {Message}" -ShowRepeatedRepoNames
    获取仓库提交信息，使用仓库文件夹名称和提交信息进行格式化输出。
    指定-ShowRepeatedRepoNames参数后，每条提交记录都会显示仓库名称，如：
    "yforum_git : 修复App.vue中的过渡效果，更新transition属性值"
    "yforum_git : 优化代码格式，简化组件结构，调整路由配置，更新请求基础URL"
#>

param (
    [Parameter(Mandatory = $true)]
    [string[]]$Paths,

    [Parameter(Mandatory = $false)]
    [ValidateSet('day', 'week', 'month', 'custom', 'all')]
    [string]$TimeRange = 'all',

    [Parameter(Mandatory = $false)]
    [string]$StartDate = "",

    [Parameter(Mandatory = $false)]
    [string]$EndDate = "",

    [Parameter(Mandatory = $false)]
    [string]$Author = "",

    [Parameter(Mandatory = $false)]
    [string]$AuthorFilter = "",

    [Parameter(Mandatory = $false)]
    [string]$OutputPath = "",
    
    [Parameter(Mandatory = $false)]
    [ValidateSet('Repository', 'RepoPath', 'RepoFolder', 'CommitId', 'Author', 'Date', 'Message')]
    [string[]]$Fields = @('Repository', 'RepoPath', 'RepoFolder', 'CommitId', 'Author', 'Date', 'Message'),
    
    [Parameter(Mandatory = $false)]
    [ValidateSet('CommitId', 'Author', 'Date', 'Message')]
    [string[]]$GitFields = @('CommitId', 'Author', 'Date', 'Message'),
    
    [Parameter(Mandatory = $false)]
    [string]$Format = "",
    
    [Parameter(Mandatory = $false)]
    [string]$TemplateFile = "",

    [Parameter(Mandatory = $false)]
    [switch]$ShowRepeatedRepoNames
)

# 设置 PowerShell 输出编码为 UTF-8
$OutputEncoding = [System.Text.Encoding]::UTF8
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

# 确保控制台能够正确显示中文字符
if ($PSVersionTable.PSVersion.Major -ge 6) {
    # PowerShell Core (6.0+) 使用 -Encoding utf8 不会添加BOM
    $PSDefaultParameterValues['*:Encoding'] = 'utf8'
    $utf8NoBomEncoding = 'utf8'
    $utf8WithBomEncoding = 'utf8BOM'
} else {
    # Windows PowerShell (5.1及以下) 需要特殊处理
    $PSDefaultParameterValues['*:Encoding'] = 'utf8'
    $utf8NoBomEncoding = New-Object System.Text.UTF8Encoding $false
    $utf8WithBomEncoding = New-Object System.Text.UTF8Encoding $true
    
    # 在Windows PowerShell中设置代码页以支持中文显示
    [System.Console]::OutputEncoding = [System.Text.Encoding]::UTF8
    try { chcp 65001 | Out-Null } catch {}
}

# 设置控制台标题
$Host.UI.RawUI.WindowTitle = "Git提交信息收集工具"

# 设置 Git 配置
git config --global core.quotepath false
git config --global i18n.logoutputencoding utf-8
git config --global i18n.commitencoding utf-8
git config --global gui.encoding utf-8

# 检查Git是否已安装
try {
    $gitVersion = git --version
    Write-Host "Git已安装: $gitVersion" -ForegroundColor Green
}
catch {
    Write-Host "错误: 未检测到Git，请确保Git已安装并添加到PATH环境变量中。" -ForegroundColor Red
    exit 1
}

# 检查路径是否存在
foreach ($path in $Paths) {
    if (-not (Test-Path $path)) {
        Write-Host "错误: 指定的路径 '$path' 不存在。" -ForegroundColor Red
        exit 1
    }
}

# 根据TimeRange计算日期范围
$since = ""
# 修复当天日期问题，设置到明天来包含今天的提交
$tomorrow = (Get-Date).AddDays(1)
$until = $tomorrow.ToString("yyyy-MM-dd")

switch ($TimeRange) {
    "day" {
        $since = (Get-Date).Date.ToString("yyyy-MM-dd")
    }
    "week" {
        $startOfWeek = (Get-Date).AddDays(-(Get-Date).DayOfWeek.value__ + 1)
        $since = $startOfWeek.ToString("yyyy-MM-dd")
    }
    "month" {
        $startOfMonth = Get-Date -Day 1
        $since = $startOfMonth.ToString("yyyy-MM-dd")
    }
    "custom" {
        if ([string]::IsNullOrEmpty($StartDate) -or [string]::IsNullOrEmpty($EndDate)) {
            Write-Host "错误: 当选择'custom'时间范围时，必须指定StartDate和EndDate参数。" -ForegroundColor Red
            exit 1
        }
        try {
            $sinceDate = [DateTime]::ParseExact($StartDate, "yyyy-MM-dd", $null)
            $untilDate = [DateTime]::ParseExact($EndDate, "yyyy-MM-dd", $null)
            # 调整结束日期为第二天凌晨，以包含结束日期当天的提交
            $untilDate = $untilDate.AddDays(1)
            $since = $StartDate
            $until = $untilDate.ToString("yyyy-MM-dd")
        }
        catch {
            Write-Host "错误: 日期格式无效。请使用'yyyy-MM-dd'格式。" -ForegroundColor Red
            exit 1
        }
    }
    "all" {
        # 不设置since，从仓库创建开始
        $since = ""
    }
}

# 定义存储所有提交信息的数组
$allCommits = @()

# 定义变量存储最早的仓库创建时间
$earliestRepoDate = $null

# 查找所有Git仓库
Write-Host "正在扫描Git仓库，请稍后..." -ForegroundColor Cyan
$gitRepos = @()

foreach ($path in $Paths) {
    $pathRepos = Get-ChildItem -Path $path -Recurse -Force -Directory -ErrorAction SilentlyContinue | 
        Where-Object { $_.Name -eq ".git" -and $_.PSIsContainer } | 
        Select-Object -ExpandProperty Parent
    
    Write-Host "在路径 '$path' 下找到 $($pathRepos.Count) 个Git仓库" -ForegroundColor Cyan
    $gitRepos += $pathRepos
}

$repoCount = $gitRepos.Count
Write-Host "总共找到 $repoCount 个Git仓库" -ForegroundColor Green

# 根据用户选择的GitFields构建Git日志格式
$gitFormatParts = @()
$gitFieldMap = @{
    'CommitId' = '%H'
    'Author' = '%an'
    'Date' = '%ad'
    'Message' = '%s'
}

# 确保至少有一个字段被选择
if ($GitFields.Count -eq 0) {
    $GitFields = @('CommitId', 'Author', 'Date', 'Message')
}

# 构建Git格式字符串
foreach ($field in $GitFields) {
    if ($gitFieldMap.ContainsKey($field)) {
        $gitFormatParts += $gitFieldMap[$field]
    }
}
$gitFormat = $gitFormatParts -join '|'

# 设置默认模板
$defaultTemplate = "{Repository} : {Message}"

# 处理格式模板文件和默认模板
if (-not [string]::IsNullOrEmpty($TemplateFile)) {
    if (Test-Path $TemplateFile) {
        try {
            # 读取模板文件的第一行作为格式模板
            $templateContent = Get-Content -Path $TemplateFile -TotalCount 1 -Encoding UTF8
            Write-Host "已从模板文件加载格式: $templateContent" -ForegroundColor Green
            # 优先使用模板文件的内容
            $Format = $templateContent
        }
        catch {
            Write-Host "警告: 读取模板文件 '$TemplateFile' 时出错: $_" -ForegroundColor Yellow
        }
    }
    else {
        Write-Host "警告: 指定的模板文件 '$TemplateFile' 不存在。" -ForegroundColor Yellow
    }
}
elseif ($Format -eq "default") {
    # 使用默认模板
    $Format = $defaultTemplate
    Write-Host "使用默认模板格式: $Format" -ForegroundColor Green
}

# 处理每个Git仓库
$currentRepo = 0
foreach ($repo in $gitRepos) {
    $currentRepo++
    # 计算百分比进度并显示
    $percentComplete = [math]::Round(($currentRepo / $repoCount) * 100)
    Write-Host "处理仓库 [${percentComplete}%]: $($repo.FullName)" -ForegroundColor Yellow
    
    Push-Location $repo.FullName
    
    try {
        # 设置 Git 命令的编码为 UTF-8
        $env:GIT_TERMINAL_PROMPT = 0
        $env:LANG = "zh_CN.UTF-8"
        
        # 获取仓库创建时间（第一次提交的时间）
        $firstCommitDate = & git log --reverse --format="%ad" --date=format:"%Y-%m-%d %H:%M:%S" | Select-Object -First 1
        
        # 更新最早的仓库创建时间
        if (-not [string]::IsNullOrEmpty($firstCommitDate)) {
            $repoCreateDate = [DateTime]::ParseExact($firstCommitDate, "yyyy-MM-dd HH:mm:ss", $null)
            if ($null -eq $earliestRepoDate -or $repoCreateDate -lt $earliestRepoDate) {
                $earliestRepoDate = $repoCreateDate
            }
            
            Write-Host "   仓库创建时间: $firstCommitDate" -ForegroundColor Cyan
        }
        
        # 构建基本的 Git 命令参数
        $gitArgs = @(
            "log"
        )
        
        # 添加时间范围参数
        if (-not [string]::IsNullOrEmpty($since)) {
            $gitArgs += "--since=$since"
        }
        
        # 添加结束时间参数
        $gitArgs += "--until=$until"
        
        # 添加格式化参数
        $gitArgs += "--pretty=format:$gitFormat"
        $gitArgs += "--date=format:%Y-%m-%d %H:%M:%S"
        
        # 添加作者筛选
        if (-not [string]::IsNullOrEmpty($Author)) {
            $gitArgs += "--author=$Author"
        }
        
        # 执行Git命令
        $commitLogs = & git $gitArgs 2>$null
        
        if ($commitLogs) {
            foreach ($log in $commitLogs) {
                $parts = $log.Split('|')
                if ($parts.Count -ge $GitFields.Count) {
                    # 创建一个空的对象来存储提交信息
                    $commitObj = [PSCustomObject]@{
                        Repository = $repo.Name
                        RepoPath = $repo.FullName
                        RepoFolder = [System.IO.Path]::GetFileName($repo.FullName)
                    }
                    
                    # 根据选择的字段添加属性
                    for ($i = 0; $i -lt $GitFields.Count; $i++) {
                        $fieldName = $GitFields[$i]
                        $fieldValue = $parts[$i]
                        $commitObj | Add-Member -NotePropertyName $fieldName -NotePropertyValue $fieldValue
                    }
                    
                    # 应用作者筛选
                    $shouldInclude = $true
                    if (-not [string]::IsNullOrEmpty($AuthorFilter) -and $commitObj.PSObject.Properties.Name -contains "Author") {
                        $shouldInclude = $false
                        $authors = $AuthorFilter -split ','
                        foreach ($author in $authors) {
                            if ($commitObj.Author -match $author.Trim()) {
                                $shouldInclude = $true
                                break
                            }
                        }
                    }
                    
                    if ($shouldInclude) {
                        $allCommits += $commitObj
                    }
                }
            }
        }
    }
    catch {
        Write-Host "警告: 处理仓库 '$($repo.FullName)' 时出错: $_" -ForegroundColor Yellow
    }
    finally {
        Pop-Location
    }
}

# 替换进度条完成消息
Write-Host "所有仓库处理完成 (100%)" -ForegroundColor Green

# 输出扫描信息
Write-Host "==== 扫描信息 ====" -ForegroundColor Cyan
if ([string]::IsNullOrEmpty($since)) {
    if ($null -ne $earliestRepoDate) {
        $earliestDateStr = $earliestRepoDate.ToString("yyyy-MM-dd HH:mm:ss")
        Write-Host "时间范围: 从仓库创建开始 ($earliestDateStr) 至 $(Get-Date -Format "yyyy-MM-dd")" -ForegroundColor Cyan
    } else {
        Write-Host "时间范围: 从仓库创建开始 至 $(Get-Date -Format "yyyy-MM-dd")" -ForegroundColor Cyan
    }
} else {
    # 显示真实的日期范围，而不是调整后的日期
    $displayUntil = (Get-Date -Date $until).AddDays(-1).ToString("yyyy-MM-dd")
    Write-Host "时间范围: $since 至 $displayUntil" -ForegroundColor Cyan
}

if ([string]::IsNullOrEmpty($Author)) {
    Write-Host "提交作者: 所有作者" -ForegroundColor Cyan
} else {
    Write-Host "提交作者: $Author" -ForegroundColor Cyan
}

if (-not [string]::IsNullOrEmpty($AuthorFilter)) {
    Write-Host "作者过滤: $AuthorFilter" -ForegroundColor Cyan
}

Write-Host "提取的Git字段: $($GitFields -join ', ')" -ForegroundColor Cyan
Write-Host "===================" -ForegroundColor Cyan

# 输出结果
$commitCount = $allCommits.Count
Write-Host "在指定时间范围内共找到 $commitCount 个提交" -ForegroundColor Green

if ($commitCount -gt 0) {
    # 应用字段筛选
    $filteredCommits = $allCommits
    
    if ($Fields -and $Fields.Count -gt 0) {
        # 只选择实际存在的字段
        $existingFields = $Fields | Where-Object { $allCommits[0].PSObject.Properties.Name -contains $_ }
        if ($existingFields.Count -gt 0) {
            $filteredCommits = $allCommits | Select-Object $existingFields
        }
    }
    
    # 应用自定义格式
    if (-not [string]::IsNullOrEmpty($Format) -or -not [string]::IsNullOrEmpty($TemplateFile)) {
        $formattedOutput = @()
        $displayedRepos = @{}  # 哈希表，记录已显示过的仓库
        
        foreach ($commit in $allCommits) {
            $line = $Format
            
            # 获取当前提交的仓库标识符（通常是RepoFolder或Repository）
            $repoKey = if ($commit.PSObject.Properties.Name -contains 'RepoFolder') { $commit.RepoFolder } else { $commit.Repository }
            
            # 替换所有占位符
            foreach ($field in ($commit | Get-Member -MemberType NoteProperty).Name) {
                $placeholder = "{$field}"
                $value = $commit.$field
                
                # 处理仓库名称重复问题
                if (-not $ShowRepeatedRepoNames -and ($field -eq 'Repository' -or $field -eq 'RepoFolder') -and $displayedRepos.ContainsKey($repoKey)) {
                    # 替换为相同长度的空格，保持缩进一致
                    $value = " " * $repoKey.Length  # 使用与仓库名称相同长度的空格
                }
                
                $line = $line.Replace($placeholder, $value)
            }
            
            # 标记此仓库已显示
            if (-not $displayedRepos.ContainsKey($repoKey)) {
                $displayedRepos[$repoKey] = $true
            }
            
            $formattedOutput += $line
        }
        
        # 输出格式化内容
        if ([string]::IsNullOrEmpty($OutputPath)) {
            $formattedOutput | ForEach-Object { Write-Host $_ }
        } else {
            # 保存到文件
            if ($PSVersionTable.PSVersion.Major -ge 6) {
                $formattedOutput | Out-File -FilePath $OutputPath -Encoding $utf8WithBomEncoding -Force
            } else {
                [System.IO.File]::WriteAllLines($OutputPath, $formattedOutput, $utf8WithBomEncoding)
            }
            Write-Host "格式化的提交信息已保存到: $OutputPath" -ForegroundColor Green
        }
    }
    else {
        # 如果未提供模板参数，默认使用JSON格式输出
        if ([string]::IsNullOrEmpty($OutputPath)) {
            # 如果没有指定输出路径，则以JSON格式输出到控制台
            $filteredCommits | ConvertTo-Json -Depth 10 | Write-Host
        }
        else {
            # 根据文件后缀判断输出格式
            $extension = [System.IO.Path]::GetExtension($OutputPath).ToLower()
            switch ($extension) {
                ".csv" {
                    # 使用 UTF8 with BOM 编码保存 CSV 文件
                    if ($PSVersionTable.PSVersion.Major -ge 6) {
                        # PowerShell Core (6.0+)
                        $filteredCommits | ConvertTo-Csv -NoTypeInformation | Out-File -FilePath $OutputPath -Encoding $utf8WithBomEncoding -Force
                    } else {
                        # Windows PowerShell (5.1及以下)
                        $csvContent = $filteredCommits | ConvertTo-Csv -NoTypeInformation
                        [System.IO.File]::WriteAllLines($OutputPath, $csvContent, $utf8WithBomEncoding)
                    }
                    Write-Host "提交信息已保存到: $OutputPath" -ForegroundColor Green
                }
                ".json" {
                    # 使用 UTF8 with BOM 编码保存 JSON 文件
                    $jsonContent = $filteredCommits | ConvertTo-Json -Depth 10
                    if ($PSVersionTable.PSVersion.Major -ge 6) {
                        # PowerShell Core (6.0+)
                        $jsonContent | Out-File -FilePath $OutputPath -Encoding $utf8WithBomEncoding -Force
                    } else {
                        # Windows PowerShell (5.1及以下)
                        [System.IO.File]::WriteAllText($OutputPath, $jsonContent, $utf8WithBomEncoding)
                    }
                    Write-Host "提交信息已保存到: $OutputPath" -ForegroundColor Green
                }
                ".txt" {
                    # 使用 UTF8 with BOM 编码保存纯文本文件
                    if ($PSVersionTable.PSVersion.Major -ge 6) {
                        # PowerShell Core (6.0+)
                        $filteredCommits | Format-Table | Out-File -FilePath $OutputPath -Encoding $utf8WithBomEncoding -Force
                    } else {
                        # Windows PowerShell (5.1及以下)
                        $textContent = $filteredCommits | Format-Table | Out-String
                        [System.IO.File]::WriteAllText($OutputPath, $textContent, $utf8WithBomEncoding)
                    }
                    Write-Host "提交信息已保存到: $OutputPath" -ForegroundColor Green
                }
                ".html" {
                    # 生成HTML内容
                    $htmlHeader = @"
<!DOCTYPE html>
<html>
<head>
    <meta charset="UTF-8">
    <title>Git提交记录</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 20px; }
        table { border-collapse: collapse; width: 100%; }
        th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }
        th { background-color: #f2f2f2; }
        tr:nth-child(even) { background-color: #f9f9f9; }
        .repo-header { background-color: #e6f7ff; font-weight: bold; }
    </style>
</head>
<body>
    <h1>Git提交记录</h1>
    <table>
        <tr>
"@
                    
                    # 生成表头
                    $htmlTableHeader = ""
                    foreach ($field in ($filteredCommits[0] | Get-Member -MemberType NoteProperty).Name) {
                        $htmlTableHeader += "            <th>$field</th>`n"
                    }
                    $htmlTableHeader += "        </tr>`n"
                    
                    # 生成表内容
                    $htmlTableBody = ""
                    $lastRepo = ""
                    foreach ($commit in $filteredCommits) {
                        $currentRepo = if ($commit.PSObject.Properties.Name -contains 'RepoFolder') { $commit.RepoFolder } else { $commit.Repository }
                        
                        # 如果是新仓库的第一条记录，添加特殊样式
                        if ($currentRepo -ne $lastRepo) {
                            $htmlTableBody += "        <tr class='repo-header'>`n"
                            $lastRepo = $currentRepo
                        } else {
                            $htmlTableBody += "        <tr>`n"
                        }
                        
                        foreach ($field in ($commit | Get-Member -MemberType NoteProperty).Name) {
                            $value = [System.Web.HttpUtility]::HtmlEncode($commit.$field)
                            $htmlTableBody += "            <td>$value</td>`n"
                        }
                        $htmlTableBody += "        </tr>`n"
                    }
                    
                    $htmlFooter = @"
    </table>
    <p>生成时间: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")</p>
</body>
</html>
"@
                    
                    $htmlContent = $htmlHeader + $htmlTableHeader + $htmlTableBody + $htmlFooter
                    
                    # 保存HTML文件
                    if ($PSVersionTable.PSVersion.Major -ge 6) {
                        # PowerShell Core (6.0+)
                        $htmlContent | Out-File -FilePath $OutputPath -Encoding $utf8WithBomEncoding -Force
                    } else {
                        # Windows PowerShell (5.1及以下)
                        [System.IO.File]::WriteAllText($OutputPath, $htmlContent, $utf8WithBomEncoding)
                    }
                    Write-Host "提交信息已保存为HTML格式到: $OutputPath" -ForegroundColor Green
                }
                ".xml" {
                    # 使用 UTF8 with BOM 编码保存 XML 文件
                    # 创建XML文档
                    $xmlDoc = New-Object System.Xml.XmlDocument
                    $xmlDeclaration = $xmlDoc.CreateXmlDeclaration("1.0", "UTF-8", $null)
                    $xmlRoot = $xmlDoc.DocumentElement
                    $xmlDoc.InsertBefore($xmlDeclaration, $xmlRoot)
                    
                    # 创建根元素
                    $rootElement = $xmlDoc.CreateElement("GitCommits")
                    $xmlDoc.AppendChild($rootElement)
                    
                    # 添加每个提交记录
                    foreach ($commit in $filteredCommits) {
                        $commitElement = $xmlDoc.CreateElement("Commit")
                        
                        foreach ($field in ($commit | Get-Member -MemberType NoteProperty).Name) {
                            $fieldElement = $xmlDoc.CreateElement($field)
                            $fieldElement.InnerText = $commit.$field
                            $commitElement.AppendChild($fieldElement)
                        }
                        
                        $rootElement.AppendChild($commitElement)
                    }
                    
                    # 保存XML文件
                    $xmlDoc.Save($OutputPath)
                    Write-Host "提交信息已保存为XML格式到: $OutputPath" -ForegroundColor Green
                }
                default {
                    Write-Host "错误: 不支持的文件格式 '$extension'。支持的格式: .csv, .json, .txt, .html, .xml" -ForegroundColor Red
                    exit 1
                }
            }
        }
    }
} 
