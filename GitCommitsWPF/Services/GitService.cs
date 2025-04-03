using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Text;
using GitCommitsWPF.Models;

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
        // 查找所有包含.git目录的文件夹
        var gitDirs = new DirectoryInfo(path).GetDirectories(".git", SearchOption.AllDirectories);

        // 返回包含.git目录的父目录（即Git仓库根目录）
        return gitDirs.Select(gitDir => gitDir.Parent)
            .Where(parent => parent != null)
            .ToList();
      }
      catch (Exception)
      {
        // 处理异常情况，如权限不足等
        return new List<DirectoryInfo>();
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
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        // 恢复原目录
        Directory.SetCurrentDirectory(currentDirectory);

        if (!string.IsNullOrEmpty(output))
        {
          var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
          var repo = new DirectoryInfo(repoPath);

          foreach (var line in lines)
          {
            if (string.IsNullOrEmpty(line))
              continue;

            var parts = line.Split('|');
            if (parts.Length >= 4)
            {
              commits.Add(new CommitInfo
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
        }
      }
      catch (Exception)
      {
        // 处理异常
      }
      return commits;
    }
  }
}