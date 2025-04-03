using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using GitCommitsWPF.Models;

namespace GitCommitsWPF.Utils
{
  public class DataGridManager
  {
    private DataGrid _commitsDataGrid;
    private readonly DialogManager _dialogManager;
    private Func<string> _getFormatFunc;

    public DataGridManager(DialogManager dialogManager)
    {
      _dialogManager = dialogManager ?? throw new ArgumentNullException(nameof(dialogManager));
    }

    public void Initialize(DataGrid dataGrid, Action<string, string, bool, Action> showMessageBoxAction, Func<string> getFormatFunc)
    {
      _commitsDataGrid = dataGrid ?? throw new ArgumentNullException(nameof(dataGrid));
      _getFormatFunc = getFormatFunc ?? throw new ArgumentNullException(nameof(getFormatFunc));
    }

    public void UpdateDataSource(List<CommitInfo> commits)
    {
      if (_commitsDataGrid != null)
      {
        _commitsDataGrid.ItemsSource = commits;
      }
    }

    public void CopySelectedRows()
    {
      // 此方法将被弃用，改用ClipboardManager的CopySelectedRows方法
      // 为保持向后兼容，此方法保留
    }

    public void ExportSelectedToClipboard()
    {
      // 此方法应改为使用ClipboardManager，暂时保留以便后续重构
    }

    public void SelectAll()
    {
      if (_commitsDataGrid != null)
      {
        _commitsDataGrid.SelectAll();
      }
    }

    public void DeselectAll()
    {
      if (_commitsDataGrid != null)
      {
        _commitsDataGrid.UnselectAll();
      }
    }

    public void ViewCommitDetails()
    {
      if (_commitsDataGrid != null && _commitsDataGrid.SelectedItem is CommitInfo selectedCommit)
      {
        // 创建详情内容
        string details = $"仓库: {selectedCommit.Repository}\n" +
                        $"仓库路径: {selectedCommit.RepoPath}\n" +
                        $"提交ID: {selectedCommit.CommitId}\n" +
                        $"作者: {selectedCommit.Author}\n" +
                        $"日期: {selectedCommit.Date}\n" +
                        $"提交信息: {selectedCommit.Message}";

        _dialogManager.ShowCustomMessageBox("提交详情", details, true);
      }
      else
      {
        _dialogManager.ShowCustomMessageBox("提示", "请先选择一个提交记录。", false);
      }
    }

    // 获取选中的提交记录
    public List<CommitInfo> GetSelectedCommits()
    {
      var selectedCommits = new List<CommitInfo>();

      if (_commitsDataGrid != null && _commitsDataGrid.SelectedItems.Count > 0)
      {
        foreach (var item in _commitsDataGrid.SelectedItems)
        {
          if (item is CommitInfo commit)
          {
            selectedCommits.Add(commit);
          }
        }
      }

      return selectedCommits;
    }
  }
}