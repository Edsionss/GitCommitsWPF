using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using Newtonsoft.Json;
using GitCommitsWPF.Models;

namespace GitCommitsWPF.Utils
{
  public class ClipboardManager
  {
    private readonly DialogManager _dialogManager;

    public ClipboardManager(DialogManager dialogManager)
    {
      _dialogManager = dialogManager ?? throw new ArgumentNullException(nameof(dialogManager));
    }

    // 将结果复制到剪贴板
    public void CopyResultToClipboard(
        List<CommitInfo> commits,
        string statsOutput,
        bool includeStats,
        string formatTemplate,
        List<string> selectedFields)
    {
      try
      {
        if (commits == null || commits.Count == 0)
        {
          _dialogManager.ShowCustomMessageBox("提示", "没有可复制的提交记录。", false);
          return;
        }

        // 准备结果文本
        var resultBuilder = new StringBuilder();

        // 添加统计信息(如果启用)
        if (includeStats && !string.IsNullOrEmpty(statsOutput))
        {
          resultBuilder.AppendLine("======== 提交统计 ========");
          resultBuilder.AppendLine();
          resultBuilder.AppendLine(statsOutput);
          resultBuilder.AppendLine("==========================");
          resultBuilder.AppendLine();
        }

        // 添加提交记录
        if (!string.IsNullOrEmpty(formatTemplate))
        {
          // 使用自定义格式模板
          foreach (var commit in commits)
          {
            string formattedLine = FormatCommitLine(commit, formatTemplate, selectedFields);
            resultBuilder.AppendLine(formattedLine);
          }
        }
        else
        {
          // 使用默认JSON格式
          var filteredCommits = commits
              .Select(commit => FilterCommitFields(commit, selectedFields))
              .ToList();

          resultBuilder.AppendLine(JsonConvert.SerializeObject(filteredCommits, Formatting.Indented));
        }

        // 复制到剪贴板
        string result = resultBuilder.ToString();
        Clipboard.SetText(result);

        // 显示复制成功消息
        _dialogManager.ShowCustomMessageBox("复制成功", $"已复制 {commits.Count} 条提交记录到剪贴板。", true);
      }
      catch (Exception ex)
      {
        _dialogManager.ShowCustomMessageBox("复制错误", $"复制到剪贴板时发生错误：{ex.Message}", false);
      }
    }

    // 根据提供的格式化字符串格式化提交记录
    private string FormatCommitLine(CommitInfo commit, string format, List<string> selectedFields)
    {
      string result = format;

      // 只处理选中的字段
      if (selectedFields.Contains("Repository"))
        result = result.Replace("{Repository}", commit.Repository ?? "");

      if (selectedFields.Contains("RepoPath"))
        result = result.Replace("{RepoPath}", commit.RepoPath ?? "");

      if (selectedFields.Contains("RepoFolder"))
        result = result.Replace("{RepoFolder}", commit.RepoFolder ?? "");

      if (selectedFields.Contains("CommitId"))
        result = result.Replace("{CommitId}", commit.CommitId ?? "");

      if (selectedFields.Contains("Author"))
        result = result.Replace("{Author}", commit.Author ?? "");

      if (selectedFields.Contains("Date"))
        result = result.Replace("{Date}", commit.Date ?? "");

      if (selectedFields.Contains("Message"))
        result = result.Replace("{Message}", commit.Message ?? "");

      return result;
    }

    // 根据选择的字段筛选提交记录
    private CommitInfo FilterCommitFields(CommitInfo commit, List<string> selectedFields)
    {
      var filteredCommit = new CommitInfo();

      if (selectedFields.Contains("Repository"))
        filteredCommit.Repository = commit.Repository;

      if (selectedFields.Contains("RepoPath"))
        filteredCommit.RepoPath = commit.RepoPath;

      if (selectedFields.Contains("RepoFolder"))
        filteredCommit.RepoFolder = commit.RepoFolder;

      if (selectedFields.Contains("CommitId"))
        filteredCommit.CommitId = commit.CommitId;

      if (selectedFields.Contains("Author"))
        filteredCommit.Author = commit.Author;

      if (selectedFields.Contains("Date"))
        filteredCommit.Date = commit.Date;

      if (selectedFields.Contains("Message"))
        filteredCommit.Message = commit.Message;

      return filteredCommit;
    }

    // 复制选定的行到剪贴板
    public void CopySelectedRows(List<CommitInfo> selectedCommits, string format)
    {
      try
      {
        if (selectedCommits == null || selectedCommits.Count == 0)
        {
          _dialogManager.ShowCustomMessageBox("提示", "没有选择任何行。", false);
          return;
        }

        var resultBuilder = new StringBuilder();

        // 使用所有字段进行格式化
        List<string> allFields = new List<string>
                {
                    "Repository", "RepoPath", "RepoFolder",
                    "CommitId", "Author", "Date", "Message"
                };

        foreach (var commit in selectedCommits)
        {
          if (!string.IsNullOrEmpty(format))
          {
            string formattedLine = FormatCommitLine(commit, format, allFields);
            resultBuilder.AppendLine(formattedLine);
          }
          else
          {
            // 默认格式: 仓库 [作者] 提交信息
            resultBuilder.AppendLine($"{commit.Repository} [{commit.Author}] {commit.Message}");
          }
        }

        Clipboard.SetText(resultBuilder.ToString());
        _dialogManager.ShowCustomMessageBox("复制成功", $"已复制 {selectedCommits.Count} 条提交记录到剪贴板。", true);
      }
      catch (Exception ex)
      {
        _dialogManager.ShowCustomMessageBox("复制错误", $"复制到剪贴板时发生错误：{ex.Message}", false);
      }
    }

    // 显示复制成功的消息框
    public void ShowCopySuccessMessageBox(string title, string message)
    {
      _dialogManager.ShowCopySuccessMessageBox(title, message);
    }
  }
}