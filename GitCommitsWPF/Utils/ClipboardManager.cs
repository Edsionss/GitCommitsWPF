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
    private readonly FormattingManager _formattingManager;
    private readonly StatisticsManager _statisticsManager;
    private ResultFormattingManager _resultFormattingManager;

    public ClipboardManager(
        DialogManager dialogManager,
        ResultFormattingManager resultFormattingManager = null,
        FormattingManager formattingManager = null,
        StatisticsManager statisticsManager = null)
    {
      _dialogManager = dialogManager ?? throw new ArgumentNullException(nameof(dialogManager));
      _resultFormattingManager = resultFormattingManager;
      _formattingManager = formattingManager;
      _statisticsManager = statisticsManager;
    }

    /// <summary>
    /// 设置ResultFormattingManager实例
    /// </summary>
    /// <param name="resultFormattingManager">ResultFormattingManager实例</param>
    public void SetResultFormattingManager(ResultFormattingManager resultFormattingManager)
    {
      _resultFormattingManager = resultFormattingManager;
    }

    // 将结果复制到剪贴板 - 使用完整格式化选项
    public void CopyResultToClipboard(
        List<CommitInfo> commits,
        bool includeStats,
        bool statsByAuthor,
        bool statsByRepo,
        bool statsByDate,
        string formatTemplate,
        List<string> selectedFields,
        bool showRepeatedRepoNames)
    {
      try
      {
        if (commits == null || commits.Count == 0)
        {
          _dialogManager.ShowCustomMessageBox("提示", "没有可复制的提交记录。", false);
          return;
        }

        // 准备结果文本
        string result;

        // 如果ResultFormattingManager可用，使用它生成格式化内容
        if (_resultFormattingManager != null)
        {
          // 使用内存TextBox模拟结果格式化
          var tempTextBox = new System.Windows.Controls.TextBox();

          // 使用ResultFormattingManager格式化提交记录，与导出文件完全相同的格式化逻辑
          _resultFormattingManager.UpdateFormattedResultTextBox(
              tempTextBox,
              commits,
              selectedFields,
              includeStats,
              statsByAuthor,
              statsByRepo,
              statsByDate,
              formatTemplate,
              showRepeatedRepoNames);

          // 获取格式化内容
          result = tempTextBox.Text;
        }
        else
        {
          // 回退到基本的格式化
          var resultBuilder = new StringBuilder();

          // 添加统计信息(如果启用)
          if (includeStats && _statisticsManager != null)
          {
            resultBuilder.AppendLine("\n======== 提交统计 ========\n");

            // 直接使用StatisticsManager生成统计信息
            _statisticsManager.GenerateStats(
                commits,
                resultBuilder,
                statsByAuthor,
                statsByRepo,
                statsByDate);

            resultBuilder.AppendLine("\n==========================\n");
          }

          // 添加提交记录
          if (!string.IsNullOrEmpty(formatTemplate) && _formattingManager != null)
          {
            // 使用FormattingManager的格式化方法
            string formattedOutput = _formattingManager.FormatCommits(
                commits,
                formatTemplate,
                showRepeatedRepoNames);

            resultBuilder.Append(formattedOutput);
          }
          else
          {
            // 使用最基本的格式化
            foreach (var commit in commits)
            {
              resultBuilder.AppendLine($"{commit.Repository} : {commit.Message}");
            }
          }

          result = resultBuilder.ToString();
        }

        // 复制到剪贴板
        Clipboard.SetText(result);

        // 显示复制成功消息
        _dialogManager.ShowCustomMessageBox("复制成功", $"已复制 {commits.Count} 条提交记录到剪贴板。", true);
      }
      catch (Exception ex)
      {
        _dialogManager.ShowCustomMessageBox("复制错误", $"复制到剪贴板时发生错误：{ex.Message}", false);
      }
    }

    // 原来的方法保留用于兼容性
    public void CopyResultToClipboard(
        List<CommitInfo> commits,
        string statsOutput,
        bool includeStats,
        string formatTemplate,
        List<string> selectedFields)
    {
      // 调用新的实现
      CopyResultToClipboard(
          commits,
          includeStats,
          includeStats && !string.IsNullOrEmpty(statsOutput) && statsOutput.Contains("按作者统计"),
          includeStats && !string.IsNullOrEmpty(statsOutput) && statsOutput.Contains("按仓库统计"),
          includeStats && !string.IsNullOrEmpty(statsOutput) && statsOutput.Contains("按日期统计"),
          formatTemplate,
          selectedFields,
          true);
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