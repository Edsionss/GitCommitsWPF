using System;
using System.IO;
using System.Text;
using System.Windows;
using Microsoft.Win32;

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
    /// 复制日志到剪贴板
    /// </summary>
    /// <param name="logContent">日志内容</param>
    public void CopyLog(string logContent)
    {
      try
      {
        if (string.IsNullOrEmpty(logContent))
        {
          _dialogManager.ShowCustomMessageBox("提示", "日志内容为空，无法复制。", false);
          return;
        }

        // 复制到剪贴板
        Clipboard.SetText(logContent);
        _dialogManager.ShowCustomMessageBox("成功", "日志内容已复制到剪贴板。", false);
      }
      catch (Exception ex)
      {
        _dialogManager.ShowCustomMessageBox("错误", $"复制日志时出错: {ex.Message}", false);
      }
    }

    /// <summary>
    /// 保存日志到文件
    /// </summary>
    /// <param name="logContent">日志内容</param>
    public void SaveLog(string logContent)
    {
      try
      {
        if (string.IsNullOrEmpty(logContent))
        {
          _dialogManager.ShowCustomMessageBox("提示", "日志内容为空，无法保存。", false);
          return;
        }

        // 创建默认文件名：Git日志_年月日时分秒.log
        string defaultFileName = $"Git日志_{DateTime.Now.ToString("yyyyMMdd_HHmmss")}.log";

        // 显示保存文件对话框
        var dialog = new SaveFileDialog
        {
          Title = "保存日志",
          Filter = "日志文件|*.log|文本文件|*.txt|所有文件|*.*",
          DefaultExt = ".log",
          FileName = defaultFileName
        };

        if (dialog.ShowDialog() == true)
        {
          // 保存日志内容到文件
          File.WriteAllText(dialog.FileName, logContent, Encoding.UTF8);
          _dialogManager.ShowCustomMessageBox("成功", $"日志已保存到: {dialog.FileName}", true, dialog.FileName);
        }
      }
      catch (Exception ex)
      {
        _dialogManager.ShowCustomMessageBox("错误", $"保存日志时出错: {ex.Message}", false);
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