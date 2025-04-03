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
using UtilsDataGridManager = GitCommitsWPF.Utils.DataGridManager;
using ServicesDataGridManager = GitCommitsWPF.Services.DataGridManager;

namespace GitCommitsWPF
{
  public partial class MainWindow : Window
  {
    private List<CommitInfo> _allCommits = new List<CommitInfo>();
    private int _repoCount = 0;
    private int _currentRepo = 0;
    private bool _isRunning = false;
    private List<CommitInfo> _filteredCommits = new List<CommitInfo>(); // 添加筛选后的提交列表

    // AuthorManager实例，用于管理作者相关功能
    private AuthorManager _authorManager = new AuthorManager();

    // LocationManager实例，用于管理位置相关功能
    private LocationManager _locationManager;

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
    private UtilsDataGridManager _dataGridManager;

    // QueryExecutionManager实例，用于管理查询执行和结果处理
    private QueryExecutionManager _queryExecutionManager;

    // PathBrowserManager实例，用于管理路径选择和位置管理的UI交互
    private PathBrowserManager _pathBrowserManager;

    // AuthorDialogManager实例，用于管理作者选择对话框相关功能
    private AuthorDialogManager _authorDialogManager;

    // TimeRangeManager实例，用于管理时间范围选择相关功能
    private TimeRangeManager _timeRangeManager = new TimeRangeManager();

    // FieldSelectionManager实例，用于管理字段选择相关功能
    private FieldSelectionManager _fieldSelectionManager = new FieldSelectionManager();

    // StatisticsOptionsManager实例，用于管理统计选项相关功能
    private StatisticsOptionsManager _statisticsOptionsManager = new StatisticsOptionsManager();

    // RepositorySelectionManager实例，用于管理仓库选择相关功能
    private RepositorySelectionManager _repositorySelectionManager;

    // ClipboardManager实例，用于管理剪贴板相关功能
    private ClipboardManager _clipboardManager;

    // 用于防止TextChanged事件循环触发
    private DispatcherTimer _pathsTextChangedTimer;
    private bool _isPathsTextBeingProcessed = false;

    public MainWindow()
    {
      InitializeComponent();

      // 设置默认值
      FormatTextBox.Text = "{Repository} : {Message}";

      // 初始化输出管理器
      _outputManager = new OutputManager(ResultTextBox, ProgressBar, Dispatcher);

      // 初始化对话框管理器
      _dialogManager = new DialogManager(this);

      // 初始化LocationManager
      _locationManager = new LocationManager();

      // 初始化日志操作管理器
      _logOperationsManager = new LogOperationsManager(_dialogManager, _outputManager);

      // 初始化导出管理器
      _exportManager = new ExportManager(_dialogManager);

      // 初始化Git操作管理器
      _gitOperationsManager = new GitOperationsManager(_outputManager, _dialogManager, _authorManager);

      // 初始化剪贴板管理器
      _clipboardManager = new ClipboardManager(_dialogManager);

      // 初始化仓库选择管理器
      _repositorySelectionManager = new RepositorySelectionManager(
        _dialogManager,
        _locationManager,
        _gitOperationsManager,
        _outputManager);

      // 初始化路径浏览管理器
      _pathBrowserManager = new PathBrowserManager(
        this,
        _dialogManager,
        _locationManager,
        _gitOperationsManager,
        _outputManager);

      // 设置PathBrowserManager对RepositorySelectionManager的引用
      _pathBrowserManager.SetRepositorySelectionManager(_repositorySelectionManager);

      // 初始化PathBrowserManager
      _pathBrowserManager.Initialize(
        PathsTextBox,
        VerifyGitPathsCheckBox,
        ChooseSystemCheckBox);

      // 初始化仓库选择管理器
      _repositorySelectionManager.Initialize(
        PathsTextBox,
        VerifyGitPathsCheckBox,
        ChooseSystemCheckBox,
        _pathBrowserManager);

      // 初始化DataGrid管理器
      _dataGridManager = new UtilsDataGridManager(_dialogManager);
      _dataGridManager.Initialize(CommitsDataGrid,
        (title, message, isSuccess, action) => _dialogManager.ShowCustomMessageBox(title, message, isSuccess, action),
        () => FormatTextBox.Text);

      // 初始化查询执行管理器 - 使用Services命名空间的DataGridManager进行类型转换
      ServicesDataGridManager servicesDataGridManager = new ServicesDataGridManager(_dialogManager);
      servicesDataGridManager.Initialize(CommitsDataGrid,
        (title, message, isSuccess) => _dialogManager.ShowCustomMessageBox(title, message, isSuccess, (string)null),
        () => FormatTextBox.Text);

      _queryExecutionManager = new QueryExecutionManager(
        _outputManager,
        _gitOperationsManager,
        _dialogManager,
        _statisticsManager,
        _formattingManager,
        servicesDataGridManager);

      // 初始化作者对话框管理器
      _authorDialogManager = new AuthorDialogManager(
        this,
        _authorManager,
        _dialogManager,
        _gitOperationsManager);

      // 初始化时间范围管理器
      _timeRangeManager.Initialize(TimeRangeComboBox, StartDatePicker, EndDatePicker);

      // 初始化字段选择管理器
      _fieldSelectionManager.Initialize(
        RepositoryFieldCheckBox,
        RepoPathFieldCheckBox,
        RepoFolderFieldCheckBox,
        CommitIdFieldCheckBox,
        AuthorFieldCheckBox,
        DateFieldCheckBox,
        MessageFieldCheckBox);

      // 初始化统计选项管理器
      _statisticsOptionsManager.Initialize(
        EnableStatsCheckBox,
        StatsByAuthorCheckBox,
        StatsByRepoCheckBox,
        StatsByDateCheckBox);

      // 监听作者文本框变化
      AuthorTextBox.TextChanged += (s, e) =>
      {
        if (!string.IsNullOrWhiteSpace(AuthorTextBox.Text))
        {
          _authorManager.AddToRecentAuthors(AuthorTextBox.Text);
        }
      };

      // 监听路径文本框变化，检查并移除重复路径
      PathsTextBox.TextChanged += (s, e) =>
      {
        // 如果正在处理文本变化，跳过
        if (_isPathsTextBeingProcessed)
          return;

        // 取消之前的计时器（如果存在）
        if (_pathsTextChangedTimer != null)
        {
          _pathsTextChangedTimer.Stop();
        }

        // 创建新的计时器
        _pathsTextChangedTimer = new DispatcherTimer();
        _pathsTextChangedTimer.Interval = TimeSpan.FromMilliseconds(1000); // 1秒延迟
        _pathsTextChangedTimer.Tick += (sender, args) =>
        {
          _pathsTextChangedTimer.Stop();

          // 设置标志位，防止循环调用
          _isPathsTextBeingProcessed = true;

          try
          {
            _repositorySelectionManager.CheckAndRemoveDuplicatePaths();
          }
          finally
          {
            // 恢复标志位
            _isPathsTextBeingProcessed = false;
          }
        };
        _pathsTextChangedTimer.Start();
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
      return _repositorySelectionManager.ValidatePath(path);
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
      _pathBrowserManager.BrowseFolder();
    }

    private void OutputPathButton_Click(object sender, RoutedEventArgs e)
    {
      _pathBrowserManager.SelectOutputPath(OutputPathTextBox);
    }

    // 开始按钮点击事件处理
    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
      // 设置执行状态并获取所需参数
      string pathsText = PathsTextBox.Text;
      string timeRange = _timeRangeManager.GetSelectedTimeRange();
      DateTime? startDate = _timeRangeManager.GetStartDate();
      DateTime? endDate = _timeRangeManager.GetEndDate();
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
        () => _fieldSelectionManager.GetSelectedFields(),
        // 获取格式文本
        () => FormatTextBox.Text,
        // 获取是否显示重复的仓库名
        () => ShowRepeatedRepoNamesCheckBox.IsChecked == true,
        // 获取是否启用统计
        () => _statisticsOptionsManager.IsStatsEnabled(),
        // 获取按作者统计
        () => _statisticsOptionsManager.IsStatsByAuthorEnabled(),
        // 获取按仓库统计
        () => _statisticsOptionsManager.IsStatsByRepoEnabled(),
        // 获取按日期统计
        () => _statisticsOptionsManager.IsStatsByDateEnabled(),
        // 更新格式化结果文本框
        (textBox) =>
        {
          var formattedResultTextBox = this.FindName("FormattedResultTextBox") as System.Windows.Controls.TextBox;
          if (formattedResultTextBox != null)
          {
            formattedResultTextBox.Text = textBox.Text;
          }
        },
        // 禁用开始按钮
        () => StartButton.IsEnabled = false,
        // 启用停止按钮
        () => StopButton.IsEnabled = true,
        // 禁用停止按钮
        () => StopButton.IsEnabled = false,
        // 启用开始按钮
        () => StartButton.IsEnabled = true,
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
            _statisticsOptionsManager.IsStatsByAuthorEnabled(),
            _statisticsOptionsManager.IsStatsByRepoEnabled(),
            _statisticsOptionsManager.IsStatsByDateEnabled());
        string statsOutput = statsBuilder.ToString();

        // 获取用户自定义格式模板
        string formatTemplate = FormatTextBox.Text;

        // 获取是否包含统计信息
        bool includeStats = _statisticsOptionsManager.IsStatsEnabled();

        // 获取选择的字段 - 使用FieldSelectionManager
        List<string> selectedFields = _fieldSelectionManager.GetSelectedFields();

        // 使用ClipboardManager复制结果到剪贴板
        _clipboardManager.CopyResultToClipboard(_filteredCommits, statsOutput, includeStats, formatTemplate, selectedFields);
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
            _statisticsOptionsManager.IsStatsByAuthorEnabled(),
            _statisticsOptionsManager.IsStatsByRepoEnabled(),
            _statisticsOptionsManager.IsStatsByDateEnabled());
        string statsOutput = statsBuilder.ToString();

        // 获取用户自定义格式模板
        string formatTemplate = FormatTextBox.Text;

        // 获取是否包含统计信息
        bool includeStats = _statisticsOptionsManager.IsStatsEnabled();

        // 获取选择的字段 - 使用FieldSelectionManager
        List<string> selectedFields = _fieldSelectionManager.GetSelectedFields();

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
        // 获取选择的字段 - 使用FieldSelectionManager
        List<string> selectedFields = _fieldSelectionManager.GetSelectedFields();

        // 生成统计数据(如果启用)
        var statsOutput = new StringBuilder();
        bool enableStats = _statisticsOptionsManager.IsStatsEnabled();

        if (enableStats)
        {
          statsOutput.AppendLine("\n======== 提交统计 ========\n");

          // 使用StatisticsManager生成统计信息
          _statisticsManager.GenerateStats(
              _allCommits,
              statsOutput,
              _statisticsOptionsManager.IsStatsByAuthorEnabled(),
              _statisticsOptionsManager.IsStatsByRepoEnabled(),
              _statisticsOptionsManager.IsStatsByDateEnabled());

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
      _clipboardManager.ShowCopySuccessMessageBox(title, message);
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
      _pathBrowserManager.ShowRecentLocationsDialog(selectedPath =>
      {
        if (!string.IsNullOrEmpty(PathsTextBox.Text))
        {
          PathsTextBox.Text += Environment.NewLine;
        }
        PathsTextBox.Text += selectedPath;
      });
    }

    private void ClearPaths_Click(object sender, RoutedEventArgs e)
    {
      _repositorySelectionManager.ClearPaths();
    }

    // 用于确认操作的自定义对话框
    private bool ShowCustomConfirmDialog(string title, string message)
    {
      return _dialogManager.ShowCustomConfirmDialog(title, message);
    }

    // 表格上下文菜单事件处理
    private void CopySelectedRows_Click(object sender, RoutedEventArgs e)
    {
      // 获取选中的行
      var selectedCommits = _dataGridManager.GetSelectedCommits();

      // 获取格式模板
      string formatTemplate = FormatTextBox.Text;

      // 使用ClipboardManager复制选中的行
      _clipboardManager.CopySelectedRows(selectedCommits, formatTemplate);
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
      _dialogManager.ShowCustomMessageBox("筛选结果", _searchFilterManager.GetFilterStats(_filteredCommits), false);
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
      _pathBrowserManager.ShowRecentSaveLocationsDialog(OutputPathTextBox);
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
        // 使用RepositorySelectionManager获取路径列表
        List<string> paths = _repositorySelectionManager.GetPathsList();

        // 如果没有输入路径，弹出提示窗体
        if (paths.Count == 0)
        {
          // 清空临时扫描路径
          _repositorySelectionManager.ClearTempScanPath();

          bool shouldContinue = _repositorySelectionManager.ShowGitPathConfirmDialog();
          if (!shouldContinue)
          {
            // 用户选择取消查找
            if (button != null) button.IsEnabled = true;
            return;
          }

          // 检查临时扫描路径是否已设置
          if (string.IsNullOrWhiteSpace(_repositorySelectionManager.TempScanPath))
          {
            // 仍然没有路径，取消操作
            if (button != null) button.IsEnabled = true;
            return;
          }

          // 使用临时扫描路径进行扫描
          paths.Add(_repositorySelectionManager.TempScanPath);
        }

        // 添加一个分隔线，开始新的日志记录
        _outputManager.AddSeparator();

        // 自动查找本地git作者
        UpdateOutput("开始扫描本地Git作者信息...");

        // 使用GitOperationsManager扫描作者
        _isRunning = true;

        // 使用AuthorDialogManager扫描并显示作者选择对话框
        string selectedAuthor = await _authorDialogManager.ScanAndShowAuthorSelectionDialogAsync(paths);

        // 如果选择了作者，更新作者文本框
        if (!string.IsNullOrEmpty(selectedAuthor))
        {
          AuthorTextBox.Text = selectedAuthor;
        }
      }
      catch (Exception ex)
      {
        UpdateOutput(string.Format("获取Git作者信息时出错: {0}", ex.Message));
        _dialogManager.ShowCustomMessageBox("错误", string.Format("获取Git作者信息时出错: {0}", ex.Message), false);
      }
      finally
      {
        // 恢复按钮状态
        if (button != null) button.IsEnabled = true;
        // 清空临时扫描路径
        _repositorySelectionManager.ClearTempScanPath();
        _isRunning = false;
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
        _dialogManager.ShowCustomMessageBox("提示", "查询已手动停止", true);

        // 同步状态
        _isRunning = _queryExecutionManager.IsRunning;
      }
      else
      {
        _dialogManager.ShowCustomMessageBox("提示", "当前没有正在进行的查询", false);
      }
    }

    // 最近使用
    private void LastAuthor_Click(object sender, RoutedEventArgs e)
    {
      // 使用AuthorDialogManager显示最近作者选择对话框
      string selectedAuthor = _authorDialogManager.ShowRecentAuthorSelectionDialog(_allCommits);

      // 如果选择了作者，更新作者文本框
      if (!string.IsNullOrEmpty(selectedAuthor))
      {
        AuthorTextBox.Text = selectedAuthor;

        // 如果作者文本框有内容，应用筛选
        ApplyFilter();
      }
    }
  }
}