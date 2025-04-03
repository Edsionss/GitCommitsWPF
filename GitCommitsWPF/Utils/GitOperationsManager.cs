using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GitCommitsWPF.Models;
using GitCommitsWPF.Services;

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
        // 异步扫描所有路径
        await Task.Run(() =>
        {
          int pathCount = paths.Count;
          int currentPath = 0;
          int totalGitRepos = 0;
          int processedRepos = 0;

          // 第一阶段：计算所有路径下的Git仓库总数 (分配总进度的20%)
          foreach (var path in paths)
          {
            if (!_isRunning) break;

            if (Directory.Exists(path))
            {
              _outputManager.UpdateOutput($"正在搜索路径: {path}");
              // 计算每个路径的搜索进度
              var repos = FindGitRepositories(path);
              totalGitRepos += repos.Count;

              // 更新进度，第一阶段分配10-30%的进度
              currentPath++;
              int searchProgress = 10 + (int)(20.0 * currentPath / pathCount);
              _outputManager.UpdateProgressBar(searchProgress);
            }
          }

          // 避免除以零错误
          if (totalGitRepos == 0)
          {
            totalGitRepos = 1;
            _outputManager.UpdateProgressBar(90);  // 如果没有仓库，进度直接到90%
          }

          _outputManager.UpdateOutput($"共找到 {totalGitRepos} 个Git仓库待扫描");
          // 扫描阶段起始进度为30%
          int startProgress = 30;

          // 重置当前路径计数器
          currentPath = 0;
          foreach (var path in paths)
          {
            if (!_isRunning) break;

            currentPath++;

            try
            {
              if (!Directory.Exists(path))
              {
                _outputManager.UpdateOutput($"警告: 路径不存在: {path}");
                continue;
              }

              _outputManager.UpdateOutput($"正在扫描路径 [{currentPath}/{pathCount}]: {path}");

              // 搜索Git仓库
              var gitRepos = FindGitRepositories(path);
              _outputManager.UpdateOutput($"在路径 '{path}' 下找到 {gitRepos.Count} 个Git仓库");

              // 从每个Git仓库获取作者信息
              foreach (var repo in gitRepos)
              {
                if (!_isRunning) break;

                processedRepos++;
                // 更新进度条，基于处理的仓库数量计算进度百分比
                // 剩余的70%进度分配给实际扫描阶段
                int percentComplete = startProgress + (int)Math.Round((processedRepos / (double)totalGitRepos) * 70);
                _outputManager.UpdateProgressBar(percentComplete);
                _outputManager.UpdateOutput($"扫描仓库 [{processedRepos}/{totalGitRepos}]: {repo.FullName}");

                ScanGitAuthors(repo, authors);
              }
            }
            catch (Exception ex)
            {
              _outputManager.UpdateOutput($"扫描路径时出错: {ex.Message}");
            }
          }

          // 扫描完成，设置进度为100%
          _outputManager.UpdateProgressBar(100);
        });
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

      // 移除重复的作者
      authors = authors.Distinct().OrderBy(a => a).ToList();
      _outputManager.UpdateOutput($"扫描完成，共找到 {authors.Count} 个不同的Git作者");

      return authors;
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

      try
      {
        // 显示进度条 - 改为任务执行前先更新UI
        await _outputManager.ShowProgressBarAsync();
        await _outputManager.UpdateProgressBarAsync(5); // 设置初始进度值
        await _outputManager.UpdateOutputAsync("正在初始化Git仓库扫描...");

        // 处理日期范围
        if (string.IsNullOrEmpty(until))
        {
          until = DateTime.Now.AddDays(1).ToString("yyyy-MM-dd"); // 设置到明天来包含今天的提交
        }

        // 增加进度到15%，表示日期处理阶段
        await _outputManager.UpdateProgressBarAsync(15);
        await Task.Delay(100); // 短暂延迟让UI有时间刷新

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
        await _outputManager.UpdateOutputAsync("正在搜索Git仓库，请稍后...");
        await Task.Delay(100); // 短暂延迟让UI有时间刷新

        // 查找所有Git仓库
        var gitRepos = new List<DirectoryInfo>();

        // 搜索仓库阶段，分配20%-40%的进度
        int pathIndex = 0;
        int totalPaths = paths.Count;

        foreach (var path in paths)
        {
          // 检查是否已手动停止
          if (!_isRunning)
          {
            await _outputManager.UpdateOutputAsync("扫描已手动停止");
            return commits;
          }

          pathIndex++;
          // 更新搜索阶段的进度
          int searchProgress = 20 + (int)((pathIndex / (double)totalPaths) * 20);
          await _outputManager.UpdateProgressBarAsync(searchProgress);
          await _outputManager.UpdateOutputAsync($"正在搜索路径 [{pathIndex}/{totalPaths}]: {path}");
          await Task.Delay(50); // 短暂延迟让UI有时间刷新

          try
          {
            var dir = new DirectoryInfo(path);
            if (!dir.Exists)
            {
              await _outputManager.UpdateOutputAsync($"警告: 路径不存在: {path}");
              continue;
            }

            if (verifyGitPaths)
            {
              // 如果启用了Git目录验证，就直接将输入的路径视为Git仓库
              if (IsGitRepository(path))
              {
                gitRepos.Add(dir);
                await _outputManager.UpdateOutputAsync($"添加Git仓库: {path}");
              }
              else
              {
                await _outputManager.UpdateOutputAsync($"警告: 路径不是Git仓库: {path}");
              }
            }
            else
            {
              // 没有启用验证，按原来的方式搜索所有子目录中的.git
              // 分批搜索，避免长时间阻塞UI
              List<DirectoryInfo> pathRepos = await Task.Run(() =>
              {
                return dir.GetDirectories(".git", SearchOption.AllDirectories)
                    .Select(gitDir => gitDir.Parent)
                    .Where(parent => parent != null)
                    .ToList();
              });

              await _outputManager.UpdateOutputAsync($"在路径 '{path}' 下找到 {pathRepos.Count} 个Git仓库");
              gitRepos.AddRange(pathRepos);
            }
          }
          catch (Exception ex)
          {
            await _outputManager.UpdateOutputAsync($"扫描路径时出错: {ex.Message}");
          }
        }

        _repoCount = gitRepos.Count;
        await _outputManager.UpdateOutputAsync($"总共找到 {_repoCount} 个Git仓库");

        // 如果没有找到仓库，设置为较高进度并退出
        if (_repoCount == 0)
        {
          await _outputManager.UpdateProgressBarAsync(90);
          await _outputManager.UpdateOutputAsync("未找到任何Git仓库，扫描结束");
          return commits;
        }

        // 设置为40%进度，表示开始处理仓库
        await _outputManager.UpdateProgressBarAsync(40);
        await Task.Delay(100); // 短暂延迟让UI有时间刷新

        // 处理每个Git仓库，分配40%-95%的进度
        _currentRepo = 0;
        DateTime? earliestRepoDate = null;

        // 分批处理每个仓库，确保UI更新及时
        for (int i = 0; i < gitRepos.Count; i++)
        {
          // 检查是否已手动停止
          if (!_isRunning)
          {
            await _outputManager.UpdateOutputAsync("处理仓库过程已手动停止");
            return commits;
          }

          var repo = gitRepos[i];
          _currentRepo++;

          // 更新处理仓库阶段的进度
          int percentComplete = 40 + (int)((_currentRepo / (double)_repoCount) * 55);
          await _outputManager.UpdateProgressBarAsync(percentComplete);
          await _outputManager.UpdateOutputAsync($"处理仓库 [{_currentRepo}/{_repoCount}]: {repo.FullName}");
          await Task.Delay(50); // 短暂延迟让UI有时间刷新

          try
          {
            // 在异步任务中处理单个仓库
            List<CommitInfo> repoCommits = await ProcessSingleRepositoryAsync(repo, since, until, author, authorFilter, gitFields, gitFormat);

            // 提取最早的提交日期
            DateTime? repoCreateDate = await GetRepositoryCreateDateAsync(repo);
            if (repoCreateDate.HasValue)
            {
              if (earliestRepoDate == null || repoCreateDate < earliestRepoDate)
              {
                earliestRepoDate = repoCreateDate;
              }
              await _outputManager.UpdateOutputAsync($"   仓库创建时间: {repoCreateDate.Value.ToString("yyyy-MM-dd HH:mm:ss")}");
            }

            // 添加此仓库的提交记录
            commits.AddRange(repoCommits);
          }
          catch (Exception ex)
          {
            await _outputManager.UpdateOutputAsync($"警告: 处理仓库 '{repo.FullName}' 时出错: {ex.Message}");
          }
        }

        // 替换进度条完成消息
        await _outputManager.UpdateOutputAsync("所有仓库处理完成 (100%)");

        // 输出扫描信息
        await _outputManager.UpdateOutputAsync("==== 扫描信息 ====");
        if (string.IsNullOrEmpty(since))
        {
          if (earliestRepoDate.HasValue)
          {
            string earliestDateStr = earliestRepoDate.Value.ToString("yyyy-MM-dd HH:mm:ss");
            await _outputManager.UpdateOutputAsync($"时间范围: 从仓库创建开始 ({earliestDateStr}) 至 {DateTime.Now.ToString("yyyy-MM-dd")}");
          }
          else
          {
            await _outputManager.UpdateOutputAsync($"时间范围: 从仓库创建开始 至 {DateTime.Now.ToString("yyyy-MM-dd")}");
          }
        }
        else
        {
          // 显示真实的日期范围，而不是调整后的日期
          string displayUntil = DateTime.Parse(until).AddDays(-1).ToString("yyyy-MM-dd");
          await _outputManager.UpdateOutputAsync($"时间范围: {since} 至 {displayUntil}");
        }

        if (string.IsNullOrEmpty(author))
        {
          await _outputManager.UpdateOutputAsync("提交作者: 所有作者");
        }
        else
        {
          await _outputManager.UpdateOutputAsync($"提交作者: {author}");
        }

        if (!string.IsNullOrEmpty(authorFilter))
        {
          await _outputManager.UpdateOutputAsync($"作者过滤: {authorFilter}");
        }

        await _outputManager.UpdateOutputAsync($"提取的Git字段: {string.Join(", ", gitFields)}");
        await _outputManager.UpdateOutputAsync("===================");

        // 输出结果数量
        await _outputManager.UpdateOutputAsync($"在指定时间范围内共找到 {commits.Count} 个提交");

        // 添加到最近使用的作者
        if (!string.IsNullOrEmpty(author))
        {
          _authorManager.AddToRecentAuthors(author);
        }

        // 设置进度条到完成状态
        await _outputManager.UpdateProgressBarAsync(100);
        await _outputManager.UpdateOutputAsync("Git提交记录收集完成");
      }
      catch (Exception ex)
      {
        await _outputManager.UpdateOutputAsync($"收集Git提交时出错: {ex.Message}");
      }
      finally
      {
        _isRunning = false;
        await _outputManager.HideProgressBarAsync();
      }

      return commits;
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
  }
}