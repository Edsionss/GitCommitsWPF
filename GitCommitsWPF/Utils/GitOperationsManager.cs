using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GitCommitsWPF.Models;
using GitCommitsWPF.Services;
using System.Collections.Concurrent;
// using System.Threading.Tasks;
using System.Threading;
// using System.Threading.Tasks;

namespace GitCommitsWPF.Utils
{
  /// <summary>
  /// 处理Git相关操作的管理器类
  /// </summary>
  public class GitOperationsManager
  {
    private readonly OutputManager _outputManager;
    private readonly DialogManager _dialogManager;
    private readonly AuthorManager _authorManager;

    private bool _isRunning = false;
    private int _repoCount = 0;
    private int _currentRepo = 0;

    // 进度报告类
    private class ProgressReport
    {
      public int CompletedItems { get; set; }
      public string CurrentRepo { get; set; }
    }

    // 进度计数器类
    private class CounterProgress
    {
      private int _count = 0;
      private int _total;
      private readonly IProgress<ProgressReport> _progress;

      public CounterProgress(int total, IProgress<ProgressReport> progress)
      {
        _total = total;
        _progress = progress;
      }

      public void ReportProgress(string currentItem)
      {
        int current = Interlocked.Increment(ref _count);
        _progress.Report(new ProgressReport { CompletedItems = current, CurrentRepo = currentItem });
      }
    }

    /// <summary>
    /// 初始化Git操作管理器
    /// </summary>
    /// <param name="outputManager">输出管理器</param>
    /// <param name="dialogManager">对话框管理器</param>
    /// <param name="authorManager">作者管理器</param>
    public GitOperationsManager(OutputManager outputManager, DialogManager dialogManager, AuthorManager authorManager)
    {
      _outputManager = outputManager ?? throw new ArgumentNullException(nameof(outputManager));
      _dialogManager = dialogManager ?? throw new ArgumentNullException(nameof(dialogManager));
      _authorManager = authorManager ?? throw new ArgumentNullException(nameof(authorManager));
    }

    /// <summary>
    /// 是否正在运行
    /// </summary>
    public bool IsRunning
    {
      get { return _isRunning; }
      set { _isRunning = value; }
    }

    /// <summary>
    /// 检查路径是否为Git仓库
    /// </summary>
    /// <param name="path">要检查的路径</param>
    /// <returns>是否为Git仓库</returns>
    public bool IsGitRepository(string path)
    {
      try
      {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
        {
          return false;
        }

        // 检查.git目录是否存在
        string gitDir = Path.Combine(path, ".git");
        if (Directory.Exists(gitDir))
        {
          return true;
        }

        // 尝试执行git命令检查
        bool commandResult = false;
        string currentDirectory = Directory.GetCurrentDirectory();

        try
        {
          Directory.SetCurrentDirectory(path);

          // 执行git命令验证是否为Git仓库
          using (var process = new Process())
          {
            process.StartInfo = new ProcessStartInfo
            {
              FileName = "git",
              Arguments = "rev-parse --is-inside-work-tree",
              UseShellExecute = false,
              RedirectStandardOutput = true,
              CreateNoWindow = true,
              StandardOutputEncoding = Encoding.UTF8
            };

            process.Start();
            string output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();

            commandResult = process.ExitCode == 0 && output.ToLower() == "true";
          }
        }
        catch
        {
          // 命令执行失败，不是Git仓库
          commandResult = false;
        }
        finally
        {
          // 恢复原来的目录
          Directory.SetCurrentDirectory(currentDirectory);
        }

        return commandResult;
      }
      catch (Exception)
      {
        return false;
      }
    }

    /// <summary>
    /// 验证路径是否为Git仓库
    /// </summary>
    /// <param name="path">路径</param>
    /// <param name="verifyGitPaths">是否验证Git路径</param>
    /// <returns>路径是否有效</returns>
    public bool ValidatePath(string path, bool verifyGitPaths)
    {
      if (!verifyGitPaths)
        return true; // 如果未启用验证，直接返回true

      if (IsGitRepository(path))
        return true;

      _dialogManager.ShowCustomMessageBox("验证失败", $"路径未通过验证，不是Git仓库: {path}", false);
      return false;
    }

    /// <summary>
    /// 查找Git仓库
    /// </summary>
    /// <param name="path">路径</param>
    /// <returns>Git仓库列表</returns>
    public List<DirectoryInfo> FindGitRepositories(string path)
    {
      try
      {
        if (!Directory.Exists(path))
        {
          _outputManager.UpdateOutput($"路径不存在: {path}");
          return new List<DirectoryInfo>();
        }

        // 使用GitService查找仓库
        List<DirectoryInfo> gitRepos = GitService.FindGitRepositories(path);
        _outputManager.UpdateOutput($"在路径 '{path}' 下找到 {gitRepos.Count} 个Git仓库");
        return gitRepos;
      }
      catch (Exception ex)
      {
        _outputManager.UpdateOutput($"搜索Git仓库时出错: {ex.Message}");
        return new List<DirectoryInfo>();
      }
    }

    /// <summary>
    /// 从Git仓库获取作者信息
    /// </summary>
    /// <param name="repo">仓库</param>
    /// <param name="authors">作者列表</param>
    public void ScanGitAuthors(DirectoryInfo repo, List<string> authors)
    {
      try
      {
        string currentDirectory = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(repo.FullName);

        _outputManager.UpdateOutput($"正在从仓库获取作者信息: {repo.FullName}");

        // 执行git命令获取所有作者
        var process = new Process
        {
          StartInfo = new ProcessStartInfo
          {
            FileName = "git",
            Arguments = "log --format=\"%an\" --all",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8
          }
        };

        process.Start();
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        // 解析输出，添加作者
        var newAuthors = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(author => !string.IsNullOrWhiteSpace(author))
            .ToList();

        authors.AddRange(newAuthors);

        // 也添加到扫描作者列表
        _authorManager.AddScannedAuthors(newAuthors);

        _outputManager.UpdateOutput($"从仓库 '{repo.Name}' 找到 {newAuthors.Count} 个作者");

        // 恢复当前目录
        Directory.SetCurrentDirectory(currentDirectory);
      }
      catch (Exception ex)
      {
        _outputManager.UpdateOutput($"获取仓库作者时出错: {ex.Message}");
      }
    }

    /// <summary>
    /// 异步扫描Git作者
    /// </summary>
    /// <param name="paths">路径列表</param>
    /// <returns>作者列表</returns>
    public async Task<List<string>> ScanGitAuthorsAsync(List<string> paths)
    {
      // 创建新的作者列表，确保不会与先前的扫描结果混合
      List<string> authors = new List<string>();

      // 显示进度条
      _outputManager.ShowProgressBar();
      _outputManager.UpdateProgressBar(10);  // 设置初始进度值
      _outputManager.UpdateOutput("正在初始化扫描过程...");

      _isRunning = true;

      try
      {
        // 并行收集所有路径下的Git仓库
        ConcurrentBag<DirectoryInfo> allReposBag = new ConcurrentBag<DirectoryInfo>();
        ConcurrentBag<string> authorsBag = new ConcurrentBag<string>();

        // 设置最大并行度
        int maxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1);
        _outputManager.UpdateOutput($"使用并行度: {maxDegreeOfParallelism}");

        // 第一步：并行收集所有有效路径
        List<string> validPaths = new List<string>();
        foreach (var path in paths)
        {
          if (Directory.Exists(path))
          {
            validPaths.Add(path);
          }
          else
          {
            _outputManager.UpdateOutput($"警告: 路径不存在: {path}");
          }
        }

        // 第二步：并行搜索所有仓库
        await Task.Run(() =>
        {
          Parallel.ForEach(validPaths, new ParallelOptions { MaxDegreeOfParallelism = maxDegreeOfParallelism }, path =>
          {
            if (!_isRunning) return;

            try
            {
              // 查找Git仓库
              FindGitRepositoriesParallel(path, allReposBag, maxDegreeOfParallelism);
            }
            catch (Exception ex)
            {
              _outputManager.UpdateOutput($"搜索路径时出错: {ex.Message}");
            }
          });
        });

        // 转换为列表并去除重复
        List<DirectoryInfo> allRepos = allReposBag
            .GroupBy(repo => repo.FullName)
            .Select(group => group.First())
            .ToList();

        _outputManager.UpdateOutput($"共找到 {allRepos.Count} 个Git仓库待扫描");
        _outputManager.UpdateProgressBar(30);

        // 第三步：并行扫描所有仓库的作者
        // 创建进度报告对象
        var progress = new Progress<ProgressReport>(report =>
        {
          // 报告进度
          int percentComplete = 30 + (int)((report.CompletedItems / (double)allRepos.Count) * 70);
          _outputManager.UpdateProgressBar(percentComplete);
          _outputManager.UpdateOutput($"扫描仓库 [{report.CompletedItems}/{allRepos.Count}]: {report.CurrentRepo}");
        });

        // 用于并行处理的计数器
        var completedCounter = new CounterProgress(allRepos.Count, progress);

        await Task.Run(() =>
        {
          Parallel.ForEach(allRepos, new ParallelOptions { MaxDegreeOfParallelism = maxDegreeOfParallelism }, repo =>
          {
            if (!_isRunning) return;

            try
            {
              // 报告当前处理的仓库
              completedCounter.ReportProgress(repo.FullName);

              // 使用更高效的方式获取作者
              ScanGitAuthorsFast(repo, authorsBag);
            }
            catch (Exception ex)
            {
              _outputManager.UpdateOutput($"扫描仓库作者时出错: {ex.Message}");
            }
          });
        });

        // 扫描完成，设置进度为100%
        _outputManager.UpdateProgressBar(100);

        // 转换为列表并去重
        authors = authorsBag.Distinct().OrderBy(a => a).ToList();

        // 添加到扫描作者列表
        _authorManager.AddScannedAuthors(authors);

        _outputManager.UpdateOutput($"扫描完成，共找到 {authors.Count} 个不同的Git作者");
      }
      catch (Exception ex)
      {
        _outputManager.UpdateOutput($"扫描Git作者时出错: {ex.Message}");
      }
      finally
      {
        _isRunning = false;
        // 隐藏进度条
        _outputManager.HideProgressBar();
      }

      return authors;
    }

    /// <summary>
    /// 使用优化的方法扫描Git作者
    /// </summary>
    private void ScanGitAuthorsFast(DirectoryInfo repo, ConcurrentBag<string> authors)
    {
      try
      {
        string currentDirectory = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(repo.FullName);

        // 执行Git命令获取所有作者 - 使用更高效的命令
        var process = new Process
        {
          StartInfo = new ProcessStartInfo
          {
            FileName = "git",
            Arguments = "log --format=\"%an\" --all --no-merges --use-mailmap", // 使用mailmap并排除merge提交
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8
          }
        };

        process.Start();

        // 逐行读取输出，直接添加到作者集合
        while (!process.StandardOutput.EndOfStream)
        {
          string author = process.StandardOutput.ReadLine();
          if (!string.IsNullOrWhiteSpace(author))
          {
            authors.Add(author);
          }
        }

        process.WaitForExit();

        // 恢复当前目录
        Directory.SetCurrentDirectory(currentDirectory);
      }
      catch (Exception)
      {
        // 忽略单个仓库的错误
      }
    }

    /// <summary>
    /// 收集Git提交
    /// </summary>
    /// <param name="paths">路径列表</param>
    /// <param name="since">开始日期</param>
    /// <param name="until">结束日期</param>
    /// <param name="author">作者</param>
    /// <param name="authorFilter">作者过滤</param>
    /// <param name="verifyGitPaths">是否验证Git路径</param>
    /// <returns>提交记录列表</returns>
    public async Task<List<CommitInfo>> CollectGitCommitsAsync(List<string> paths, string since, string until, string author, string authorFilter, bool verifyGitPaths)
    {
      List<CommitInfo> commits = new List<CommitInfo>();
      _isRunning = true;

      // 记录扫描开始时间
      DateTime scanStartTime = DateTime.Now;

      try
      {
        // 显示进度条 - 改为任务执行前先更新UI
        await _outputManager.ShowProgressBarAsync();
        await _outputManager.UpdateProgressBarAsync(5); // 设置初始进度值
        await _outputManager.UpdateOutputAsync("正在初始化Git仓库扫描...");

        // 预处理筛选条件，优化性能
        await _outputManager.UpdateOutputAsync("预处理筛选条件...");

        // 处理日期范围
        if (string.IsNullOrEmpty(until))
        {
          // Git的until参数是不包含当天的，所以需要使用明天的日期才能包含今天的提交
          until = DateTime.Now.AddDays(1).ToString("yyyy-MM-dd"); // 设置到明天来包含今天的提交
        }

        // 特殊处理"今天"的情况，确保能获取到0点的提交
        if (!string.IsNullOrEmpty(since) && DateTime.TryParse(since, out DateTime sinceDate))
        {
          // 如果since日期是今天，特殊处理确保获取所有提交
          if (sinceDate.Date == DateTime.Today)
          {
            // 使用00:00:00作为开始时间
            since = $"{sinceDate.ToString("yyyy-MM-dd")} 00:00:00";
            await _outputManager.OutputInfoAsync($"检测到当天查询，设置精确开始时间: {since}");
          }
        }

        // 如果until日期是明天，同样特殊处理
        if (!string.IsNullOrEmpty(until) && DateTime.TryParse(until, out DateTime untilDate))
        {
          if (untilDate.Date == DateTime.Today.AddDays(1))
          {
            // 使用23:59:59作为结束时间确保包含所有提交
            until = $"{DateTime.Today.ToString("yyyy-MM-dd")} 23:59:59";
            await _outputManager.OutputInfoAsync($"检测到当天查询，设置精确结束时间: {until}");
          }
        }

        // 记录实际使用的时间范围，以帮助调试
        await _outputManager.OutputInfoAsync("实际使用的时间范围:");
        await _outputManager.OutputInfoAsync($"- 开始日期(--since): {(string.IsNullOrEmpty(since) ? "全部" : since)}");
        await _outputManager.OutputInfoAsync($"- 结束日期(--until): {until} (不包含当天)");

        // 输出时间格式相关信息
        await _outputManager.OutputInfoAsync("注意: Git日期格式处理方式是:");
        await _outputManager.OutputInfoAsync("- --since参数是包含指定时间点的 (开始时间 >= since)");
        await _outputManager.OutputInfoAsync("- --until参数是不包含指定时间点的 (结束时间 < until)");

        // 判断是否有筛选条件
        bool hasFilters = !string.IsNullOrEmpty(since) || !string.IsNullOrEmpty(until) ||
                          !string.IsNullOrEmpty(author) || !string.IsNullOrEmpty(authorFilter);

        if (hasFilters)
        {
          await _outputManager.OutputInfoAsync("检测到筛选条件，将采用优化扫描策略");

          // 打印详细筛选条件
          if (!string.IsNullOrEmpty(since))
          {
            await _outputManager.UpdateOutputAsync($"- 开始时间: {since}");
          }
          if (!string.IsNullOrEmpty(until))
          {
            await _outputManager.UpdateOutputAsync($"- 结束时间: {until}");
          }
          if (!string.IsNullOrEmpty(author))
          {
            await _outputManager.UpdateOutputAsync($"- 作者筛选: {author}");
          }
          if (!string.IsNullOrEmpty(authorFilter))
          {
            await _outputManager.UpdateOutputAsync($"- 作者关键词筛选: {authorFilter}");
          }
        }

        // 增加进度到15%，表示日期处理阶段
        await _outputManager.UpdateProgressBarAsync(15);

        // 构建Git字段
        var gitFields = new List<string> { "CommitId", "Author", "Date", "Message" };

        // 构建Git格式字符串
        var gitFormatParts = new List<string>();
        var gitFieldMap = new Dictionary<string, string>
                {
                    { "CommitId", "%H" },
                    { "Author", "%an" },
                    { "Date", "%ad" },
                    { "Message", "%s" }
                };

        foreach (var field in gitFields)
        {
          if (gitFieldMap.ContainsKey(field))
          {
            gitFormatParts.Add(gitFieldMap[field]);
          }
        }
        string gitFormat = string.Join("|", gitFormatParts);

        // 增加进度到20%，表示准备阶段完成
        await _outputManager.UpdateProgressBarAsync(20);
        await _outputManager.OutputHighlightAsync("正在搜索Git仓库，请稍后...");

        // 实时打印扫描路径信息
        await _outputManager.OutputInfoAsync($"开始扫描指定的 {paths.Count} 个路径...");
        foreach (var path in paths)
        {
          await _outputManager.UpdateOutputAsync($"- 扫描路径: {path}");
        }

        // 并行收集所有仓库路径
        var gitRepos = new List<DirectoryInfo>();
        var validPaths = new List<string>();

        // 验证路径是否存在
        int validPathCount = 0;
        int invalidPathCount = 0;
        foreach (var path in paths)
        {
          if (Directory.Exists(path))
          {
            validPaths.Add(path);
            validPathCount++;
          }
          else
          {
            await _outputManager.OutputWarningAsync($"警告: 路径不存在: {path}");
            invalidPathCount++;
          }
        }

        await _outputManager.OutputInfoAsync($"路径验证结果: {validPathCount} 个有效路径, {invalidPathCount} 个无效路径");

        // 并行收集仓库
        ConcurrentBag<DirectoryInfo> reposBag = new ConcurrentBag<DirectoryInfo>();

        // 最大并行度 - 根据CPU核心数调整，避免过度并行
        int maxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1);
        await _outputManager.OutputInfoAsync($"使用并行度: {maxDegreeOfParallelism} 进行扫描");

        // 记录仓库搜索开始时间
        DateTime repoSearchStartTime = DateTime.Now;

        await Task.Run(() =>
        {
          Parallel.ForEach(validPaths, new ParallelOptions { MaxDegreeOfParallelism = maxDegreeOfParallelism }, path =>
          {
            if (!_isRunning) return;

            try
            {
              if (verifyGitPaths)
              {
                // 直接验证是否为Git仓库
                if (IsGitRepository(path))
                {
                  reposBag.Add(new DirectoryInfo(path));
                  _outputManager.UpdateOutput($"找到Git仓库: {path}");
                }
              }
              else
              {
                // 优化的仓库查找算法
                FindGitRepositoriesParallel(path, reposBag, maxDegreeOfParallelism);
              }
            }
            catch (Exception ex)
            {
              // 记录错误但继续处理其他路径
              _outputManager.UpdateOutput($"搜索路径时出错: {ex.Message}");
            }
          });
        });

        // 计算仓库搜索用时
        TimeSpan repoSearchTime = DateTime.Now - repoSearchStartTime;

        // 转换为列表并排序，确保结果一致性
        gitRepos = reposBag.ToList();

        // 移除重复的仓库（可能由于路径嵌套导致）
        int originalRepoCount = gitRepos.Count;
        gitRepos = gitRepos.GroupBy(repo => repo.FullName)
                          .Select(group => group.First())
                          .ToList();
        int uniqueRepoCount = gitRepos.Count;

        if (originalRepoCount > uniqueRepoCount)
        {
          await _outputManager.OutputInfoAsync($"移除了 {originalRepoCount - uniqueRepoCount} 个重复的Git仓库");
        }

        _repoCount = gitRepos.Count;
        await _outputManager.OutputSuccessAsync($"总共找到 {_repoCount} 个Git仓库 (用时: {FormatTimeSpan(repoSearchTime)})");

        // 如果没有找到仓库，设置为较高进度并退出
        if (_repoCount == 0)
        {
          await _outputManager.UpdateProgressBarAsync(90);
          await _outputManager.OutputWarningAsync("未找到任何Git仓库，扫描结束");
          return commits;
        }

        // 如果存在筛选条件，先进行预筛选以提高性能
        if (hasFilters)
        {
          DateTime preFilterStartTime = DateTime.Now;
          await _outputManager.OutputHighlightAsync("正在预筛选仓库...");
          List<DirectoryInfo> filteredRepos = new List<DirectoryInfo>();

          // 并行预筛选
          ConcurrentBag<DirectoryInfo> filteredReposBag = new ConcurrentBag<DirectoryInfo>();

          // 检查是否可以进行批量跳过大型仓库操作
          bool canSkipLargeRepos = !string.IsNullOrEmpty(since) &&
                                  (DateTime.TryParse(since, out DateTime sinceDateForSkipping) &&
                                   (DateTime.Now - sinceDateForSkipping).TotalDays <= 30); // 只针对近期时间范围执行优化

          await Task.Run(() =>
          {
            Parallel.ForEach(gitRepos, new ParallelOptions { MaxDegreeOfParallelism = maxDegreeOfParallelism }, repo =>
            {
              if (!_isRunning) return;

              try
              {
                // 检查仓库大小，对大型仓库执行轻量级检查
                bool isLargeRepo = false;
                if (canSkipLargeRepos)
                {
                  isLargeRepo = IsLargeRepository(repo);
                  if (isLargeRepo)
                  {
                    _outputManager.OutputInfo($"检测到大型仓库: {repo.FullName}，将使用轻量级扫描");
                  }
                }

                // 预检查仓库是否包含符合条件的提交
                if (TwoStageCheckRepository(repo, since, until, author))
                {
                  filteredReposBag.Add(repo);
                  _outputManager.OutputSuccess($"仓库符合筛选条件: {repo.FullName}");
                }
                else
                {
                  _outputManager.OutputInfo($"仓库不符合筛选条件，已跳过: {repo.FullName}");
                }
              }
              catch (Exception)
              {
                // 如果预检查失败，添加到结果中以确保不会漏掉
                filteredReposBag.Add(repo);
                _outputManager.OutputWarning($"仓库预检查失败，将包含在结果中: {repo.FullName}");
              }
            });
          });

          // 更新仓库列表为预筛选后的结果
          int originalCount = gitRepos.Count;
          gitRepos = filteredReposBag.ToList();
          _repoCount = gitRepos.Count;

          TimeSpan preFilterTime = DateTime.Now - preFilterStartTime;
          await _outputManager.OutputSuccessAsync($"预筛选完成 (用时: {FormatTimeSpan(preFilterTime)})");
          await _outputManager.OutputInfoAsync($"预筛选结果: 从 {originalCount} 个仓库中筛选出 {_repoCount} 个符合条件的仓库");
        }

        // 设置为40%进度，表示开始处理仓库
        await _outputManager.UpdateProgressBarAsync(40);

        // 并行处理所有Git仓库
        ConcurrentBag<CommitInfo> commitsBag = new ConcurrentBag<CommitInfo>();
        ConcurrentBag<DateTime?> createDatesBag = new ConcurrentBag<DateTime?>();

        // 记录提交扫描开始时间
        DateTime commitScanStartTime = DateTime.Now;

        // 创建进度报告对象
        var progress = new Progress<ProgressReport>(report =>
        {
          // 报告进度
          int percentComplete = 40 + (int)((report.CompletedItems / (double)_repoCount) * 55);
          _outputManager.UpdateProgressBar(percentComplete);
          _outputManager.OutputHighlight($"处理仓库 [{report.CompletedItems}/{_repoCount}]: {report.CurrentRepo}");
        });

        // 用于并行处理的计数器
        var completedCounter = new CounterProgress(_repoCount, progress);

        // 维护一个仓库处理状态字典，用于统计性能数据
        ConcurrentDictionary<string, (int CommitCount, TimeSpan ProcessTime, bool IsLarge)> repoProcessStats =
            new ConcurrentDictionary<string, (int, TimeSpan, bool)>();

        // 创建仓库结果字典，为每个仓库创建独立的结果集实例
        ConcurrentDictionary<string, List<CommitInfo>> repoCommitsDict =
            new ConcurrentDictionary<string, List<CommitInfo>>();

        // 检查是否可以使用轻量级模式
        bool useLightweightMode = !string.IsNullOrEmpty(since) &&
                              (DateTime.TryParse(since, out DateTime lightModeSinceDate) &&
                               (DateTime.Now - lightModeSinceDate).TotalDays <= 30);

        if (useLightweightMode)
        {
          await _outputManager.OutputInfoAsync("检测到近期时间范围查询，将采用轻量级扫描模式");
        }

        await Task.Run(() =>
        {
          Parallel.ForEach(gitRepos, new ParallelOptions { MaxDegreeOfParallelism = maxDegreeOfParallelism }, repo =>
          {
            if (!_isRunning) return;

            try
            {
              // 报告当前处理的仓库
              completedCounter.ReportProgress(repo.FullName);

              // 记录仓库处理开始时间
              DateTime repoStartTime = DateTime.Now;

              // 判断是否为大型仓库
              bool isLargeRepo = false;
              if (useLightweightMode)
              {
                isLargeRepo = IsLargeRepository(repo);
              }

              // 处理单个仓库，为每个仓库创建独立的结果集
              var repoCommits = ProcessSingleRepositoryParallel(repo, since, until, author, authorFilter, gitFields, gitFormat, isLargeRepo);

              // 保存仓库的提交到字典中，确保每个仓库有独立的结果集
              if (repoCommits.Count > 0)
              {
                // 使用仓库的全路径作为键，确保唯一性
                repoCommitsDict.TryAdd(repo.FullName, repoCommits);
              }

              // 记录仓库处理完成时间和统计信息
              TimeSpan repoProcessTime = DateTime.Now - repoStartTime;
              repoProcessStats.TryAdd(repo.FullName, (repoCommits.Count, repoProcessTime, isLargeRepo));

              // 添加此仓库的提交到总集合
              if (repoCommits.Count > 0)
              {
                string infoMessage = isLargeRepo
                    ? $"  - 从大型仓库 '{repo.Name}' 中找到 {repoCommits.Count} 个提交 (用时: {FormatTimeSpan(repoProcessTime)})"
                    : $"  - 从仓库 '{repo.Name}' 中找到 {repoCommits.Count} 个提交 (用时: {FormatTimeSpan(repoProcessTime)})";

                _outputManager.OutputSuccess(infoMessage);

                // 将仓库的提交添加到总结果集
                foreach (var commit in repoCommits)
                {
                  commitsBag.Add(commit);
                }
              }
              else
              {
                _outputManager.OutputInfo($"  - 仓库 '{repo.Name}' 中未找到符合条件的提交 (用时: {FormatTimeSpan(repoProcessTime)})");
              }

              // 获取仓库创建日期
              DateTime? repoCreateDate = GetRepositoryCreateDateParallel(repo);
              if (repoCreateDate.HasValue)
              {
                createDatesBag.Add(repoCreateDate);
                _outputManager.UpdateOutput($"  - 仓库创建时间: {repoCreateDate.Value.ToString("yyyy-MM-dd HH:mm:ss")}");
              }
            }
            catch (Exception ex)
            {
              _outputManager.OutputError($"警告: 处理仓库 '{repo.FullName}' 时出错: {ex.Message}");
            }
          });
        });

        // 计算提交扫描用时
        TimeSpan commitScanTime = DateTime.Now - commitScanStartTime;

        // 收集所有提交
        commits = commitsBag.ToList();

        // 增加仓库分组排序逻辑，确保同一仓库的提交保持在一起
        await _outputManager.OutputInfoAsync("正在对提交记录进行分组和排序...");

        // 先按仓库分组，确保同一仓库的提交保持在一起
        var commitsByRepo = commits
            .GroupBy(c => c.RepoPath ?? "Unknown")
            .OrderBy(g => g.Key)
            .ToList();

        // 创建新的排序后的列表
        List<CommitInfo> sortedCommits = new List<CommitInfo>();

        // 对每个仓库组单独排序，然后添加到结果列表
        foreach (var repoGroup in commitsByRepo)
        {
          string repoPath = repoGroup.Key;
          // 该仓库的提交按日期降序排序
          var repoCommitsSorted = repoGroup
              .OrderByDescending(c =>
              {
                if (DateTime.TryParse(c.Date, out DateTime date))
                  return date;
                return DateTime.MinValue;
              })
              .ToList();

          // 验证该仓库的所有提交是否有正确的仓库信息
          string repoName = Path.GetFileName(repoPath);
          foreach (var commit in repoCommitsSorted)
          {
            if (string.IsNullOrEmpty(commit.Repository))
            {
              commit.Repository = repoName;
            }
            if (string.IsNullOrEmpty(commit.RepoPath))
            {
              commit.RepoPath = repoPath;
            }
            if (string.IsNullOrEmpty(commit.RepoFolder))
            {
              commit.RepoFolder = repoName;
            }
          }

          // 添加到结果列表
          sortedCommits.AddRange(repoCommitsSorted);

          // 输出每个仓库的提交数量，帮助调试
          await _outputManager.OutputInfoAsync($"仓库 '{repoName}' 有 {repoCommitsSorted.Count} 个提交");
        }

        // 用排序后的列表替换原来的列表
        commits = sortedCommits;

        // 找出最早的仓库创建日期
        DateTime? earliestRepoDate = null;
        if (createDatesBag.Count > 0)
        {
          earliestRepoDate = createDatesBag.Min();
        }

        // 替换进度条完成消息
        await _outputManager.OutputSuccessAsync("所有仓库处理完成 (100%)");

        // 输出性能统计信息
        if (repoProcessStats.Count > 0)
        {
          // 找出处理最慢的仓库
          var slowestRepo = repoProcessStats.OrderByDescending(kv => kv.Value.ProcessTime).First();
          // 找出提交量最大的仓库
          var largestRepo = repoProcessStats.OrderByDescending(kv => kv.Value.CommitCount).First();

          await _outputManager.OutputHighlightAsync("==== 性能详情 ====");
          await _outputManager.OutputInfoAsync($"处理最慢的仓库: {Path.GetFileName(slowestRepo.Key)} (用时: {FormatTimeSpan(slowestRepo.Value.ProcessTime)})");
          await _outputManager.OutputInfoAsync($"提交量最大的仓库: {Path.GetFileName(largestRepo.Key)} (提交数: {largestRepo.Value.CommitCount})");

          // 大型仓库统计
          int largeRepoCount = repoProcessStats.Count(kv => kv.Value.IsLarge);
          if (largeRepoCount > 0)
          {
            await _outputManager.OutputInfoAsync($"大型仓库数量: {largeRepoCount} (使用了轻量级扫描模式)");
          }
        }

        // 计算总扫描用时
        TimeSpan totalScanTime = DateTime.Now - scanStartTime;

        // 输出扫描信息
        await _outputManager.OutputHighlightAsync("==== 扫描信息 ====");
        if (string.IsNullOrEmpty(since))
        {
          if (earliestRepoDate.HasValue)
          {
            string earliestDateStr = earliestRepoDate.Value.ToString("yyyy-MM-dd HH:mm:ss");
            await _outputManager.OutputInfoAsync($"时间范围: 从仓库创建开始 ({earliestDateStr}) 至 {DateTime.Now.ToString("yyyy-MM-dd")}");
          }
          else
          {
            await _outputManager.OutputInfoAsync($"时间范围: 从仓库创建开始 至 {DateTime.Now.ToString("yyyy-MM-dd")}");
          }
        }
        else
        {
          // 显示真实的日期范围，而不是调整后的日期
          string displayUntil = DateTime.Parse(until).AddDays(-1).ToString("yyyy-MM-dd");
          await _outputManager.OutputInfoAsync($"时间范围: {since} 至 {displayUntil}");
        }

        if (string.IsNullOrEmpty(author))
        {
          await _outputManager.OutputInfoAsync("提交作者: 所有作者");
        }
        else
        {
          await _outputManager.OutputInfoAsync($"提交作者: {author}");
        }

        if (!string.IsNullOrEmpty(authorFilter))
        {
          await _outputManager.OutputInfoAsync($"作者过滤: {authorFilter}");
        }

        await _outputManager.OutputInfoAsync($"提取的Git字段: {string.Join(", ", gitFields)}");

        // 输出性能统计信息
        await _outputManager.OutputHighlightAsync("==== 性能统计 ====");
        await _outputManager.OutputInfoAsync($"仓库搜索用时: {FormatTimeSpan(repoSearchTime)}");
        if (hasFilters)
        {
          await _outputManager.OutputInfoAsync($"仓库预筛选用时: {FormatTimeSpan(DateTime.Now - repoSearchStartTime - repoSearchTime)}");
        }
        await _outputManager.OutputInfoAsync($"提交扫描用时: {FormatTimeSpan(commitScanTime)}");
        await _outputManager.OutputInfoAsync($"总扫描用时: {FormatTimeSpan(totalScanTime)}");
        await _outputManager.OutputHighlightAsync("===================");

        // 输出结果数量
        await _outputManager.OutputSuccessAsync($"在指定时间范围内共找到 {commits.Count} 个提交");

        // 添加到最近使用的作者
        if (!string.IsNullOrEmpty(author))
        {
          _authorManager.AddToRecentAuthors(author);
        }

        // 设置进度条到完成状态
        await _outputManager.UpdateProgressBarAsync(100);
        await _outputManager.OutputSuccessAsync($"Git提交记录收集完成，总用时: {FormatTimeSpan(totalScanTime)}");
      }
      catch (Exception ex)
      {
        await _outputManager.OutputErrorAsync($"收集Git提交时出错: {ex.Message}");
      }
      finally
      {
        _isRunning = false;
        await _outputManager.HideProgressBarAsync();
      }

      return commits;
    }

    /// <summary>
    /// 格式化TimeSpan为易读格式
    /// </summary>
    private string FormatTimeSpan(TimeSpan time)
    {
      if (time.TotalHours >= 1)
      {
        return $"{(int)time.TotalHours}小时{time.Minutes}分{time.Seconds}秒";
      }
      else if (time.TotalMinutes >= 1)
      {
        return $"{time.Minutes}分{time.Seconds}秒";
      }
      else
      {
        return $"{time.Seconds}秒";
      }
    }

    // 新增方法：异步处理单个Git仓库
    private async Task<List<CommitInfo>> ProcessSingleRepositoryAsync(DirectoryInfo repo, string since, string until, string author, string authorFilter, List<string> gitFields, string gitFormat)
    {
      // 为每个仓库创建独立的结果集实例
      List<CommitInfo> repoCommits = new List<CommitInfo>();

      // 使用Task.Run在后台线程执行这个操作，避免阻塞UI线程
      await Task.Run(async () =>
      {
        try
        {
          string currentDirectory = Directory.GetCurrentDirectory();
          Directory.SetCurrentDirectory(repo.FullName);

          // 构建Git命令参数
          var arguments = "log";

          if (!string.IsNullOrEmpty(since))
          {
            arguments += $" --since=\"{since}\"";
          }

          arguments += $" --until=\"{until}\"";
          arguments += $" --pretty=format:\"{gitFormat}\"";
          arguments += " --date=format:\"%Y-%m-%d %H:%M:%S\"";

          // 仅当author不为空或空白字符时才添加到git命令中
          if (!string.IsNullOrWhiteSpace(author))
          {
            arguments += $" --author=\"{author}\"";
          }

          // 输出完整git命令，帮助调试
          _outputManager.OutputInfo($"执行Git命令: git {arguments}");

          // 添加日期处理的详细日志
          _outputManager.OutputInfo("---------- 日期处理详情 ----------");
          _outputManager.OutputInfo(!string.IsNullOrEmpty(since)
              ? $"开始日期参数: {since}"
              : "未指定开始日期");
          _outputManager.OutputInfo(!string.IsNullOrEmpty(until)
              ? $"结束日期参数: {until}"
              : "未指定结束日期");
          _outputManager.OutputInfo("----------------------------------");

          // 执行Git命令
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

          if (!string.IsNullOrEmpty(output))
          {
            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            // 创建仓库特定的结果集
            List<CommitInfo> repoSpecificCommits = new List<CommitInfo>();

            foreach (var line in lines)
            {
              // 检查是否已手动停止
              if (!_isRunning)
              {
                await _outputManager.UpdateOutputAsync("处理提交数据过程已手动停止");
                Directory.SetCurrentDirectory(currentDirectory);
                return;
              }

              if (string.IsNullOrEmpty(line))
                continue;

              try
              {
                var parts = line.Split('|');
                if (parts.Length >= gitFields.Count)
                {
                  // 创建一个提交信息对象，确保包含完整的仓库信息
                  var commitObj = new CommitInfo
                  {
                    Repository = repo.Name,
                    RepoPath = repo.FullName,
                    RepoFolder = Path.GetFileName(repo.FullName)
                  };

                  // 根据选择的字段添加属性
                  for (int i = 0; i < gitFields.Count; i++)
                  {
                    if (i < parts.Length) // 确保下标不会越界
                    {
                      var fieldName = gitFields[i];
                      var fieldValue = parts[i];

                      switch (fieldName)
                      {
                        case "CommitId":
                          commitObj.CommitId = fieldValue;
                          break;
                        case "Author":
                          commitObj.Author = fieldValue;
                          break;
                        case "Date":
                          commitObj.Date = fieldValue;
                          break;
                        case "Message":
                          commitObj.Message = fieldValue;
                          break;
                      }
                    }
                  }

                  // 应用作者筛选
                  bool shouldInclude = true;
                  if (!string.IsNullOrEmpty(authorFilter) && commitObj.Author != null)
                  {
                    shouldInclude = false;
                    var authorPatterns = authorFilter.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(p => p.Trim().ToLower())
                        .Where(p => !string.IsNullOrEmpty(p))
                        .ToArray();

                    // 如果没有有效的过滤模式，默认包含所有
                    if (authorPatterns.Length == 0)
                    {
                      shouldInclude = true;
                    }
                    else
                    {
                      // 优化字符串比较 - 大小写不敏感
                      string authorLower = commitObj.Author.ToLower();

                      // 检查每个过滤模式
                      foreach (var pattern in authorPatterns)
                      {
                        if (authorLower.Contains(pattern))
                        {
                          shouldInclude = true;
                          break;
                        }
                      }
                    }
                  }

                  if (shouldInclude)
                  {
                    repoSpecificCommits.Add(commitObj);
                  }
                }
              }
              catch (Exception ex)
              {
                await _outputManager.UpdateOutputAsync($"处理提交记录时出错: {ex.Message}");
              }
            }

            // 将这个仓库特定的结果添加到总结果中
            repoCommits.AddRange(repoSpecificCommits);
          }

          // 恢复当前目录
          Directory.SetCurrentDirectory(currentDirectory);
        }
        catch (Exception ex)
        {
          await _outputManager.UpdateOutputAsync($"处理仓库数据时出错: {ex.Message}");
        }
      });

      return repoCommits;
    }

    // 新增方法：获取仓库创建日期
    private async Task<DateTime?> GetRepositoryCreateDateAsync(DirectoryInfo repo)
    {
      DateTime? repoCreateDate = null;

      await Task.Run(() =>
      {
        try
        {
          string currentDirectory = Directory.GetCurrentDirectory();
          Directory.SetCurrentDirectory(repo.FullName);

          var firstCommitProcess = new Process
          {
            StartInfo = new ProcessStartInfo
            {
              FileName = "git",
              Arguments = "log --reverse --format=\"%ad\" --date=format:\"%Y-%m-%d %H:%M:%S\"",
              UseShellExecute = false,
              RedirectStandardOutput = true,
              CreateNoWindow = true,
              StandardOutputEncoding = Encoding.UTF8
            }
          };

          firstCommitProcess.Start();
          string firstCommitDateStr = firstCommitProcess.StandardOutput.ReadLine();
          firstCommitProcess.WaitForExit();

          if (!string.IsNullOrEmpty(firstCommitDateStr))
          {
            repoCreateDate = DateTime.ParseExact(firstCommitDateStr, "yyyy-MM-dd HH:mm:ss", null);
          }

          Directory.SetCurrentDirectory(currentDirectory);
        }
        catch
        {
          // 忽略错误，返回null
        }
      });

      return repoCreateDate;
    }

    // 新增方法：并行处理仓库
    private void FindGitRepositoriesParallel(string path, ConcurrentBag<DirectoryInfo> reposBag, int maxDegreeOfParallelism)
    {
      try
      {
        if (!Directory.Exists(path))
        {
          return;
        }

        // 首先检查当前目录是否是Git仓库
        if (IsGitRepository(path))
        {
          reposBag.Add(new DirectoryInfo(path));
          return; // 如果当前目录是Git仓库，不需要再查找子目录
        }

        // 获取第一级子目录
        DirectoryInfo[] subDirs;
        try
        {
          subDirs = new DirectoryInfo(path).GetDirectories();
        }
        catch (Exception)
        {
          // 处理权限不足等错误
          return;
        }

        // 并行处理子目录
        Parallel.ForEach(subDirs, new ParallelOptions { MaxDegreeOfParallelism = maxDegreeOfParallelism }, subDir =>
        {
          if (!_isRunning) return;

          try
          {
            // 检查子目录是否包含.git
            string gitDir = Path.Combine(subDir.FullName, ".git");
            if (Directory.Exists(gitDir))
            {
              reposBag.Add(subDir);
            }
            else
            {
              // 递归处理子目录
              FindGitRepositoriesParallel(subDir.FullName, reposBag, maxDegreeOfParallelism);
            }
          }
          catch (Exception)
          {
            // 忽略单个子目录的错误，继续处理其他目录
          }
        });
      }
      catch (Exception)
      {
        // 处理异常但不中断程序
      }
    }

    // 新增方法：并行处理单个仓库
    private List<CommitInfo> ProcessSingleRepositoryParallel(DirectoryInfo repo, string since, string until, string author, string authorFilter, List<string> gitFields, string gitFormat, bool isLargeRepo = false)
    {
      // 为每个仓库创建独立的结果集实例
      List<CommitInfo> repoCommits = new List<CommitInfo>();

      try
      {
        string currentDirectory = Directory.GetCurrentDirectory();

        try
        {
          // 记录当前仓库信息，确保不会丢失
          string repoName = repo.Name;
          string repoFullPath = repo.FullName;
          string repoFolderName = Path.GetFileName(repo.FullName);

          // 输出当前处理的仓库信息，帮助调试
          _outputManager.OutputInfo($"开始处理仓库: {repoName} ({repoFullPath})");

          // 输出时间范围信息，帮助跟踪问题
          _outputManager.OutputInfo($"使用时间范围 - 开始: {(string.IsNullOrEmpty(since) ? "全部" : since)}, 结束: {(string.IsNullOrEmpty(until) ? "今天" : until)}");

          Directory.SetCurrentDirectory(repo.FullName);

          // 构建Git命令参数
          var arguments = "log";

          // 处理开始日期参数 (since)
          if (!string.IsNullOrEmpty(since))
          {
            string sinceParam = since;

            // 如果since不包含具体时间，添加00:00:00确保包含整天
            if (!since.Contains(":"))
            {
              sinceParam = $"{since} 00:00:00";
            }

            arguments += $" --since=\"{sinceParam}\"";
            _outputManager.OutputInfo($"使用开始时间参数: --since=\"{sinceParam}\"");
          }

          // 处理结束日期参数 (until)
          if (!string.IsNullOrEmpty(until))
          {
            string untilParam = until;

            // 如果until不包含具体时间，使用00:00:00
            if (!until.Contains(":"))
            {
              DateTime parsedDate;
              if (DateTime.TryParse(until, out parsedDate))
              {
                // 对于日期格式的until，使用整天的表示
                untilParam = $"{until} 00:00:00";
              }
            }

            arguments += $" --until=\"{untilParam}\"";
            _outputManager.OutputInfo($"使用结束时间参数: --until=\"{untilParam}\"");
          }

          arguments += $" --pretty=format:\"{gitFormat}\"";
          arguments += " --date=format:\"%Y-%m-%d %H:%M:%S\"";

          // 性能优化 - 添加命令选项，避免遍历所有对象
          arguments += " --no-walk=sorted";

          // 批量处理，提高性能，默认单次获取1000个
          // 对于大型仓库，使用--max-count限制结果数量
          if (isLargeRepo)
          {
            arguments += " -n500"; // 对大型仓库限制为前500个提交
            _outputManager.OutputInfo($"对大型仓库 {repoName} 限制为最近500个提交");
          }
          else
          {
            arguments += " -n1000"; // 标准仓库批量获取1000个
          }

          // 仅当author不为空或空白字符时才添加到git命令中
          if (!string.IsNullOrWhiteSpace(author))
          {
            arguments += $" --author=\"{author}\"";
          }

          // 使用多线程和优化的命令选项
          if (isLargeRepo)
          {
            // 对大型仓库直接使用HEAD，避免扫描所有分支
            arguments += " HEAD";
          }
          else
          {
            // 对普通仓库包含所有引用
            arguments += " --all";
          }

          // 输出完整git命令，帮助调试
          _outputManager.OutputInfo($"执行Git命令: git {arguments}");

          // 添加日期处理的详细日志
          _outputManager.OutputInfo("---------- 日期处理详情 ----------");
          _outputManager.OutputInfo(!string.IsNullOrEmpty(since)
              ? $"开始日期参数: {since}"
              : "未指定开始日期");
          _outputManager.OutputInfo(!string.IsNullOrEmpty(until)
              ? $"结束日期参数: {until}"
              : "未指定结束日期");
          _outputManager.OutputInfo("----------------------------------");

          // 执行Git命令
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

          // 同步读取输出，避免内存问题
          List<string> lines = new List<string>();
          while (!process.StandardOutput.EndOfStream)
          {
            if (!_isRunning) break;

            var line = process.StandardOutput.ReadLine();
            if (!string.IsNullOrEmpty(line))
            {
              lines.Add(line);
            }
          }

          process.WaitForExit();

          // 检查是否有错误
          string error = process.StandardError.ReadToEnd();
          if (!string.IsNullOrEmpty(error))
          {
            _outputManager.OutputWarning($"Git命令执行时出现警告: {error}");
          }

          // 创建仓库特定的并发集合，确保结果互不干扰
          ConcurrentBag<CommitInfo> repoSpecificCommitsBag = new ConcurrentBag<CommitInfo>();

          // 记录处理的行数，帮助调试
          int processedLines = 0;
          int addedCommits = 0;

          Parallel.ForEach(lines, line =>
          {
            if (!_isRunning) return;

            if (string.IsNullOrEmpty(line)) return;

            try
            {
              Interlocked.Increment(ref processedLines);

              var parts = line.Split('|');
              if (parts.Length >= gitFields.Count)
              {
                // 创建一个提交信息对象，确保包含完整的仓库信息
                var commitObj = new CommitInfo
                {
                  // 使用前面保存的仓库信息，确保始终一致
                  Repository = repoName,
                  RepoPath = repoFullPath,
                  RepoFolder = repoFolderName
                };

                // 根据选择的字段添加属性
                for (int i = 0; i < gitFields.Count; i++)
                {
                  if (i < parts.Length) // 确保下标不会越界
                  {
                    var fieldName = gitFields[i];
                    var fieldValue = parts[i];

                    switch (fieldName)
                    {
                      case "CommitId":
                        commitObj.CommitId = fieldValue;
                        break;
                      case "Author":
                        commitObj.Author = fieldValue;
                        break;
                      case "Date":
                        commitObj.Date = fieldValue;
                        break;
                      case "Message":
                        commitObj.Message = fieldValue;
                        break;
                    }
                  }
                }

                // 验证提交日期是否在指定范围内
                bool isInDateRange = true;
                if (!string.IsNullOrEmpty(since) || !string.IsNullOrEmpty(until))
                {
                  if (DateTime.TryParse(commitObj.Date, out DateTime commitDate))
                  {
                    // 检查开始日期范围
                    if (!string.IsNullOrEmpty(since) && DateTime.TryParse(since, out DateTime sinceDate))
                    {
                      // since参数是包含当天的，所以判断 >= 
                      if (commitDate < sinceDate)
                      {
                        isInDateRange = false;
                      }
                    }

                    // 检查结束日期范围
                    if (!string.IsNullOrEmpty(until) && DateTime.TryParse(until, out DateTime untilDate))
                    {
                      // until参数是不包含的，注意这里的判断是 >= (不包含until当天)
                      // TimeRangeManager已经在传入时将日期+1天了，这里只需要检查是否达到了until日期
                      if (commitDate >= untilDate)
                      {
                        isInDateRange = false;
                      }
                    }
                  }
                }

                // 如果日期不在范围内，跳过此提交
                if (!isInDateRange)
                {
                  return;
                }

                // 应用作者筛选
                bool shouldInclude = true;
                if (!string.IsNullOrEmpty(authorFilter) && commitObj.Author != null)
                {
                  shouldInclude = false;
                  var authorPatterns = authorFilter.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                      .Select(p => p.Trim().ToLower())
                      .Where(p => !string.IsNullOrEmpty(p))
                      .ToArray();

                  // 如果没有有效的过滤模式，默认包含所有
                  if (authorPatterns.Length == 0)
                  {
                    shouldInclude = true;
                  }
                  else
                  {
                    // 优化字符串比较 - 大小写不敏感
                    string authorLower = commitObj.Author.ToLower();

                    // 检查每个过滤模式
                    foreach (var pattern in authorPatterns)
                    {
                      if (authorLower.Contains(pattern))
                      {
                        shouldInclude = true;
                        break;
                      }
                    }
                  }
                }

                if (shouldInclude)
                {
                  repoSpecificCommitsBag.Add(commitObj);
                  Interlocked.Increment(ref addedCommits);
                }
              }
            }
            catch (Exception ex)
            {
              // 记录解析错误但继续处理
              _outputManager.OutputWarning($"解析提交行时出错: {ex.Message}");
            }
          });

          // 转换为列表，并对提交按日期排序，确保每个仓库内部的时间顺序正确
          repoCommits = repoSpecificCommitsBag.ToList();

          // 对提交按日期降序排序
          repoCommits = repoCommits.OrderByDescending(c =>
          {
            if (DateTime.TryParse(c.Date, out DateTime date))
              return date;
            return DateTime.MinValue;
          }).ToList();

          // 输出处理统计信息
          _outputManager.OutputInfo($"仓库 {repoName} 处理完成: 共处理 {processedLines} 行，添加 {addedCommits} 个提交");

          // 再次验证所有提交的仓库信息是否正确
          foreach (var commit in repoCommits)
          {
            // 确保没有空值
            if (string.IsNullOrEmpty(commit.Repository))
            {
              commit.Repository = repoName;
            }
            if (string.IsNullOrEmpty(commit.RepoPath))
            {
              commit.RepoPath = repoFullPath;
            }
            if (string.IsNullOrEmpty(commit.RepoFolder))
            {
              commit.RepoFolder = repoFolderName;
            }
          }
        }
        finally
        {
          // 恢复当前目录
          Directory.SetCurrentDirectory(currentDirectory);
        }
      }
      catch (Exception ex)
      {
        // 处理仓库级别的错误，记录错误信息
        _outputManager.OutputError($"处理仓库 '{repo.FullName}' 时发生错误: {ex.Message}");
        // 返回空列表，确保不会因为一个仓库的错误影响其他仓库
        repoCommits = new List<CommitInfo>();
      }

      return repoCommits;
    }

    // 新增方法：获取仓库创建日期
    private DateTime? GetRepositoryCreateDateParallel(DirectoryInfo repo)
    {
      DateTime? repoCreateDate = null;

      try
      {
        string currentDirectory = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(repo.FullName);

        // 优化Git命令，只获取第一个提交的日期
        var firstCommitProcess = new Process
        {
          StartInfo = new ProcessStartInfo
          {
            FileName = "git",
            Arguments = "log --reverse --format=\"%ad\" --date=format:\"%Y-%m-%d %H:%M:%S\" -n1",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8
          }
        };

        firstCommitProcess.Start();
        string firstCommitDateStr = firstCommitProcess.StandardOutput.ReadLine();
        firstCommitProcess.WaitForExit();

        if (!string.IsNullOrEmpty(firstCommitDateStr))
        {
          repoCreateDate = DateTime.ParseExact(firstCommitDateStr, "yyyy-MM-dd HH:mm:ss", null);
        }

        Directory.SetCurrentDirectory(currentDirectory);
      }
      catch
      {
        // 忽略错误，返回null
      }

      return repoCreateDate;
    }

    // 新增方法：两阶段筛选策略 - 时间优先，作者次之
    private bool TwoStageCheckRepository(DirectoryInfo repo, string since, string until, string author)
    {
      try
      {
        string currentDirectory = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(repo.FullName);

        // 第一阶段：仅基于时间范围进行快速筛选
        if (!string.IsNullOrEmpty(since) || !string.IsNullOrEmpty(until))
        {
          var timeCheckArguments = "log -n1";  // 只检查是否有至少一个提交符合条件

          if (!string.IsNullOrEmpty(since))
          {
            timeCheckArguments += $" --since=\"{since}\"";
          }

          if (!string.IsNullOrEmpty(until))
          {
            timeCheckArguments += $" --until=\"{until}\"";
          }

          // 执行时间范围筛选
          var timeCheckProcess = new Process
          {
            StartInfo = new ProcessStartInfo
            {
              FileName = "git",
              Arguments = timeCheckArguments,
              UseShellExecute = false,
              RedirectStandardOutput = true,
              CreateNoWindow = true,
              StandardOutputEncoding = Encoding.UTF8
            }
          };

          timeCheckProcess.Start();
          string timeCheckOutput = timeCheckProcess.StandardOutput.ReadToEnd();
          timeCheckProcess.WaitForExit();

          // 如果时间范围筛选未通过，直接返回false
          if (string.IsNullOrWhiteSpace(timeCheckOutput))
          {
            Directory.SetCurrentDirectory(currentDirectory);
            return false;
          }
        }

        // 第二阶段：基于作者进行筛选（只有通过时间筛选的仓库才会到这里）
        if (!string.IsNullOrWhiteSpace(author))
        {
          var authorCheckArguments = "log -n1";

          // 添加时间参数以确保结果一致性
          if (!string.IsNullOrEmpty(since))
          {
            authorCheckArguments += $" --since=\"{since}\"";
          }

          if (!string.IsNullOrEmpty(until))
          {
            authorCheckArguments += $" --until=\"{until}\"";
          }

          // 添加作者参数
          authorCheckArguments += $" --author=\"{author}\"";

          // 执行作者筛选
          var authorCheckProcess = new Process
          {
            StartInfo = new ProcessStartInfo
            {
              FileName = "git",
              Arguments = authorCheckArguments,
              UseShellExecute = false,
              RedirectStandardOutput = true,
              CreateNoWindow = true,
              StandardOutputEncoding = Encoding.UTF8
            }
          };

          authorCheckProcess.Start();
          string authorCheckOutput = authorCheckProcess.StandardOutput.ReadToEnd();
          authorCheckProcess.WaitForExit();

          // 恢复当前目录
          Directory.SetCurrentDirectory(currentDirectory);

          // 返回作者筛选结果
          return !string.IsNullOrWhiteSpace(authorCheckOutput);
        }

        // 恢复当前目录
        Directory.SetCurrentDirectory(currentDirectory);

        // 如果没有作者筛选，则已经通过了时间筛选
        return true;
      }
      catch (Exception ex)
      {
        _outputManager.OutputWarning($"预检查仓库时出错: {ex.Message}");
        // 如果出错，保守地返回true以便仓库能被包含在完整扫描中
        return true;
      }
    }

    /// <summary>
    /// 检查是否为大型仓库
    /// </summary>
    private bool IsLargeRepository(DirectoryInfo repo)
    {
      try
      {
        string currentDirectory = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(repo.FullName);

        // 检查提交数量
        var commitCountProcess = new Process
        {
          StartInfo = new ProcessStartInfo
          {
            FileName = "git",
            Arguments = "rev-list --count --all",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8
          }
        };

        commitCountProcess.Start();
        string countOutput = commitCountProcess.StandardOutput.ReadToEnd().Trim();
        commitCountProcess.WaitForExit();

        // 恢复当前目录
        Directory.SetCurrentDirectory(currentDirectory);

        // 如果提交数量大于5000，视为大型仓库
        if (int.TryParse(countOutput, out int commitCount))
        {
          return commitCount > 5000;
        }

        return false;
      }
      catch
      {
        return false;
      }
    }
  }
}