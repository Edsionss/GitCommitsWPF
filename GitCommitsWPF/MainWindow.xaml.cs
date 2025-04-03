using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Threading;
using System.Xml;
using Microsoft.Win32;
using Newtonsoft.Json;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using FolderBrowserDialog = System.Windows.Forms.FolderBrowserDialog;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using MessageBox = System.Windows.MessageBox;
using Button = System.Windows.Controls.Button;
using ListBox = System.Windows.Controls.ListBox;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Formatting = Newtonsoft.Json.Formatting;
using TabControl = System.Windows.Controls.TabControl;
using TabItem = System.Windows.Controls.TabItem;
using DockPanel = System.Windows.Controls.DockPanel;
using GitCommitsWPF.Models;
using GitCommitsWPF.Services;
using GitCommitsWPF.Utils;

namespace GitCommitsWPF
{
  public partial class MainWindow : Window
  {
    private List<CommitInfo> _allCommits = new List<CommitInfo>();
    private int _repoCount = 0;
    private int _currentRepo = 0;
    private bool _isRunning = false;
    private List<CommitInfo> _filteredCommits = new List<CommitInfo>(); // 添加筛选后的提交列表
    private string _tempScanPath = ""; // 用于保存临时扫描路径

    // AuthorManager实例，用于管理作者相关功能
    private AuthorManager _authorManager = new AuthorManager();

    // LocationManager实例，用于管理位置相关功能
    private LocationManager _locationManager = new LocationManager();

    // OutputManager实例，用于管理输出和进度条
    private OutputManager _outputManager;

    // DialogManager实例，用于管理对话框
    private DialogManager _dialogManager;

    // LogOperationsManager实例，用于管理日志操作
    private LogOperationsManager _logOperationsManager;

    // ExportManager实例，用于管理导出和保存结果
    private ExportManager _exportManager;

    // GitOperationsManager实例，用于管理Git相关操作
    private GitOperationsManager _gitOperationsManager;

    // StatisticsManager实例，用于管理统计功能
    private StatisticsManager _statisticsManager = new StatisticsManager();

    // SearchFilterManager实例，用于管理搜索过滤功能
    private SearchFilterManager _searchFilterManager = new SearchFilterManager();

    // FormattingManager实例，用于管理格式化显示功能
    private FormattingManager _formattingManager = new FormattingManager();

    // DataGridManager实例，用于管理DataGrid相关功能
    private DataGridManager _dataGridManager;

    // QueryExecutionManager实例，用于管理查询执行和结果处理
    private QueryExecutionManager _queryExecutionManager;

    public MainWindow()
    {
      InitializeComponent();

      // 设置默认值
      TimeRangeComboBox.SelectedIndex = 0; // 默认选择'所有时间'
      FormatTextBox.Text = "{Repository} : {Message}";

      // 设置日期选择器为当前日期
      StartDatePicker.SelectedDate = DateTime.Today;
      EndDatePicker.SelectedDate = DateTime.Today;

      // 初始化输出管理器
      _outputManager = new OutputManager(ResultTextBox, ProgressBar, Dispatcher);

      // 初始化对话框管理器
      _dialogManager = new DialogManager(this);

      // 初始化日志操作管理器
      _logOperationsManager = new LogOperationsManager(_dialogManager, _outputManager);

      // 初始化导出管理器
      _exportManager = new ExportManager(_dialogManager);

      // 初始化Git操作管理器
      _gitOperationsManager = new GitOperationsManager(_outputManager, _dialogManager, _authorManager);

      // 初始化DataGrid管理器
      _dataGridManager = new DataGridManager(_dialogManager);
      _dataGridManager.Initialize(CommitsDataGrid, ShowCustomMessageBox, () => FormatTextBox.Text);

      // 初始化查询执行管理器
      _queryExecutionManager = new QueryExecutionManager(
        _outputManager,
        _gitOperationsManager,
        _dialogManager,
        _statisticsManager,
        _formattingManager,
        _dataGridManager);

      // 监听作者文本框变化
      AuthorTextBox.TextChanged += (s, e) =>
      {
        if (!string.IsNullOrWhiteSpace(AuthorTextBox.Text))
        {
          _authorManager.AddToRecentAuthors(AuthorTextBox.Text);
        }
      };

      // 加载扫描到的作者
      _authorManager.LoadScannedAuthors();

      // 加载最近保存位置
      LoadSaveLocations();
      // 加载最近使用的位置
      LoadRecentLocations();

      // 添加Loaded事件处理器，确保UI完全加载后执行初始化
      this.Loaded += MainWindow_Loaded;

      // 添加关闭事件处理器，确保应用程序退出时保存设置
      this.Closing += MainWindow_Closing;
    }

    // 窗口加载完成后的处理
    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
      // 再次确保加载最近保存位置和最近使用的位置
      LoadSaveLocations();
      LoadRecentLocations();
    }

    // 窗口关闭时的处理
    private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
      // 保存最近保存位置和最近使用的位置
      SaveSaveLocations();
      SaveRecentLocations();
    }

    // 配置DataGrid的属性和行为
    private void ConfigureDataGrid()
    {
      // 使用DataGridManager进行配置，无需在此处直接配置
      // 已在构造函数中通过_dataGridManager.Initialize完成配置
    }

    // 加载最近使用的位置
    private void LoadRecentLocations()
    {
      _locationManager.LoadRecentLocations();
    }

    // 保存最近使用的位置
    private void SaveRecentLocations()
    {
      _locationManager.SaveRecentLocations();
    }

    // 添加路径到最近位置列表
    private void AddToRecentLocations(string path)
    {
      _locationManager.AddToRecentLocations(path);
    }

    // 验证路径是否为Git仓库
    private bool ValidatePath(string path)
    {
      bool verifyGitPaths = false;
      Dispatcher.Invoke(() =>
      {
        verifyGitPaths = VerifyGitPathsCheckBox.IsChecked == true;
      });

      return _gitOperationsManager.ValidatePath(path, verifyGitPaths);
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
      bool ChooseSystemCheck = ChooseSystemCheckBox.IsChecked == true;

      if (ChooseSystemCheck)
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
          if (ValidatePath(selectedPath))
          {
            // 添加选择的路径到文本框
            if (!string.IsNullOrEmpty(PathsTextBox.Text))
            {
              PathsTextBox.Text += Environment.NewLine;
            }
            PathsTextBox.Text += selectedPath;

            // 添加到最近位置
            AddToRecentLocations(selectedPath);
            // 确保保存最近位置
            SaveRecentLocations();
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
          if (ValidatePath(selectedPath))
          {
            // 添加选择的路径到文本框
            if (!string.IsNullOrEmpty(PathsTextBox.Text))
            {
              PathsTextBox.Text += Environment.NewLine;
            }
            PathsTextBox.Text += selectedPath;

            // 添加到最近位置
            AddToRecentLocations(selectedPath);
            // 确保保存最近位置
            SaveRecentLocations();
          }
        }
      }
    }

    private void OutputPathButton_Click(object sender, RoutedEventArgs e)
    {
      // 创建默认文件名：Git提交记录_年月日.csv
      string defaultFileName = string.Format("Git提交记录_{0}.txt", DateTime.Now.ToString("yyyyMMdd"));
      var dialog = new SaveFileDialog
      {
        Title = "保存结果",
        Filter = "*.txt|*.csv|JSON文件|*.json|文本文件|CSV文件|HTML文件|*.html|XML文件|*.xml|所有文件|*.*",
        DefaultExt = ".txt",
        FileName = defaultFileName,

      };

      if (dialog.ShowDialog() == true)
      {
        OutputPathTextBox.Text = dialog.FileName;
      }
    }


    private void TimeRangeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      var selectedItem = TimeRangeComboBox.SelectedItem as ComboBoxItem;
      if (selectedItem != null)
      {
        var timeRange = selectedItem.Tag.ToString();
        // 仅在选择"custom"时启用日期选择器
        bool isCustom = timeRange == "custom";
        StartDatePicker.IsEnabled = isCustom;
        EndDatePicker.IsEnabled = isCustom;
      }
    }

    // 开始按钮点击事件处理
    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
      // 设置执行状态并获取所需参数
      string pathsText = PathsTextBox.Text;
      string timeRange = ((ComboBoxItem)TimeRangeComboBox.SelectedItem).Tag.ToString();
      DateTime? startDate = StartDatePicker.SelectedDate;
      DateTime? endDate = EndDatePicker.SelectedDate;
      string author = AuthorTextBox.Text;
      string authorFilter = AuthorFilterTextBox.Text;
      bool verifyGitPaths = VerifyGitPathsCheckBox.IsChecked == true;

      // 使用QueryExecutionManager执行查询
      await _queryExecutionManager.ExecuteQuery(
        pathsText,
        timeRange,
        startDate,
        endDate,
        author,
        authorFilter,
        verifyGitPaths,
        // 获取选择的字段
        (repository, repoPath, repoFolder, commitId, author1, date, message) =>
          _formattingManager.GetSelectedFields(
            repository,
            repoPath,
            repoFolder,
            commitId,
            author1,
            date,
            message),
        // 获取格式文本
        () => FormatTextBox.Text,
        // 获取是否显示重复的仓库名
        () => ShowRepeatedRepoNamesCheckBox.IsChecked == true,
        // 获取是否启用统计
        () => EnableStatsCheckBox.IsChecked == true,
        // 获取按作者统计
        () => StatsByAuthorCheckBox.IsChecked == true,
        // 获取按仓库统计
        () => StatsByRepoCheckBox.IsChecked == true,
        // 获取按日期统计
        () => StatsByDateCheckBox.IsChecked == true,
        // 更新格式化结果文本框
        (textBox) =>
        {
          var formattedResultTextBox = this.FindName("FormattedResultTextBox") as System.Windows.Controls.TextBox;
          if (formattedResultTextBox != null)
          {
            formattedResultTextBox.Text = textBox.Text;
          }
        },
        // 启用开始按钮
        () => StartButton.IsEnabled = false,
        // 启用停止按钮
        () => StopButton.IsEnabled = true,
        // 启用保存按钮
        () => SaveButton.IsEnabled = true,
        // 清空搜索框
        () => SearchFilterTextBox.Clear());

      // 查询完成后，将查询结果同步到字段
      _allCommits = _queryExecutionManager.AllCommits;
      _filteredCommits = _queryExecutionManager.FilteredCommits;
      _isRunning = _queryExecutionManager.IsRunning;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
      if (_filteredCommits.Count == 0)
      {
        _dialogManager.ShowCustomMessageBox("提示", "没有可保存的提交记录。", false);
        return;
      }

      // 使用ExportManager显示保存选项对话框
      _exportManager.ShowSaveOptionsDialog(this,
        // 保存到文件的回调
        () =>
        {
          SaveToFile();
        },
        // 复制到剪贴板的回调
        () =>
        {
          CopyResultToClipboard();
        });
    }

    private void SaveToFile()
    {
      if (string.IsNullOrEmpty(OutputPathTextBox.Text))
      {
        // 创建默认文件名：Git提交记录_年月日.csv
        string defaultFileName = $"Git提交记录_{DateTime.Now.ToString("yyyyMMdd")}.txt";

        string outputPath = _exportManager.ShowSaveFileDialog(defaultFileName);
        if (!string.IsNullOrEmpty(outputPath))
        {
          OutputPathTextBox.Text = outputPath;
        }
        else
        {
          return;
        }
      }

      try
      {
        string filePath = OutputPathTextBox.Text;
        SaveResults(filePath);

        // 添加到最近保存位置
        AddToSaveLocations(filePath);

        // 刷新结果页签的内容
        UpdateFormattedResultTextBox();
      }
      catch (Exception ex)
      {
        _dialogManager.ShowCustomMessageBox("保存错误", $"保存文件时发生错误：{ex.Message}", false);
      }
    }

    private void CopyResultToClipboard()
    {
      try
      {
        if (_filteredCommits.Count == 0)
        {
          _dialogManager.ShowCustomMessageBox("提示", "没有可复制的提交记录。", false);
          return;
        }

        // 获取统计信息
        var statsBuilder = new StringBuilder();
        _queryExecutionManager.GenerateStats(
            statsBuilder,
            StatsByAuthorCheckBox.IsChecked == true,
            StatsByRepoCheckBox.IsChecked == true,
            StatsByDateCheckBox.IsChecked == true);
        string statsOutput = statsBuilder.ToString();

        // 获取用户自定义格式模板
        string formatTemplate = FormatTextBox.Text;

        // 获取是否包含统计信息
        bool includeStats = EnableStatsCheckBox.IsChecked == true;

        // 获取选择的字段
        List<string> selectedFields = new List<string>();
        if (RepositoryFieldCheckBox.IsChecked == true) selectedFields.Add("Repository");
        if (RepoPathFieldCheckBox.IsChecked == true) selectedFields.Add("RepoPath");
        if (RepoFolderFieldCheckBox.IsChecked == true) selectedFields.Add("RepoFolder");
        if (CommitIdFieldCheckBox.IsChecked == true) selectedFields.Add("CommitId");
        if (AuthorFieldCheckBox.IsChecked == true) selectedFields.Add("Author");
        if (DateFieldCheckBox.IsChecked == true) selectedFields.Add("Date");
        if (MessageFieldCheckBox.IsChecked == true) selectedFields.Add("Message");

        // 复制结果到剪贴板
        _exportManager.CopyResultToClipboard(_filteredCommits, statsOutput, includeStats, formatTemplate, selectedFields);
      }
      catch (Exception ex)
      {
        _dialogManager.ShowCustomMessageBox("复制错误", $"复制到剪贴板时发生错误：{ex.Message}", false);
      }
    }

    private void SaveResults(string outputPath)
    {
      try
      {
        if (_filteredCommits.Count == 0)
        {
          _dialogManager.ShowCustomMessageBox("提示", "没有可保存的提交记录。", false);
          return;
        }

        // 获取统计信息
        var statsBuilder = new StringBuilder();
        _queryExecutionManager.GenerateStats(
            statsBuilder,
            StatsByAuthorCheckBox.IsChecked == true,
            StatsByRepoCheckBox.IsChecked == true,
            StatsByDateCheckBox.IsChecked == true);
        string statsOutput = statsBuilder.ToString();

        // 获取用户自定义格式模板
        string formatTemplate = FormatTextBox.Text;

        // 获取是否包含统计信息
        bool includeStats = EnableStatsCheckBox.IsChecked == true;

        // 获取选择的字段
        List<string> selectedFields = new List<string>();
        if (RepositoryFieldCheckBox.IsChecked == true) selectedFields.Add("Repository");
        if (RepoPathFieldCheckBox.IsChecked == true) selectedFields.Add("RepoPath");
        if (RepoFolderFieldCheckBox.IsChecked == true) selectedFields.Add("RepoFolder");
        if (CommitIdFieldCheckBox.IsChecked == true) selectedFields.Add("CommitId");
        if (AuthorFieldCheckBox.IsChecked == true) selectedFields.Add("Author");
        if (DateFieldCheckBox.IsChecked == true) selectedFields.Add("Date");
        if (MessageFieldCheckBox.IsChecked == true) selectedFields.Add("Message");

        // 保存结果
        _exportManager.SaveResults(outputPath, _filteredCommits, statsOutput, includeStats, formatTemplate, selectedFields);
      }
      catch (Exception ex)
      {
        _dialogManager.ShowCustomMessageBox("错误", $"保存文件时出错: {ex.Message}", false);
      }
    }

    // 辅助方法：更新结果页签内容
    private void UpdateFormattedResultTextBox()
    {
      var formattedResultTextBox = this.FindName("FormattedResultTextBox") as System.Windows.Controls.TextBox;
      if (formattedResultTextBox != null)
      {
        // 获取选择的字段
        List<string> selectedFields = _formattingManager.GetSelectedFields(
            RepositoryFieldCheckBox.IsChecked == true,
            RepoPathFieldCheckBox.IsChecked == true,
            RepoFolderFieldCheckBox.IsChecked == true,
            CommitIdFieldCheckBox.IsChecked == true,
            AuthorFieldCheckBox.IsChecked == true,
            DateFieldCheckBox.IsChecked == true,
            MessageFieldCheckBox.IsChecked == true);

        // 生成统计数据(如果启用)
        var statsOutput = new StringBuilder();
        bool enableStats = EnableStatsCheckBox.IsChecked == true;

        if (enableStats)
        {
          statsOutput.AppendLine("\n======== 提交统计 ========\n");

          // 使用StatisticsManager生成统计信息
          _statisticsManager.GenerateStats(
              _allCommits,
              statsOutput,
              StatsByAuthorCheckBox.IsChecked == true,
              StatsByRepoCheckBox.IsChecked == true,
              StatsByDateCheckBox.IsChecked == true);

          statsOutput.AppendLine("\n==========================\n");
        }

        string formattedContent = string.Empty;

        // 应用自定义格式
        string format = FormatTextBox.Text;
        if (!string.IsNullOrEmpty(format))
        {
          // 使用FormattingManager格式化提交记录
          string formattedOutput = _formattingManager.FormatCommits(
              _allCommits,
              format,
              ShowRepeatedRepoNamesCheckBox.IsChecked == true);

          // 合并统计和格式化输出
          formattedContent = _formattingManager.CombineOutput(statsOutput.ToString(), formattedOutput, enableStats);
        }
        else
        {
          // 如果没有指定格式，使用JSON格式
          var filteredCommits = new List<CommitInfo>();
          foreach (var commit in _allCommits)
          {
            // 使用FormattingManager创建过滤后的提交信息
            filteredCommits.Add(_formattingManager.CreateFilteredCommit(commit, selectedFields));
          }

          // 使用JSON格式
          string jsonOutput = JsonConvert.SerializeObject(filteredCommits, Newtonsoft.Json.Formatting.Indented);
          formattedContent = _formattingManager.CombineOutput(statsOutput.ToString(), jsonOutput, enableStats);
        }

        formattedResultTextBox.Text = formattedContent;
      }
    }

    // 显示复制成功的消息框（不显示文件相关按钮）
    private void ShowCopySuccessMessageBox(string title, string message)
    {
      _dialogManager.ShowCopySuccessMessageBox(title, message);
    }

    // 更新输出内容
    private void UpdateOutput(string message)
    {
      _outputManager.UpdateOutput(message);
    }

    // 更新进度条
    private void UpdateProgressBar(int value)
    {
      _outputManager.UpdateProgressBar(value);
    }

    // 添加最近位置按钮点击事件
    private void AddRecentLocation_Click(object sender, RoutedEventArgs e)
    {
      if (_locationManager.RecentLocations.Count == 0)
      {
        ShowCustomMessageBox("信息", "没有最近的位置记录。", false);
        return;
      }

      // 创建一个选择窗口
      var selectWindow = new Window
      {
        Title = "选择最近位置",
        Width = 500,
        Height = 300,
        WindowStartupLocation = WindowStartupLocation.CenterOwner,
        Owner = this,
        ResizeMode = ResizeMode.NoResize
      };

      var grid = new Grid();
      grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
      grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

      // 列表框，显示最近的位置
      var listBox = new ListBox
      {
        Margin = new Thickness(10),
        ItemsSource = _locationManager.RecentLocations
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
        Content = "添加",
        Padding = new Thickness(15, 5, 15, 5),
        Margin = new Thickness(5),
        MinWidth = 80
      };

      addButton.Click += (s, evt) =>
      {
        if (listBox.SelectedItem is string selectedPath)
        {
          if (!string.IsNullOrEmpty(PathsTextBox.Text))
          {
            PathsTextBox.Text += Environment.NewLine;
          }
          PathsTextBox.Text += selectedPath;

          // 确保路径被添加到最近位置并保存
          AddToRecentLocations(selectedPath);
          SaveRecentLocations();

          selectWindow.Close();
        }
        else
        {
          ShowCustomMessageBox("提示", "请先选择一个位置", false);
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
        _locationManager.ClearRecentLocations();
        selectWindow.Close();
        ShowCustomMessageBox("信息", "已清除最近位置记录。", false);
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
      grid.Children.Add(listBox);
      grid.Children.Add(buttonPanel);

      selectWindow.Content = grid;
      selectWindow.ShowDialog();
    }

    private void ClearPaths_Click(object sender, RoutedEventArgs e)
    {
      if (string.IsNullOrEmpty(PathsTextBox.Text))
      {
        ShowCustomMessageBox("信息", "路径已为空。", false);
        return;
      }

      if (ShowCustomConfirmDialog("确认", "确定要清空所有路径吗？"))
      {
        PathsTextBox.Text = "";
      }
    }

    // 用于确认操作的自定义对话框
    private bool ShowCustomConfirmDialog(string title, string message)
    {
      return _dialogManager.ShowCustomConfirmDialog(title, message);
    }

    // 表格上下文菜单事件处理
    private void CopySelectedRows_Click(object sender, RoutedEventArgs e)
    {
      _dataGridManager.CopySelectedRows();
    }

    private void ExportSelectedToClipboard_Click(object sender, RoutedEventArgs e)
    {
      _dataGridManager.ExportSelectedToClipboard();
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
      _dataGridManager.SelectAll();
    }

    private void DeselectAll_Click(object sender, RoutedEventArgs e)
    {
      _dataGridManager.DeselectAll();
    }

    private void ViewCommitDetails_Click(object sender, RoutedEventArgs e)
    {
      _dataGridManager.ViewCommitDetails();
    }

    // 搜索过滤相关方法
    private void SearchFilterTextBox_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
    {
      if (e.Key == System.Windows.Input.Key.Enter)
      {
        ApplyFilter();
      }
    }

    private void SearchButton_Click(object sender, RoutedEventArgs e)
    {
      ApplyFilter();
    }

    private void ClearSearchButton_Click(object sender, RoutedEventArgs e)
    {
      SearchFilterTextBox.Text = "";
      ApplyFilter();
    }

    private void ApplyFilter()
    {
      string filterText = SearchFilterTextBox.Text.Trim().ToLower();

      if (string.IsNullOrEmpty(filterText))
      {
        // 如果没有筛选条件，显示所有数据
        _dataGridManager.UpdateDataSource(_allCommits);
        _filteredCommits = new List<CommitInfo>(_allCommits);
        return;
      }

      // 使用SearchFilterManager应用过滤
      _filteredCommits = _searchFilterManager.ApplyFilter(_allCommits, filterText);

      // 更新UI
      _dataGridManager.UpdateDataSource(_filteredCommits);

      // 显示筛选结果
      ShowCustomMessageBox("筛选结果", _searchFilterManager.GetFilterStats(_filteredCommits), false);
    }

    // 显示自定义消息窗口，如果是保存成功消息，则提供打开文件选项
    private void ShowCustomMessageBox(string title, string message, bool isSuccess)
    {
      string filePath = isSuccess ? OutputPathTextBox.Text : null;

      // 如果是成功消息且有文件路径，添加到最近保存位置
      if (isSuccess && !string.IsNullOrEmpty(filePath))
      {
        _locationManager.AddToSaveLocations(filePath);
      }

      _dialogManager.ShowCustomMessageBox(title, message, isSuccess, filePath);
    }

    // 加载最近保存的位置
    private void LoadSaveLocations()
    {
      _locationManager.LoadSaveLocations();
    }

    // 保存最近保存的位置
    private void SaveSaveLocations()
    {
      _locationManager.SaveSaveLocations();
    }

    // 添加路径到最近保存位置列表
    private void AddToSaveLocations(string path)
    {
      _locationManager.AddToSaveLocations(path);
    }

    private void LoadLastPath_Click(object sender, RoutedEventArgs e)
    {
      if (_locationManager.SaveLocations.Count == 0)
      {
        ShowCustomMessageBox("信息", "没有最近的保存位置记录。", false);
        return;
      }

      // 创建一个选择窗口
      var selectWindow = new Window
      {
        Title = "选择最近保存位置",
        Width = 500,
        Height = 300,
        WindowStartupLocation = WindowStartupLocation.CenterOwner,
        Owner = this,
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
            // 如果存在，则将路径的文件名中的日期替换成当前日期
            string newPath = Path.Combine(Path.GetDirectoryName(selectedPath), "GIT提交记录查询结果" + DateTime.Now.ToString("yyyyMMdd") + Path.GetExtension(selectedPath));
            // OutputPathTextBox.Text = Path.Combine(Path.GetDirectoryName(selectedPath), DateTime.Now.ToString("yyyyMMdd") + Path.GetExtension(selectedPath));
            if (File.Exists(newPath))
            {
              ShowCustomMessageBox("提示", "文件已存在，请选择其他保存位置。", false);
              return;
            }
            else
            {
              OutputPathTextBox.Text = newPath;
            }

            // 确保将选择的路径添加到最近保存位置并保存
            AddToSaveLocations(selectedPath);
            SaveSaveLocations();

            selectWindow.Close();
          }
          else
          {
            ShowCustomMessageBox("提示", "文件路径不存在", false);
          }
        }
        else
        {
          ShowCustomMessageBox("提示", "请先选择一个保存位置", false);
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
        ShowCustomMessageBox("信息", "已清除最近保存位置记录。", false);
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

    // [该方法未使用，可以移除] 复选框的勾选事件在BrowseButton_Click方法中已处理
    private void ChooseSystemCheckBox_Checked(object sender, RoutedEventArgs e)
    {
      // 此方法为空，不需要特殊处理，因为在BrowseButton_Click中已经处理了选择逻辑
    }

    // 处理获取本地git作者点击事件
    private async void LocationAuthor_Click(object sender, RoutedEventArgs e)
    {
      // 禁用按钮，防止重复点击
      var button = sender as Button;
      if (button != null) button.IsEnabled = false;

      try
      {
        // 获取路径列表
        string pathsText = PathsTextBox.Text;
        List<string> paths = new List<string>();

        // 如果没有输入路径，弹出提示窗体
        if (string.IsNullOrWhiteSpace(pathsText))
        {
          // 清空临时扫描路径
          _tempScanPath = "";

          bool shouldContinue = ShowGitPathConfirmDialog();
          if (!shouldContinue)
          {
            // 用户选择取消查找
            if (button != null) button.IsEnabled = true;
            return;
          }

          // 检查临时扫描路径是否已设置
          if (string.IsNullOrWhiteSpace(_tempScanPath))
          {
            // 仍然没有路径，取消操作
            if (button != null) button.IsEnabled = true;
            return;
          }

          // 使用_tempScanPath进行扫描
          paths.Add(_tempScanPath);
        }
        else
        {
          // 处理路径
          paths.AddRange(pathsText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
        }

        // 添加一个分隔线，开始新的日志记录
        _outputManager.AddSeparator();

        // 自动查找本地git作者
        UpdateOutput("开始扫描本地Git作者信息...");

        // 使用GitOperationsManager扫描作者
        _isRunning = true;

        // 使用GitOperationsManager异步扫描Git作者
        List<string> authors = await _gitOperationsManager.ScanGitAuthorsAsync(paths);

        // 检查结果
        if (authors.Count > 0)
        {
          // 显示作者选择对话框
          ShowAuthorSelectionDialog(authors);
        }
        else
        {
          ShowCustomMessageBox("提示", "未找到任何Git作者信息", false);
        }
      }
      catch (Exception ex)
      {
        UpdateOutput(string.Format("获取Git作者信息时出错: {0}", ex.Message));
        ShowCustomMessageBox("错误", string.Format("获取Git作者信息时出错: {0}", ex.Message), false);
      }
      finally
      {
        // 恢复按钮状态
        if (button != null) button.IsEnabled = true;
        // 清空临时扫描路径
        _tempScanPath = "";
        _isRunning = false;
      }
    }

    // 显示Git路径确认对话框
    private bool ShowGitPathConfirmDialog()
    {
      bool result = false;

      // 创建确认窗口
      var confirmWindow = new Window
      {
        Title = "未找到Git路径",
        Width = 400,
        Height = 200,
        WindowStartupLocation = WindowStartupLocation.CenterOwner,
        Owner = this,
        ResizeMode = ResizeMode.NoResize,
        Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#f0f0f0"))
      };

      // 创建内容面板
      var grid = new Grid();
      grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
      grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

      // 消息文本
      var messageText = new TextBlock
      {
        Text = "您没有填写Git路径，无法查找Git作者信息。",
        Margin = new Thickness(20, 20, 20, 10),
        TextWrapping = TextWrapping.Wrap,
        VerticalAlignment = VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Center
      };
      Grid.SetRow(messageText, 0);
      grid.Children.Add(messageText);

      // 按钮面板
      var buttonPanel = new StackPanel
      {
        Orientation = Orientation.Horizontal,
        HorizontalAlignment = HorizontalAlignment.Center,
        Margin = new Thickness(0, 10, 0, 20)
      };
      Grid.SetRow(buttonPanel, 1);

      // 选择最近路径按钮
      var selectPathButton = new Button
      {
        Content = "选择最近路径",
        Padding = new Thickness(15, 5, 15, 5),
        Margin = new Thickness(10),
        MinWidth = 120
      };

      selectPathButton.Click += (s, e) =>
      {
        confirmWindow.Close();
        result = ShowRecentPathSelectionDialog();
      };

      // 取消查找按钮
      var cancelButton = new Button
      {
        Content = "取消查找",
        Padding = new Thickness(15, 5, 15, 5),
        Margin = new Thickness(10),
        MinWidth = 100
      };

      cancelButton.Click += (s, e) =>
      {
        result = false;
        confirmWindow.Close();
      };

      buttonPanel.Children.Add(selectPathButton);
      buttonPanel.Children.Add(cancelButton);
      grid.Children.Add(buttonPanel);

      confirmWindow.Content = grid;
      confirmWindow.ShowDialog();

      return result;
    }

    // 显示最近路径选择对话框，带有红色提示信息
    private bool ShowRecentPathSelectionDialog()
    {
      if (_locationManager.RecentLocations.Count == 0)
      {
        ShowCustomMessageBox("信息", "没有最近的位置记录。", false);
        return false;
      }

      bool result = false;
      string selectedPathResult = null;

      // 创建一个选择窗口
      var selectWindow = new Window
      {
        Title = "选择最近路径",
        Width = 500,
        Height = 350,
        WindowStartupLocation = WindowStartupLocation.CenterOwner,
        Owner = this,
        ResizeMode = ResizeMode.NoResize
      };

      var grid = new Grid();
      grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
      grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
      grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

      // 红色提示文本
      var warningText = new TextBlock
      {
        Text = "请选择git项目的根目录避免查询时间太久",
        Margin = new Thickness(10, 10, 10, 5),
        Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red),
        FontWeight = FontWeights.Bold,
        TextWrapping = TextWrapping.Wrap
      };
      Grid.SetRow(warningText, 0);
      grid.Children.Add(warningText);

      // 列表框，显示最近的位置
      var listBox = new ListBox
      {
        Margin = new Thickness(10),
        ItemsSource = _locationManager.RecentLocations
      };
      Grid.SetRow(listBox, 1);
      grid.Children.Add(listBox);

      // 按钮面板
      var buttonPanel = new StackPanel
      {
        Orientation = Orientation.Horizontal,
        HorizontalAlignment = HorizontalAlignment.Center,
        Margin = new Thickness(0, 0, 0, 10)
      };
      Grid.SetRow(buttonPanel, 2);

      // 确认按钮
      var confirmButton = new Button
      {
        Content = "确定",
        Padding = new Thickness(15, 5, 15, 5),
        Margin = new Thickness(5),
        MinWidth = 80
      };

      confirmButton.Click += (s, evt) =>
      {
        if (listBox.SelectedItem is string selectedPath)
        {
          // 不再将选择的路径填入到PathsTextBox，而是仅保存选中的路径
          selectedPathResult = selectedPath;
          result = true;
          selectWindow.Close();
        }
        else
        {
          ShowCustomMessageBox("提示", "请先选择一个位置", false);
        }
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
        result = false;
        selectWindow.Close();
      };

      buttonPanel.Children.Add(confirmButton);
      buttonPanel.Children.Add(cancelButton);
      grid.Children.Add(buttonPanel);

      selectWindow.Content = grid;
      selectWindow.ShowDialog();

      // 如果成功选择了路径，设置临时扫描路径列表
      if (result && !string.IsNullOrEmpty(selectedPathResult))
      {
        _tempScanPath = selectedPathResult;
      }

      return result;
    }

    // 从Git仓库获取作者信息
    private void ScanGitAuthors(DirectoryInfo repo, List<string> authors)
    {
      try
      {
        string currentDirectory = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(repo.FullName);

        UpdateOutput(string.Format("正在从仓库获取作者信息: {0}", repo.FullName));

        // 执行git命令获取所有作者
        var process = new Process
        {
          StartInfo = new ProcessStartInfo
          {
            FileName = "git",
            Arguments = "log --format=\"%an\" --all",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8
          }
        };

        process.Start();
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        // 解析输出，添加作者
        var newAuthors = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(author => !string.IsNullOrWhiteSpace(author))
            .ToList();

        authors.AddRange(newAuthors);

        // 也添加到扫描作者列表
        _authorManager.AddScannedAuthors(newAuthors);

        UpdateOutput(string.Format("从仓库 '{0}' 找到 {1} 个作者", repo.Name, newAuthors.Count));

        // 恢复当前目录
        Directory.SetCurrentDirectory(currentDirectory);
      }
      catch (Exception ex)
      {
        UpdateOutput(string.Format("获取仓库作者时出错: {0}", ex.Message));
      }
    }

    // 显示作者选择对话框
    private void ShowAuthorSelectionDialog(List<string> authors)
    {
      // 创建并显示作者选择窗口
      var authorSelectionWindow = new Views.AuthorSelectionWindow(_authorManager, _dialogManager)
      {
        Owner = this
      };

      // 初始化扫描作者列表，并自动切换到扫描作者标签页
      if (authorSelectionWindow.InitializeFromScanResults(authors))
      {
        if (authorSelectionWindow.ShowDialog() == true && !string.IsNullOrEmpty(authorSelectionWindow.SelectedAuthor))
        {
          AuthorTextBox.Text = authorSelectionWindow.SelectedAuthor;
        }
      }
    }

    // 显示作者选择对话框（从最近作者中选择）
    private void ShowRecentAuthorSelectionDialog()
    {
      // 创建并显示作者选择窗口
      var authorSelectionWindow = new Views.AuthorSelectionWindow(_authorManager, _dialogManager)
      {
        Owner = this
      };

      // 从已有提交中初始化作者列表（如果最近作者列表为空）
      if (authorSelectionWindow.InitializeFromCommits(_allCommits))
      {
        if (authorSelectionWindow.ShowDialog() == true && !string.IsNullOrEmpty(authorSelectionWindow.SelectedAuthor))
        {
          AuthorTextBox.Text = authorSelectionWindow.SelectedAuthor;
        }
      }
    }

    // 复制日志
    private void CopyLog_Click(object sender, RoutedEventArgs e)
    {
      _logOperationsManager.CopyLog(ResultTextBox.Text);
    }

    // 保存日志
    private void SaveLog_Click(object sender, RoutedEventArgs e)
    {
      _logOperationsManager.SaveLog(ResultTextBox.Text);
    }

    // 清空日志
    private void CleanLog_Click(object sender, RoutedEventArgs e)
    {
      _logOperationsManager.CleanLog();
    }

    // 停止查询
    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
      if (_queryExecutionManager.IsRunning)
      {
        _queryExecutionManager.IsRunning = false;
        StartButton.IsEnabled = true;
        StopButton.IsEnabled = false;
        _outputManager.UpdateOutput("===== 查询已手动停止 =====");
        _outputManager.HideProgressBar();
        ShowCustomMessageBox("提示", "查询已手动停止", true);

        // 同步状态
        _isRunning = _queryExecutionManager.IsRunning;
      }
      else
      {
        ShowCustomMessageBox("提示", "当前没有正在进行的查询", false);
      }
    }

    // 最近使用
    private void LastAuthor_Click(object sender, RoutedEventArgs e)
    {
      // 显示最近作者选择对话框
      ShowRecentAuthorSelectionDialog();

      // 如果作者文本框有内容，应用筛选
      if (!string.IsNullOrEmpty(AuthorTextBox.Text))
      {
        ApplyFilter();
      }
    }
  }
}