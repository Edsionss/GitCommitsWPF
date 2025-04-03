using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using System.Windows.Forms;
using GitCommitsWPF.Utils;
using System.Windows.Threading;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using FolderBrowserDialog = System.Windows.Forms.FolderBrowserDialog;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using GitCommitsWPF.Models;
using System.Windows.Media;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Orientation = System.Windows.Controls.Orientation;
using Button = System.Windows.Controls.Button;
using ListBox = System.Windows.Controls.ListBox;
using TextBox = System.Windows.Controls.TextBox;
using SelectionMode = System.Windows.Controls.SelectionMode;
using System.Linq; // 用于Cast方法
using Brushes = System.Windows.Media.Brushes;

namespace GitCommitsWPF.Services
{
  /// <summary>
  /// 路径浏览管理器，用于管理路径选择和位置管理的UI交互
  /// </summary>
  public class PathBrowserManager
  {
    private readonly DialogManager _dialogManager;
    private readonly LocationManager _locationManager;
    private readonly GitOperationsManager _gitOperationsManager;
    private readonly OutputManager _outputManager;
    private readonly Window _owner;
    private readonly Dispatcher _dispatcher;
    private RepositorySelectionManager _repositorySelectionManager;

    // UI控件的引用
    private TextBox _pathsTextBox;
    private System.Windows.Controls.CheckBox _verifyGitPathsCheckBox;
    private System.Windows.Controls.CheckBox _chooseSystemCheckBox;

    public PathBrowserManager(
        Window owner,
        DialogManager dialogManager,
        LocationManager locationManager,
        GitOperationsManager gitOperationsManager,
        OutputManager outputManager)
    {
      _owner = owner;
      _dialogManager = dialogManager;
      _locationManager = locationManager;
      _gitOperationsManager = gitOperationsManager;
      _outputManager = outputManager;
      _dispatcher = Dispatcher.CurrentDispatcher;
    }

    /// <summary>
    /// 设置RepositorySelectionManager引用
    /// </summary>
    public void SetRepositorySelectionManager(RepositorySelectionManager repositorySelectionManager)
    {
      _repositorySelectionManager = repositorySelectionManager;
    }

    /// <summary>
    /// 初始化控件引用
    /// </summary>
    public void Initialize(
        TextBox pathsTextBox,
        System.Windows.Controls.CheckBox verifyGitPathsCheckBox,
        System.Windows.Controls.CheckBox chooseSystemCheckBox)
    {
      _pathsTextBox = pathsTextBox;
      _verifyGitPathsCheckBox = verifyGitPathsCheckBox;
      _chooseSystemCheckBox = chooseSystemCheckBox;
    }

    /// <summary>
    /// 浏览按钮点击事件处理
    /// </summary>
    public void BrowseFolder()
    {
      bool chooseSystemCheck = _chooseSystemCheckBox.IsChecked == true;

      if (chooseSystemCheck)
      {
        // 使用系统文件窗口 (FolderBrowserDialog)
        var dialog = new FolderBrowserDialog
        {
          Description = "选择要扫描Git仓库的路径",
          ShowNewFolderButton = false
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
          string selectedPath = dialog.SelectedPath;

          // 验证路径（如果启用了验证）
          if (_repositorySelectionManager.ValidatePath(selectedPath))
          {
            // 添加选择的路径到文本框
            _repositorySelectionManager.AddPath(selectedPath);

            // 添加到最近位置
            _locationManager.AddToRecentLocations(selectedPath);
            // 确保保存最近位置
            _locationManager.SaveRecentLocations();
          }
        }
      }
      else
      {
        // 使用微软窗口 (OpenFileDialog)
        var dialog = new OpenFileDialog
        {
          Title = "选择要扫描Git仓库的文件夹",
          ValidateNames = false,
          CheckFileExists = false,
          CheckPathExists = true,
          FileName = "选择此文件夹",
          // 将过滤器设置为目录
          Filter = "文件夹|*.this.directory"
        };

        if (dialog.ShowDialog() == true)
        {
          // 从文件路径中获取文件夹路径
          string selectedPath = Path.GetDirectoryName(dialog.FileName);

          // 验证路径（如果启用了验证）
          if (_repositorySelectionManager.ValidatePath(selectedPath))
          {
            // 添加选择的路径到文本框
            _repositorySelectionManager.AddPath(selectedPath);

            // 添加到最近位置
            _locationManager.AddToRecentLocations(selectedPath);
            // 确保保存最近位置
            _locationManager.SaveRecentLocations();
          }
        }
      }
    }

    /// <summary>
    /// 显示最近位置选择对话框
    /// </summary>
    public void ShowRecentLocationsDialog(Action<string> pathAddedCallback)
    {
      if (_locationManager.RecentLocations.Count == 0)
      {
        _dialogManager.ShowCustomMessageBox("信息", "没有最近的位置记录。", false);
        return;
      }

      // 创建一个选择窗口
      var selectWindow = new Window
      {
        Title = "选择最近位置",
        Width = 500,
        Height = 300,
        WindowStartupLocation = WindowStartupLocation.CenterOwner,
        Owner = _owner,
        ResizeMode = ResizeMode.NoResize
      };

      var grid = new Grid();
      grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
      grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
      grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

      // 列表框，显示最近的位置，配置为支持多选
      var listBox = new ListBox
      {
        Margin = new Thickness(10),
        ItemsSource = _locationManager.RecentLocations,
        SelectionMode = SelectionMode.Multiple // 启用多选模式
      };
      Grid.SetRow(listBox, 0);

      // 添加提示文本
      var tipTextBlock = new TextBlock
      {
        Text = "提示: 按住Ctrl键可选择多个路径，或使用全选按钮",
        Margin = new Thickness(10, 0, 10, 5),
        Foreground = Brushes.Gray,
        HorizontalAlignment = HorizontalAlignment.Left
      };
      Grid.SetRow(tipTextBlock, 1);

      // 按钮面板
      var buttonPanel = new StackPanel
      {
        Orientation = Orientation.Horizontal,
        HorizontalAlignment = HorizontalAlignment.Center,
        Margin = new Thickness(0, 0, 0, 10)
      };
      Grid.SetRow(buttonPanel, 2);

      // 添加按钮
      var addButton = new Button
      {
        Content = "添加选中项",
        Padding = new Thickness(15, 5, 15, 5),
        Margin = new Thickness(5),
        MinWidth = 80
      };

      addButton.Click += (s, evt) =>
      {
        if (listBox.SelectedItems.Count > 0)
        {
          int addedCount = 0;

          foreach (var selectedPath in listBox.SelectedItems.Cast<string>())
          {
            // 确保路径被添加到最近位置并保存
            _locationManager.AddToRecentLocations(selectedPath);

            // 通过回调通知添加路径
            pathAddedCallback?.Invoke(selectedPath);
            addedCount++;
          }

          // 保存最近位置（只需要保存一次）
          _locationManager.SaveRecentLocations();

          // 显示成功消息
          _dialogManager.ShowCustomMessageBox("添加完成", $"已成功添加 {addedCount} 个路径", true);

          selectWindow.Close();
        }
        else
        {
          _dialogManager.ShowCustomMessageBox("提示", "请先选择至少一个位置", false);
        }
      };

      // 添加全选按钮
      var selectAllButton = new Button
      {
        Content = "全选",
        Padding = new Thickness(15, 5, 15, 5),
        Margin = new Thickness(5),
        MinWidth = 80
      };

      selectAllButton.Click += (s, evt) =>
      {
        listBox.SelectAll();
      };

      // 清除按钮
      var clearButton = new Button
      {
        Content = "清除记录",
        Padding = new Thickness(15, 5, 15, 5),
        Margin = new Thickness(5),
        MinWidth = 80
      };

      clearButton.Click += (s, evt) =>
      {
        _locationManager.ClearRecentLocations();
        selectWindow.Close();
        _dialogManager.ShowCustomMessageBox("信息", "已清除最近位置记录。", false);
      };

      // 取消按钮
      var cancelButton = new Button
      {
        Content = "取消",
        Padding = new Thickness(15, 5, 15, 5),
        Margin = new Thickness(5),
        MinWidth = 80
      };

      cancelButton.Click += (s, evt) =>
      {
        selectWindow.Close();
      };

      buttonPanel.Children.Add(addButton);
      buttonPanel.Children.Add(selectAllButton);
      buttonPanel.Children.Add(clearButton);
      buttonPanel.Children.Add(cancelButton);
      grid.Children.Add(listBox);
      grid.Children.Add(tipTextBlock);
      grid.Children.Add(buttonPanel);

      selectWindow.Content = grid;
      selectWindow.ShowDialog();
    }

    /// <summary>
    /// 选择保存路径对话框
    /// </summary>
    public void SelectOutputPath(TextBox outputPathTextBox)
    {
      // 使用FileUtility生成默认文件名
      string defaultFileName = FileUtility.GenerateDefaultFileName();
      var dialog = new SaveFileDialog
      {
        Title = "保存结果",
        Filter = "*.txt|*.csv|JSON文件|*.json|文本文件|CSV文件|HTML文件|*.html|XML文件|*.xml|所有文件|*.*",
        DefaultExt = ".txt",
        FileName = defaultFileName,
      };

      if (dialog.ShowDialog() == true)
      {
        outputPathTextBox.Text = dialog.FileName;
      }
    }

    /// <summary>
    /// 显示最近保存位置选择对话框
    /// </summary>
    public void ShowRecentSaveLocationsDialog(TextBox outputPathTextBox)
    {
      if (_locationManager.SaveLocations.Count == 0)
      {
        _dialogManager.ShowCustomMessageBox("信息", "没有最近的保存位置记录。", false);
        return;
      }

      // 创建一个选择窗口
      var selectWindow = new Window
      {
        Title = "选择最近保存位置",
        Width = 500,
        Height = 300,
        WindowStartupLocation = WindowStartupLocation.CenterOwner,
        Owner = _owner,
        ResizeMode = ResizeMode.NoResize
      };

      var grid = new Grid();
      grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
      grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

      // 列表框，显示最近的保存位置
      var listBox = new ListBox
      {
        Margin = new Thickness(10),
        ItemsSource = _locationManager.SaveLocations
      };
      Grid.SetRow(listBox, 0);

      // 按钮面板
      var buttonPanel = new StackPanel
      {
        Orientation = Orientation.Horizontal,
        HorizontalAlignment = HorizontalAlignment.Center,
        Margin = new Thickness(0, 0, 0, 10)
      };
      Grid.SetRow(buttonPanel, 1);

      // 添加按钮
      var addButton = new Button
      {
        Content = "使用",
        Padding = new Thickness(15, 5, 15, 5),
        Margin = new Thickness(5),
        MinWidth = 80
      };

      addButton.Click += (s, evt) =>
      {
        if (listBox.SelectedItem is string selectedPath)
        {
          // 验证文件路径是否存在
          if (File.Exists(selectedPath))
          {
            // 生成新的文件名，使用原始文件的目录和扩展名
            string newPath = Path.Combine(
              Path.GetDirectoryName(selectedPath),
              "GIT提交记录查询结果" + DateTime.Now.ToString("yyyyMMddHHmmss") + Path.GetExtension(selectedPath));

            if (File.Exists(newPath))
            {
              // 询问用户是否覆盖已有文件
              bool overwrite = _dialogManager.ShowCustomConfirmDialog(
                "文件已存在",
                $"文件 {Path.GetFileName(newPath)} 已存在，是否覆盖？");

              if (!overwrite)
              {
                // 用户选择不覆盖，返回
                return;
              }
            }

            // 更新输出路径文本框
            outputPathTextBox.Text = newPath;

            // 确保将选择的路径添加到最近保存位置并保存
            _locationManager.AddToSaveLocations(selectedPath);
            _locationManager.SaveSaveLocations();

            selectWindow.Close();
          }
          else
          {
            _dialogManager.ShowCustomMessageBox("提示", "文件路径不存在", false);
          }
        }
        else
        {
          _dialogManager.ShowCustomMessageBox("提示", "请先选择一个保存位置", false);
        }
      };

      // 清除按钮
      var clearButton = new Button
      {
        Content = "清除记录",
        Padding = new Thickness(15, 5, 15, 5),
        Margin = new Thickness(5),
        MinWidth = 80
      };

      clearButton.Click += (s, evt) =>
      {
        _locationManager.ClearSaveLocations();
        selectWindow.Close();
        _dialogManager.ShowCustomMessageBox("信息", "已清除最近保存位置记录。", false);
      };

      // 取消按钮
      var cancelButton = new Button
      {
        Content = "取消",
        Padding = new Thickness(15, 5, 15, 5),
        Margin = new Thickness(5),
        MinWidth = 80
      };

      cancelButton.Click += (s, evt) =>
      {
        selectWindow.Close();
      };

      buttonPanel.Children.Add(addButton);
      buttonPanel.Children.Add(clearButton);
      buttonPanel.Children.Add(cancelButton);

      // 确保将listBox和buttonPanel添加到grid中
      grid.Children.Add(listBox);
      grid.Children.Add(buttonPanel);

      selectWindow.Content = grid;
      selectWindow.ShowDialog();
    }
  }
}