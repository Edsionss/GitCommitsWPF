using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using GitCommitsWPF.Models;

namespace GitCommitsWPF.Utils
{
  /// <summary>
  /// 管理Git提交记录格式化显示相关功能的类
  /// </summary>
  public class FormattingManager
  {
    /// <summary>
    /// 使用指定格式格式化提交记录
    /// </summary>
    /// <param name="commits">提交记录列表</param>
    /// <param name="format">格式模板，例如 "{Repository} : {Message}"</param>
    /// <param name="showRepeatedRepoNames">是否显示重复的仓库名称</param>
    /// <returns>格式化后的结果字符串</returns>
    public string FormatCommits(List<CommitInfo> commits, string format, bool showRepeatedRepoNames)
    {
      if (commits == null || commits.Count == 0)
      {
        return string.Empty;
      }

      if (string.IsNullOrEmpty(format))
      {
        return string.Empty;
      }

      var formattedOutput = new StringBuilder();

      // 确保每个提交记录都有正确的仓库信息
      foreach (var commit in commits)
      {
        if (string.IsNullOrEmpty(commit.Repository) && !string.IsNullOrEmpty(commit.RepoPath))
        {
          commit.Repository = System.IO.Path.GetFileName(commit.RepoPath);
        }

        if (string.IsNullOrEmpty(commit.RepoFolder) && !string.IsNullOrEmpty(commit.RepoPath))
        {
          commit.RepoFolder = System.IO.Path.GetFileName(commit.RepoPath);
        }
      }

      // 优化：按仓库路径分组，确保同一仓库的提交保持在一起
      var commitsByRepo = commits
          .GroupBy(c => c.RepoPath ?? "Unknown")
          .ToList();

      // 遍历每个仓库组
      foreach (var repoGroup in commitsByRepo)
      {
        string repoPath = repoGroup.Key;
        var repoCommits = repoGroup.ToList();

        // 每个仓库组单独维护显示状态
        bool isFirstCommitInRepo = true;

        // 处理该仓库的所有提交
        foreach (var commit in repoCommits)
        {
          string line = format;

          // 替换所有占位符，确保仓库名称正确显示
          if (showRepeatedRepoNames || isFirstCommitInRepo)
          {
            // 总是显示第一条记录的仓库名
            line = line.Replace("{Repository}", commit.Repository ?? "");
            line = line.Replace("{RepoFolder}", commit.RepoFolder ?? "");
          }
          else
          {
            // 对后续记录，如果不显示重复名，则替换为空白
            string repoSpaces = new string(' ', commit.Repository?.Length ?? 0);
            string folderSpaces = new string(' ', commit.RepoFolder?.Length ?? 0);

            line = line.Replace("{Repository}", repoSpaces);
            line = line.Replace("{RepoFolder}", folderSpaces);
          }

          line = line.Replace("{RepoPath}", commit.RepoPath ?? "");
          line = line.Replace("{CommitId}", commit.CommitId ?? "");
          line = line.Replace("{Author}", commit.Author ?? "");
          line = line.Replace("{Date}", commit.Date ?? "");
          line = line.Replace("{Message}", commit.Message ?? "");

          formattedOutput.AppendLine(line);

          // 标记此仓库已显示第一条记录
          isFirstCommitInRepo = false;
        }
      }

      return formattedOutput.ToString();
    }

    /// <summary>
    /// 创建过滤后的CommitInfo对象，只包含指定字段
    /// </summary>
    /// <param name="commit">原始提交信息</param>
    /// <param name="selectedFields">选择的字段列表</param>
    /// <returns>过滤后的提交信息</returns>
    public CommitInfo CreateFilteredCommit(CommitInfo commit, List<string> selectedFields)
    {
      var filteredCommit = new CommitInfo();

      if (selectedFields.Contains("Repository")) filteredCommit.Repository = commit.Repository;
      if (selectedFields.Contains("RepoPath")) filteredCommit.RepoPath = commit.RepoPath;
      if (selectedFields.Contains("RepoFolder")) filteredCommit.RepoFolder = commit.RepoFolder;
      if (selectedFields.Contains("CommitId")) filteredCommit.CommitId = commit.CommitId;
      if (selectedFields.Contains("Author")) filteredCommit.Author = commit.Author;
      if (selectedFields.Contains("Date")) filteredCommit.Date = commit.Date;
      if (selectedFields.Contains("Message")) filteredCommit.Message = commit.Message;

      return filteredCommit;
    }

    /// <summary>
    /// 组合统计数据和格式化结果
    /// </summary>
    /// <param name="statsOutput">统计输出</param>
    /// <param name="formattedOutput">格式化输出</param>
    /// <param name="enableStats">是否启用统计</param>
    /// <returns>合并后的输出字符串</returns>
    public string CombineOutput(string statsOutput, string formattedOutput, bool enableStats)
    {
      if (!enableStats)
      {
        return formattedOutput;
      }

      return statsOutput + Environment.NewLine + formattedOutput;
    }
  }
}