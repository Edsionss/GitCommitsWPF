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
    /// 提供Git相关操作的服务类
    /// </summary>
    public class GitService
    {
        /// <summary>
        /// 检查指定路径是否为Git仓库
        /// </summary>
        /// <param name="path">要检查的路径</param>
        /// <returns>如果是Git仓库则返回true，否则返回false</returns>
        public static bool IsGitRepository(string path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                return false;

            // 检查.git目录或文件是否存在
            string gitDir = Path.Combine(path, ".git");
            return Directory.Exists(gitDir) || File.Exists(gitDir);
        }

        /// <summary>
        /// 查找指定目录下的所有Git仓库
        /// </summary>
        /// <param name="path">要搜索的目录</param>
        /// <returns>包含找到的Git仓库的目录信息列表</returns>
        public static List<DirectoryInfo> FindGitRepositories(string path)
        {
            var result = new List<DirectoryInfo>();
            try
            {
                var dir = new DirectoryInfo(path);
                if (!dir.Exists)
                    return result;

                // 搜索所有.git目录，获取其父目录
                var repos = dir.GetDirectories(".git", SearchOption.AllDirectories)
                    .Select(gitDir => gitDir.Parent)
                    .Where(parent => parent != null)
                    .ToList();

                result.AddRange(repos);
            }
            catch (Exception)
            {
                // 处理权限不足等问题，直接返回空列表
            }
            return result;
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