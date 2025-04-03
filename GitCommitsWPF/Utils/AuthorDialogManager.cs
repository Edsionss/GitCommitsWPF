using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using GitCommitsWPF.Models;
using GitCommitsWPF.Views;

namespace GitCommitsWPF.Utils
{
  /// <summary>
  /// 作者对话框管理器，用于处理作者选择相关的对话框和功能
  /// </summary>
  public class AuthorDialogManager
  {
    private readonly Window _owner;
    private readonly AuthorManager _authorManager;
    private readonly DialogManager _dialogManager;
    private readonly GitOperationsManager _gitOperationsManager;

    /// <summary>
    /// 初始化作者对话框管理器
    /// </summary>
    /// <param name="owner">拥有者窗口</param>
    /// <param name="authorManager">作者管理器</param>
    /// <param name="dialogManager">对话框管理器</param>
    /// <param name="gitOperationsManager">Git操作管理器</param>
    public AuthorDialogManager(Window owner, AuthorManager authorManager, DialogManager dialogManager, GitOperationsManager gitOperationsManager)
    {
      _owner = owner;
      _authorManager = authorManager;
      _dialogManager = dialogManager;
      _gitOperationsManager = gitOperationsManager;
    }

    /// <summary>
    /// 显示作者选择对话框
    /// </summary>
    /// <param name="authors">作者列表</param>
    /// <returns>选择的作者</returns>
    public string ShowAuthorSelectionDialog(List<string> authors)
    {
      // 创建并显示作者选择窗口
      var authorSelectionWindow = new AuthorSelectionWindow(_authorManager, _dialogManager)
      {
        Owner = _owner
      };

      // 初始化扫描作者列表，并自动切换到扫描作者标签页
      if (authorSelectionWindow.InitializeFromScanResults(authors))
      {
        if (authorSelectionWindow.ShowDialog() == true && !string.IsNullOrEmpty(authorSelectionWindow.SelectedAuthor))
        {
          return authorSelectionWindow.SelectedAuthor;
        }
      }

      return null;
    }

    /// <summary>
    /// 显示作者选择对话框（从最近作者中选择）
    /// </summary>
    /// <param name="commits">提交信息列表，用于初始化作者列表</param>
    /// <returns>选择的作者</returns>
    public string ShowRecentAuthorSelectionDialog(List<CommitInfo> commits = null)
    {
      // 创建并显示作者选择窗口
      var authorSelectionWindow = new AuthorSelectionWindow(_authorManager, _dialogManager)
      {
        Owner = _owner
      };

      // 从已有提交中初始化作者列表（如果最近作者列表为空）
      if (authorSelectionWindow.InitializeFromCommits(commits))
      {
        if (authorSelectionWindow.ShowDialog() == true && !string.IsNullOrEmpty(authorSelectionWindow.SelectedAuthor))
        {
          return authorSelectionWindow.SelectedAuthor;
        }
      }

      return null;
    }

    /// <summary>
    /// 异步扫描并显示Git作者选择对话框
    /// </summary>
    /// <param name="paths">要扫描的路径列表</param>
    /// <returns>选择的作者</returns>
    public async System.Threading.Tasks.Task<string> ScanAndShowAuthorSelectionDialogAsync(List<string> paths)
    {
      // 使用GitOperationsManager异步扫描Git作者
      List<string> authors = await _gitOperationsManager.ScanGitAuthorsAsync(paths);

      // 检查结果
      if (authors.Count > 0)
      {
        // 显示作者选择对话框
        return ShowAuthorSelectionDialog(authors);
      }
      else
      {
        _dialogManager.ShowCustomMessageBox("提示", "未找到任何Git作者信息", false);
        return null;
      }
    }
  }
}