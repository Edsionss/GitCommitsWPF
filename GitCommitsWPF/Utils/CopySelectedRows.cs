using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using GitCommitsWPF.Models;

namespace GitCommitsWPF.Utils
{
  public class CopySelectedRows
  {
    private DialogManager _dialogManager;
    private ResultFormattingManager _resultFormattingManager;
    private FormattingManager _formattingManager;

    public CopySelectedRows(DialogManager dialogManager, ResultFormattingManager resultFormattingManager, FormattingManager formattingManager)
    {
      _dialogManager = dialogManager;
      _resultFormattingManager = resultFormattingManager;
      _formattingManager = formattingManager;
    }

    // 复制选定的行到剪贴板
    public void CopyRowsToClipboard(List<CommitInfo> selectedCommits, string format)
    {
      try
      {
        if (selectedCommits == null || selectedCommits.Count == 0)
        {
          _dialogManager.ShowCustomMessageBox("提示", "没有选择任何行。", false);
          return;
        }

        // 使用与CopyResultToClipboard相同的格式化逻辑
        // 选中所有字段
        List<string> allFields = new List<string>
                {
                    "Repository", "RepoPath", "RepoFolder",
                    "CommitId", "Author", "Date", "Message"
                };

        // 准备结果文本
        string result;

        // 如果ResultFormattingManager可用，使用它生成格式化内容
        if (_resultFormattingManager != null)
        {
          // 使用内存TextBox模拟结果格式化
          var tempTextBox = new System.Windows.Controls.TextBox();

          // 对于选中的行，我们不显示统计信息
          _resultFormattingManager.UpdateFormattedResultTextBox(
              tempTextBox,
              selectedCommits,
              allFields,
              false, // 不包含统计
              false,
              false,
              false,
              format,
              true); // 总是显示重复的仓库名称

          // 获取格式化内容
          result = tempTextBox.Text;
        }
        else if (_formattingManager != null && !string.IsNullOrEmpty(format))
        {
          // 使用FormattingManager进行格式化
          result = _formattingManager.FormatCommits(
              selectedCommits,
              format,
              true); // 总是显示重复的仓库名称
        }
        else
        {
          // 回退到最基本的格式化
          var resultBuilder = new StringBuilder();
          foreach (var commit in selectedCommits)
          {
            // 默认格式: 仓库 [作者] 提交信息
            resultBuilder.AppendLine($"{commit.Repository} [{commit.Author}] {commit.Message}");
          }
          result = resultBuilder.ToString();
        }

        // 复制到剪贴板
        Clipboard.SetText(result);
        _dialogManager.ShowCustomMessageBox("复制成功", $"已复制 {selectedCommits.Count} 条提交记录到剪贴板。", true);
      }
      catch (Exception ex)
      {
        _dialogManager.ShowCustomMessageBox("复制错误", $"复制到剪贴板时发生错误：{ex.Message}", false);
      }
    }
  }
}