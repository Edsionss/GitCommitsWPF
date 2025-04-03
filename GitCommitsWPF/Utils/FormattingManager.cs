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
      var displayedRepos = new Dictionary<string, bool>();

      foreach (var commit in commits)
      {
        string line = format;

        // 获取当前提交的仓库标识符
        string repoKey = !string.IsNullOrEmpty(commit.RepoFolder) ? commit.RepoFolder : commit.Repository;

        // 替换所有占位符
        line = line.Replace("{Repository}",
            (!showRepeatedRepoNames && displayedRepos.ContainsKey(repoKey)) ?
            new string(' ', repoKey.Length) : commit.Repository);

        line = line.Replace("{RepoPath}", commit.RepoPath);

        line = line.Replace("{RepoFolder}",
            (!showRepeatedRepoNames && displayedRepos.ContainsKey(repoKey)) ?
            new string(' ', repoKey.Length) : commit.RepoFolder);

        line = line.Replace("{CommitId}", commit.CommitId ?? "");
        line = line.Replace("{Author}", commit.Author ?? "");
        line = line.Replace("{Date}", commit.Date ?? "");
        line = line.Replace("{Message}", commit.Message ?? "");

        formattedOutput.AppendLine(line);

        // 标记此仓库已显示
        if (!displayedRepos.ContainsKey(repoKey))
        {
          displayedRepos[repoKey] = true;
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
    /// 获取选中字段的列表
    /// </summary>
    public List<string> GetSelectedFields(
        bool includeRepository,
        bool includeRepoPath,
        bool includeRepoFolder,
        bool includeCommitId,
        bool includeAuthor,
        bool includeDate,
        bool includeMessage)
    {
      List<string> selectedFields = new List<string>();

      if (includeRepository) selectedFields.Add("Repository");
      if (includeRepoPath) selectedFields.Add("RepoPath");
      if (includeRepoFolder) selectedFields.Add("RepoFolder");
      if (includeCommitId) selectedFields.Add("CommitId");
      if (includeAuthor) selectedFields.Add("Author");
      if (includeDate) selectedFields.Add("Date");
      if (includeMessage) selectedFields.Add("Message");

      return selectedFields;
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