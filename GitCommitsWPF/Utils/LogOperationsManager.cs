using System;
using System.IO;
using System.Text;
using System.Windows;
using Microsoft.Win32;
using System.Windows.Documents;

namespace GitCommitsWPF.Utils
{
  /// <summary>
  /// 处理日志操作（复制、保存、清除）的管理器类
  /// </summary>
  public class LogOperationsManager
  {
    private readonly DialogManager _dialogManager;
    private readonly OutputManager _outputManager;

    /// <summary>
    /// 初始化日志操作管理器
    /// </summary>
    /// <param name="dialogManager">对话框管理器</param>
    /// <param name="outputManager">输出管理器</param>
    public LogOperationsManager(DialogManager dialogManager, OutputManager outputManager)
    {
      _dialogManager = dialogManager ?? throw new ArgumentNullException(nameof(dialogManager));
      _outputManager = outputManager ?? throw new ArgumentNullException(nameof(outputManager));
    }

    /// <summary>
    /// 复制日志
    /// </summary>
    /// <param name="logContent">日志内容</param>
    public void CopyLog(object logControl)
    {
      try
      {
        string logContent = string.Empty;

        // 支持RichTextBox和TextBox两种控件
        if (logControl is System.Windows.Controls.RichTextBox richTextBox)
        {
          TextRange textRange = new TextRange(richTextBox.Document.ContentStart, richTextBox.Document.ContentEnd);
          logContent = textRange.Text;
        }
        else if (logControl is string textContent)
        {
          logContent = textContent;
        }
        else if (logControl is System.Windows.Controls.TextBox textBox)
        {
          logContent = textBox.Text;
        }

        if (string.IsNullOrEmpty(logContent))
        {
          _dialogManager.ShowCustomMessageBox("提示", "日志内容为空，无法复制。", false);
          return;
        }

        // 复制到剪贴板
        Clipboard.SetText(logContent);
        _dialogManager.ShowCustomMessageBox("成功", "日志已复制到剪贴板。", true);
      }
      catch (Exception ex)
      {
        _dialogManager.ShowCustomMessageBox("错误", string.Format("复制日志时出错: {0}", ex.Message), false);
      }
    }

    /// <summary>
    /// 保存日志
    /// </summary>
    /// <param name="logContent">日志内容</param>
    public void SaveLog(object logControl)
    {
      try
      {
        string logContent = string.Empty;

        // 支持RichTextBox和TextBox两种控件
        if (logControl is System.Windows.Controls.RichTextBox richTextBox)
        {
          TextRange textRange = new TextRange(richTextBox.Document.ContentStart, richTextBox.Document.ContentEnd);
          logContent = textRange.Text;
        }
        else if (logControl is string textContent)
        {
          logContent = textContent;
        }
        else if (logControl is System.Windows.Controls.TextBox textBox)
        {
          logContent = textBox.Text;
        }

        if (string.IsNullOrEmpty(logContent))
        {
          _dialogManager.ShowCustomMessageBox("提示", "日志内容为空，无法保存。", false);
          return;
        }

        // 使用SaveFileDialog保存日志
        var saveFileDialog = new SaveFileDialog
        {
          Filter = "文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*",
          Title = "保存日志文件",
          FileName = string.Format("Git查询日志_{0}.txt", DateTime.Now.ToString("yyyyMMdd_HHmmss"))
        };

        if (saveFileDialog.ShowDialog() == true)
        {
          File.WriteAllText(saveFileDialog.FileName, logContent, Encoding.UTF8);
          _dialogManager.ShowCustomMessageBox("成功", string.Format("日志已保存到文件: {0}", saveFileDialog.FileName), true);
        }
      }
      catch (Exception ex)
      {
        _dialogManager.ShowCustomMessageBox("错误", string.Format("保存日志时出错: {0}", ex.Message), false);
      }
    }

    /// <summary>
    /// 清空日志内容
    /// </summary>
    /// <returns>是否确认清空</returns>
    public bool CleanLog()
    {
      if (_dialogManager.ShowCustomConfirmDialog("确认", "确定要清除所有日志吗？"))
      {
        _outputManager.ClearOutput();
        _outputManager.UpdateOutput("日志已清除");
        return true;
      }
      return false;
    }
  }
}