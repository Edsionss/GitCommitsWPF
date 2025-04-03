using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Text;
using GitCommitsWPF.Models;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace GitCommitsWPF.Services
{
  /// <summary>
  /// 提供Git相关基础操作的服务类
  /// </summary>
  public static class GitService
  {
    /// <summary>
    /// 检查路径是否为Git仓库
    /// </summary>
    /// <param name="path">路径</param>
    /// <returns>是否为Git仓库</returns>
    public static bool IsGitRepository(string path)
    {
      if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
        return false;

      // 检查是否存在.git目录
      string gitDir = Path.Combine(path, ".git");
      if (Directory.Exists(gitDir))
        return true;

      return false;
    }

    /// <summary>
    /// 查找路径下的所有Git仓库
    /// </summary>
    /// <param name="path">搜索路径</param>
    /// <returns>Git仓库目录列表</returns>
    public static List<DirectoryInfo> FindGitRepositories(string path)
    {
      if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
        return new List<DirectoryInfo>();

      try
      {
        // 使用ConcurrentBag存储结果，以支持并行处理
        var gitReposBag = new ConcurrentBag<DirectoryInfo>();

        // 首先检查当前目录是否是Git仓库
        if (IsGitRepository(path))
        {
          gitReposBag.Add(new DirectoryInfo(path));
          return gitReposBag.ToList();
        }

        // 获取顶级目录
        DirectoryInfo[] topDirs;
        try
        {
          topDirs = new DirectoryInfo(path).GetDirectories();
        }
        catch
        {
          return new List<DirectoryInfo>();
        }

        // 并行处理第一级子目录
        int maxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1);
        Parallel.ForEach(topDirs, new ParallelOptions { MaxDegreeOfParallelism = maxDegreeOfParallelism }, dir =>
        {
          try
          {
            // 检查当前目录是否包含.git
            string gitDir = Path.Combine(dir.FullName, ".git");
            if (Directory.Exists(gitDir))
            {
              gitReposBag.Add(dir);
            }
            else
            {
              // 递归搜索子目录 - 使用深度优先搜索以获得更好的性能
              SearchGitRepositories(dir, gitReposBag);
            }
          }
          catch
          {
            // 忽略单个目录的错误
          }
        });

        // 转换为列表并返回
        return gitReposBag.ToList();
      }
      catch (Exception)
      {
        // 处理异常情况，如权限不足等
        return new List<DirectoryInfo>();
      }
    }

    /// <summary>
    /// 递归搜索Git仓库
    /// </summary>
    /// <param name="dir">要搜索的目录</param>
    /// <param name="reposBag">结果集合</param>
    private static void SearchGitRepositories(DirectoryInfo dir, ConcurrentBag<DirectoryInfo> reposBag)
    {
      try
      {
        // 获取子目录
        DirectoryInfo[] subdirs;
        try
        {
          subdirs = dir.GetDirectories();
        }
        catch
        {
          return; // 忽略权限错误
        }

        // 检查是否有.git目录
        foreach (var subdir in subdirs)
        {
          try
          {
            string gitDir = Path.Combine(subdir.FullName, ".git");
            if (Directory.Exists(gitDir))
            {
              reposBag.Add(subdir);
            }
            else
            {
              // 递归搜索
              SearchGitRepositories(subdir, reposBag);
            }
          }
          catch
          {
            // 忽略单个目录的错误
          }
        }
      }
      catch
      {
        // 忽略错误并继续
      }
    }

    /// <summary>
    /// 检查是否已安装Git
    /// </summary>
    /// <returns>是否已安装Git</returns>
    public static bool IsGitInstalled()
    {
      try
      {
        using (var process = new System.Diagnostics.Process())
        {
          process.StartInfo.FileName = "git";
          process.StartInfo.Arguments = "--version";
          process.StartInfo.UseShellExecute = false;
          process.StartInfo.CreateNoWindow = true;
          process.StartInfo.RedirectStandardOutput = true;
          process.StartInfo.RedirectStandardError = true;

          process.Start();
          process.WaitForExit();

          return process.ExitCode == 0;
        }
      }
      catch
      {
        return false;
      }
    }

    /// <summary>
    /// 获取Git仓库的首次提交日期
    /// </summary>
    /// <param name="repoPath">Git仓库路径</param>
    /// <returns>首次提交的日期字符串，如果获取失败则返回null</returns>
    public static string GetFirstCommitDate(string repoPath)
    {
      try
      {
        // 保存当前目录
        string currentDirectory = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(repoPath);

        // 执行git命令获取第一次提交的时间
        var process = new Process
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

        process.Start();
        string firstCommitDateStr = process.StandardOutput.ReadLine();
        process.WaitForExit();

        // 恢复原目录
        Directory.SetCurrentDirectory(currentDirectory);

        return firstCommitDateStr;
      }
      catch (Exception)
      {
        return null;
      }
    }

    /// <summary>
    /// 获取Git仓库的提交记录
    /// </summary>
    /// <param name="repoPath">Git仓库路径</param>
    /// <param name="since">开始日期（可选）</param>
    /// <param name="until">结束日期（可选）</param>
    /// <param name="author">作者过滤（可选）</param>
    /// <returns>CommitInfo对象列表</returns>
    public static List<CommitInfo> GetCommits(string repoPath, string since = "", string until = "", string author = "")
    {
      var commits = new List<CommitInfo>();
      try
      {
        // 保存当前目录
        string currentDirectory = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(repoPath);

        // 构建Git命令参数
        var arguments = "log";

        if (!string.IsNullOrEmpty(since))
        {
          arguments += " --since=\"" + since + "\"";
        }

        if (!string.IsNullOrEmpty(until))
        {
          arguments += " --until=\"" + until + "\"";
        }

        arguments += " --pretty=format:\"%H|%an|%ad|%s\"";
        arguments += " --date=format:\"%Y-%m-%d %H:%M:%S\"";

        if (!string.IsNullOrEmpty(author))
        {
          arguments += " --author=\"" + author + "\"";
        }

        // 添加性能优化选项
        arguments += " --all"; // 包含所有引用
        arguments += " -n1000"; // 限制结果数量

        // 执行Git命令
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
        var repo = new DirectoryInfo(repoPath);

        // 使用ConcurrentBag存储结果
        var commitsBag = new ConcurrentBag<CommitInfo>();

        // 逐行读取并处理输出
        List<string> lines = new List<string>();
        while (!process.StandardOutput.EndOfStream)
        {
          string line = process.StandardOutput.ReadLine();
          if (!string.IsNullOrEmpty(line))
          {
            lines.Add(line);
          }
        }

        process.WaitForExit();

        // 恢复原目录
        Directory.SetCurrentDirectory(currentDirectory);

        // 并行处理结果
        Parallel.ForEach(lines, line =>
        {
          try
          {
            var parts = line.Split('|');
            if (parts.Length >= 4)
            {
              commitsBag.Add(new CommitInfo
              {
                Repository = repo.Name,
                RepoPath = repo.FullName,
                RepoFolder = Path.GetFileName(repo.FullName),
                CommitId = parts[0],
                Author = parts[1],
                Date = parts[2],
                Message = parts[3]
              });
            }
          }
          catch
          {
            // 忽略单行解析错误
          }
        });

        // 转换为列表
        commits = commitsBag.ToList();
      }
      catch (Exception)
      {
        // 处理异常
      }

      return commits;
    }
  }
}