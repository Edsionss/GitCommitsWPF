using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using GitCommitsWPF.Utils;

namespace GitCommitsWPF.Views
{
  /// <summary>
  /// AuthorSelectionWindow.xaml 的交互逻辑
  /// </summary>
  public partial class AuthorSelectionWindow : Window
  {
    private AuthorManager _authorManager;
    private DialogManager _dialogManager;

    /// <summary>
    /// 获取或设置用户选择的作者名称
    /// </summary>
    public string SelectedAuthor { get; private set; }

    /// <summary>
    /// 初始化作者选择窗口
    /// </summary>
    /// <param name="authorManager">作者管理器</param>
    /// <param name="dialogManager">对话框管理器</param>
    public AuthorSelectionWindow(AuthorManager authorManager, DialogManager dialogManager)
    {
      InitializeComponent();

      _authorManager = authorManager;
      _dialogManager = dialogManager;

      // 初始化最近使用的作者列表
      RefreshRecentAuthors();

      // 初始化扫描到的作者列表
      RefreshScannedAuthors();

      // 添加搜索功能
      SearchBox.TextChanged += SearchBox_TextChanged;
    }

    /// <summary>
    /// 刷新最近使用的作者列表
    /// </summary>
    private void RefreshRecentAuthors()
    {
      RecentListBox.ItemsSource = _authorManager.RecentAuthors;
    }

    /// <summary>
    /// 刷新扫描到的作者列表
    /// </summary>
    private void RefreshScannedAuthors()
    {
      ScannedListBox.ItemsSource = _authorManager.ScannedAuthors.OrderBy(a => a).ToList();
    }

    /// <summary>
    /// 搜索框文本变更事件处理
    /// </summary>
    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
      string filter = SearchBox.Text.ToLower();
      if (string.IsNullOrEmpty(filter))
      {
        ScannedListBox.ItemsSource = _authorManager.ScannedAuthors.OrderBy(a => a).ToList();
      }
      else
      {
        ScannedListBox.ItemsSource = _authorManager.ScannedAuthors
            .Where(a => a.ToLower().Contains(filter))
            .OrderBy(a => a)
            .ToList();
      }
    }

    /// <summary>
    /// 选择按钮点击事件
    /// </summary>
    private void SelectButton_Click(object sender, RoutedEventArgs e)
    {
      ListBox selectedListBox = null;
      if (AuthorTabControl.SelectedItem == RecentTab)
      {
        selectedListBox = RecentListBox;
      }
      else if (AuthorTabControl.SelectedItem == ScannedTab)
      {
        selectedListBox = ScannedListBox;
      }

      if (selectedListBox != null && selectedListBox.SelectedItem is string selectedAuthor)
      {
        SelectedAuthor = selectedAuthor;

        // 如果选择的是扫描作者，添加到最近作者列表
        if (AuthorTabControl.SelectedItem == ScannedTab)
        {
          _authorManager.AddToRecentAuthors(selectedAuthor);
        }

        DialogResult = true;
        Close();
      }
      else
      {
        _dialogManager.ShowCustomMessageBox("提示", "请先选择一个作者", false);
      }
    }

    /// <summary>
    /// 清除记录按钮点击事件
    /// </summary>
    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
      if (AuthorTabControl.SelectedItem == RecentTab)
      {
        // 清除最近作者记录
        _authorManager.RecentAuthors.Clear();
        _authorManager.SaveRecentAuthors();
        RefreshRecentAuthors();
        _dialogManager.ShowCustomMessageBox("信息", "已清除最近作者记录。", false);
      }
      else if (AuthorTabControl.SelectedItem == ScannedTab)
      {
        // 清除扫描作者记录
        _authorManager.ScannedAuthors.Clear();
        _authorManager.SaveScannedAuthors();
        RefreshScannedAuthors();
        _dialogManager.ShowCustomMessageBox("信息", "已清除扫描作者记录。", false);
      }
    }

    /// <summary>
    /// 取消按钮点击事件
    /// </summary>
    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
      DialogResult = false;
      Close();
    }

    /// <summary>
    /// 从已有提交中初始化作者列表（如果最近作者列表为空）
    /// </summary>
    /// <param name="commits">提交信息列表</param>
    public void InitializeFromCommits(IEnumerable<Models.CommitInfo> commits)
    {
      // 如果最近作者列表为空，则尝试从已有提交中收集
      if (_authorManager.RecentAuthors.Count == 0 && commits != null)
      {
        // 从已有提交中收集作者信息
        HashSet<string> uniqueAuthors = new HashSet<string>();
        foreach (var commit in commits)
        {
          if (!string.IsNullOrEmpty(commit.Author))
          {
            uniqueAuthors.Add(commit.Author);
          }
        }

        // 如果找到作者，添加到最近作者列表
        if (uniqueAuthors.Count > 0)
        {
          foreach (var author in uniqueAuthors)
          {
            _authorManager.AddToRecentAuthors(author);
          }

          // 刷新列表
          RefreshRecentAuthors();
        }
        else
        {
          _dialogManager.ShowCustomMessageBox("提示", "尚未找到任何作者信息。请先执行查询或添加作者。", false);
          DialogResult = false;
          Close();
        }
      }
    }
  }
}