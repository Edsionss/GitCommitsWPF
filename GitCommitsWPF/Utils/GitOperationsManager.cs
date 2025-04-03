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
          until = DateTime.Now.AddDays(1).ToString("yyyy-MM-dd"); // 设置到明天来包含今天的提交
        }

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

          await Task.Run(() =>
          {
            Parallel.ForEach(gitRepos, new ParallelOptions { MaxDegreeOfParallelism = maxDegreeOfParallelism }, repo =>
            {
              if (!_isRunning) return;

              try
              {
                // 预检查仓库是否包含符合条件的提交
                if (PreCheckRepository(repo, since, until, author))
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

        await Task.Run(() =>
        {
          Parallel.ForEach(gitRepos, new ParallelOptions { MaxDegreeOfParallelism = maxDegreeOfParallelism }, repo =>
          {
            if (!_isRunning) return;

            try
            {
              // 报告当前处理的仓库
              completedCounter.ReportProgress(repo.FullName);

              // 处理单个仓库
              var repoCommits = ProcessSingleRepositoryParallel(repo, since, until, author, authorFilter, gitFields, gitFormat);

              // 添加此仓库的提交
              if (repoCommits.Count > 0)
              {
                _outputManager.OutputSuccess($"  - 从仓库 '{repo.Name}' 中找到 {repoCommits.Count} 个提交");
                foreach (var commit in repoCommits)
                {
                  commitsBag.Add(commit);
                }
              }
              else
              {
                _outputManager.OutputInfo($"  - 仓库 '{repo.Name}' 中未找到符合条件的提交");
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

        // 按日期排序提交
        if (commits.Count > 0)
        {
          await _outputManager.OutputInfoAsync("正在对提交记录进行排序...");
          commits = commits.OrderByDescending(c =>
          {
            if (DateTime.TryParse(c.Date, out DateTime date))
              return date;
            return DateTime.MinValue;
          }).ToList();
        }

        // 找出最早的仓库创建日期
        DateTime? earliestRepoDate = null;
        if (createDatesBag.Count > 0)
        {
          earliestRepoDate = createDatesBag.Min();
        }

        // 替换进度条完成消息
        await _outputManager.OutputSuccessAsync("所有仓库处理完成 (100%)");

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

          if (!string.IsNullOrEmpty(author))
          {
            arguments += $" --author=\"{author}\"";
          }

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
                  // 创建一个提交信息对象
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
                    var authors = authorFilter.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var authorPattern in authors)
                    {
                      string trimmedPattern = authorPattern.Trim();
                      if (!string.IsNullOrEmpty(trimmedPattern) &&
                                  commitObj.Author.IndexOf(trimmedPattern, StringComparison.OrdinalIgnoreCase) >= 0)
                      {
                        shouldInclude = true;
                        break;
                      }
                    }
                  }

                  if (shouldInclude)
                  {
                    repoCommits.Add(commitObj);
                  }
                }
              }
              catch (Exception ex)
              {
                await _outputManager.UpdateOutputAsync($"处理提交记录时出错: {ex.Message}");
              }
            }
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
    private List<CommitInfo> ProcessSingleRepositoryParallel(DirectoryInfo repo, string since, string until, string author, string authorFilter, List<string> gitFields, string gitFormat)
    {
      List<CommitInfo> repoCommits = new List<CommitInfo>();

      try
      {
        string currentDirectory = Directory.GetCurrentDirectory();

        try
        {
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

          // 性能优化 - 添加命令选项，避免遍历所有对象
          arguments += " --no-walk=sorted";

          // 批量处理，提高性能，单次获取1000个
          arguments += " -n1000";

          if (!string.IsNullOrEmpty(author))
          {
            arguments += $" --author=\"{author}\"";
          }

          // 使用多线程和优化的命令选项
          arguments += " --all"; // 包含所有引用

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

          // 批量处理提交记录，利用并行处理
          ConcurrentBag<CommitInfo> commitsBag = new ConcurrentBag<CommitInfo>();

          Parallel.ForEach(lines, line =>
          {
            if (!_isRunning) return;

            if (string.IsNullOrEmpty(line)) return;

            try
            {
              var parts = line.Split('|');
              if (parts.Length >= gitFields.Count)
              {
                // 创建一个提交信息对象
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
                  var authors = authorFilter.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                  // 优化字符串比较
                  string authorLower = commitObj.Author.ToLower();

                  foreach (var authorPattern in authors)
                  {
                    string trimmedPattern = authorPattern.Trim().ToLower();
                    if (!string.IsNullOrEmpty(trimmedPattern) &&
                               authorLower.Contains(trimmedPattern))
                    {
                      shouldInclude = true;
                      break;
                    }
                  }
                }

                if (shouldInclude)
                {
                  commitsBag.Add(commitObj);
                }
              }
            }
            catch
            {
              // 忽略单条解析错误
            }
          });

          // 转换为列表
          repoCommits = commitsBag.ToList();
        }
        finally
        {
          // 恢复当前目录
          Directory.SetCurrentDirectory(currentDirectory);
        }
      }
      catch (Exception)
      {
        // 处理仓库级别的错误
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

    // 新增方法：预检查仓库是否包含符合条件的提交
    private bool PreCheckRepository(DirectoryInfo repo, string since, string until, string author)
    {
      try
      {
        string currentDirectory = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(repo.FullName);

        // 构建快速检查的Git命令
        var arguments = "log -n1";  // 只检查是否有至少一个提交符合条件

        if (!string.IsNullOrEmpty(since))
        {
          arguments += $" --since=\"{since}\"";
        }

        if (!string.IsNullOrEmpty(until))
        {
          arguments += $" --until=\"{until}\"";
        }

        if (!string.IsNullOrEmpty(author))
        {
          arguments += $" --author=\"{author}\"";
        }

        // 执行Git命令进行快速检查
        var process = new Process
        {
          StartInfo = new ProcessStartInfo
          {
            FileName = "git",
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8
          }
        };

        process.Start();
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        // 恢复当前目录
        Directory.SetCurrentDirectory(currentDirectory);

        // 如果有输出，说明存在符合条件的提交
        return !string.IsNullOrWhiteSpace(output);
      }
      catch (Exception ex)
      {
        _outputManager.UpdateOutput($"预检查仓库时出错: {ex.Message}");
        // 如果出错，保守地返回true以便仓库能被包含在完整扫描中
        return true;
      }
    }
  }
}