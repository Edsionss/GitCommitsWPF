using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GitCommitsWPF.Models;
using GitCommitsWPF.Services;
using GitCommitsWPF.Utils;
using Newtonsoft.Json;
using Formatting = Newtonsoft.Json.Formatting;

namespace GitCommitsWPF.Services
{
  public class DataGridManager
  {
    private readonly Utils.DialogManager _dialogManager;
    private DataGrid _commitsDataGrid;
    private Action<string, string, bool> _showMessageBoxAction;
    private Func<string> _getFormatText;

    public DataGridManager(Utils.DialogManager dialogManager)
    {
      _dialogManager = dialogManager;
    }

    /// <summary>
    /// 初始化DataGridManager
    /// </summary>
    /// <param name="commitsDataGrid">DataGrid控件</param>
    /// <param name="showMessageBoxAction">显示消息框的回调</param>
    /// <param name="getFormatText">获取格式化模板的回调</param>
    public void Initialize(DataGrid commitsDataGrid, Action<string, string, bool> showMessageBoxAction, Func<string> getFormatText)
    {
      _commitsDataGrid = commitsDataGrid;
      _showMessageBoxAction = showMessageBoxAction;
      _getFormatText = getFormatText;
      ConfigureDataGrid();
    }

    /// <summary>
    /// 配置DataGrid的属性和行为
    /// </summary>
    private void ConfigureDataGrid()
    {
      // 设置DataGrid的排序行为
      _commitsDataGrid.Sorting += (s, e) =>
      {
        // 可以在这里添加自定义排序逻辑
        e.Handled = false; // 使用默认排序行为
      };

      // 设置DataGrid的选择模式
      _commitsDataGrid.SelectionMode = DataGridSelectionMode.Extended;
      _commitsDataGrid.SelectionUnit = DataGridSelectionUnit.FullRow;

      // 双击行时查看详细信息
      _commitsDataGrid.MouseDoubleClick += CommitsDataGrid_MouseDoubleClick;
    }

    /// <summary>
    /// 处理DataGrid的双击事件
    /// </summary>
    private void CommitsDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
      if (_commitsDataGrid.SelectedItem is CommitInfo selectedCommit)
      {
        var details = new StringBuilder();
        details.AppendLine("提交详情：");
        details.AppendLine(string.Format("仓库: {0}", selectedCommit.Repository));
        details.AppendLine(string.Format("仓库路径: {0}", selectedCommit.RepoPath));
        details.AppendLine(string.Format("提交ID: {0}", selectedCommit.CommitId));
        details.AppendLine(string.Format("作者: {0}", selectedCommit.Author));
        details.AppendLine(string.Format("日期: {0}", selectedCommit.Date));
        details.AppendLine(string.Format("消息: {0}", selectedCommit.Message));

        _showMessageBoxAction("提交详情", details.ToString(), false);
      }
    }

    /// <summary>
    /// 复制选中的行
    /// </summary>
    public void CopySelectedRows()
    {
      try
      {
        var selectedItems = _commitsDataGrid.SelectedItems.Cast<CommitInfo>().ToList();
        if (selectedItems.Count == 0) return;

        StringBuilder clipboardText = new StringBuilder();
        string formatTemplate = _getFormatText();

        foreach (var item in selectedItems)
        {
          string line = "";

          // 使用和显示相同的格式
          if (!string.IsNullOrEmpty(formatTemplate))
          {
            line = formatTemplate;
            line = line.Replace("{Repository}", item.Repository)
                .Replace("{RepoPath}", item.RepoPath)
                .Replace("{RepoFolder}", item.RepoFolder)
                .Replace("{CommitId}", item.CommitId)
                .Replace("{Author}", item.Author)
                .Replace("{Date}", item.Date)
                .Replace("{Message}", item.Message);
          }
          else
          {
            // 默认格式
            line = string.Format("{0}: {1}", item.Repository, item.Message);
          }

          clipboardText.AppendLine(line);
        }

        System.Windows.Clipboard.SetText(clipboardText.ToString());
        _showMessageBoxAction("复制成功", string.Format("已复制 {0} 行数据到剪贴板", _commitsDataGrid.SelectedItems.Count), false);
      }
      catch (Exception ex)
      {
        _showMessageBoxAction("错误", string.Format("复制到剪贴板时出错: {0}", ex.Message), false);
      }
    }

    /// <summary>
    /// 导出选中的行到剪贴板
    /// </summary>
    public void ExportSelectedToClipboard()
    {
      try
      {
        var selectedItems = _commitsDataGrid.SelectedItems.Cast<CommitInfo>().ToList();
        if (selectedItems.Count == 0) return;

        // 将选中的行转换为JSON格式
        var jsonItems = selectedItems.Select(item => new
        {
          Repository = item.Repository,
          RepoPath = item.RepoPath,
          RepoFolder = item.RepoFolder,
          CommitId = item.CommitId,
          Author = item.Author,
          Date = item.Date,
          Message = item.Message
        }).ToList();

        string json = JsonConvert.SerializeObject(jsonItems, Formatting.Indented);
        System.Windows.Clipboard.SetText(json);
        _showMessageBoxAction("导出成功", string.Format("已导出 {0} 行数据到剪贴板 (JSON格式)", _commitsDataGrid.SelectedItems.Count), false);
      }
      catch (Exception ex)
      {
        _showMessageBoxAction("错误", string.Format("导出到剪贴板时出错: {0}", ex.Message), false);
      }
    }

    /// <summary>
    /// 选择所有行
    /// </summary>
    public void SelectAll()
    {
      _commitsDataGrid.SelectAll();
    }

    /// <summary>
    /// 取消选择所有行
    /// </summary>
    public void DeselectAll()
    {
      _commitsDataGrid.UnselectAll();
    }

    /// <summary>
    /// 查看提交详情
    /// </summary>
    public void ViewCommitDetails()
    {
      if (_commitsDataGrid.SelectedItem is CommitInfo selectedCommit)
      {
        var details = new StringBuilder();
        details.AppendLine("提交详情：");
        details.AppendLine(string.Format("仓库: {0}", selectedCommit.Repository));
        details.AppendLine(string.Format("仓库路径: {0}", selectedCommit.RepoPath));
        details.AppendLine(string.Format("提交ID: {0}", selectedCommit.CommitId));
        details.AppendLine(string.Format("作者: {0}", selectedCommit.Author));
        details.AppendLine(string.Format("日期: {0}", selectedCommit.Date));
        details.AppendLine(string.Format("消息: {0}", selectedCommit.Message));

        _showMessageBoxAction("提交详情", details.ToString(), false);
      }
    }

    /// <summary>
    /// 更新表格数据源
    /// </summary>
    /// <param name="commits">提交数据</param>
    public void UpdateDataSource(List<CommitInfo> commits)
    {
      _commitsDataGrid.ItemsSource = commits;
    }
  }
}