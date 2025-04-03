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

      // 初始化表格视图
      ConfigureDataGrid();

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
    }

    // 配置DataGrid的属性和行为
    private void ConfigureDataGrid()
    {
      // 设置DataGrid的排序行为
      CommitsDataGrid.Sorting += (s, e) =>
      {
        // 可以在这里添加自定义排序逻辑
        e.Handled = false; // 使用默认排序行为
      };

      // 设置DataGrid的选择模式
      CommitsDataGrid.SelectionMode = DataGridSelectionMode.Extended;
      CommitsDataGrid.SelectionUnit = DataGridSelectionUnit.FullRow;

      // 双击行时查看详细信息
      CommitsDataGrid.MouseDoubleClick += (s, e) =>
      {
        if (CommitsDataGrid.SelectedItem is CommitInfo selectedCommit)
        {
          var details = new StringBuilder();
          details.AppendLine("提交详情：");
          details.AppendLine(string.Format("仓库: {0}", selectedCommit.Repository));
          details.AppendLine(string.Format("仓库路径: {0}", selectedCommit.RepoPath));
          details.AppendLine(string.Format("提交ID: {0}", selectedCommit.CommitId));
          details.AppendLine(string.Format("作者: {0}", selectedCommit.Author));
          details.AppendLine(string.Format("日期: {0}", selectedCommit.Date));
          details.AppendLine(string.Format("消息: {0}", selectedCommit.Message));

          ShowCustomMessageBox("提交详情", details.ToString(), false);
        }
      };
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
      // 检查路径是否为空
      if (string.IsNullOrWhiteSpace(PathsTextBox.Text))
      {
        ShowCustomMessageBox("错误", "请先输入要扫描的Git仓库路径", false);
        return;
      }

      // 清空现有的结果
      _allCommits.Clear();
      _filteredCommits.Clear();
      _isRunning = true;

      // 禁用开始按钮，启用停止按钮
      StartButton.IsEnabled = false;
      StopButton.IsEnabled = true;

      try
      {
        // 先添加一个分隔线
        _outputManager.AddSeparator();

        // 异步执行查询
        await CollectGitCommits();

        // 查询完成后，显示结果并更新UI
        ShowResults();

        // 更新完成消息
        SaveButton.IsEnabled = true;
        int commitCount = _allCommits.Count;
        string commitText = commitCount == 1 ? "条提交记录" : "条提交记录";
        UpdateOutput(string.Format("===== 扫描完成，找到 {0} {1} =====，点击结果页签查看", commitCount, commitText));
      }
      catch (Exception ex)
      {
        ShowCustomMessageBox("错误", string.Format("执行过程中发生错误：{0}", ex.Message), false);
      }
      finally
      {
        _isRunning = false;
        StartButton.IsEnabled = true;
        StopButton.IsEnabled = false;
        _outputManager.HideProgressBar();
      }
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
        AddToSaveLocations(Path.GetDirectoryName(filePath));

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
        GenerateStats(statsBuilder);
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
        GenerateStats(statsBuilder);
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
        List<string> selectedFields = new List<string>();
        if (RepositoryFieldCheckBox.IsChecked == true) selectedFields.Add("Repository");
        if (RepoPathFieldCheckBox.IsChecked == true) selectedFields.Add("RepoPath");
        if (RepoFolderFieldCheckBox.IsChecked == true) selectedFields.Add("RepoFolder");
        if (CommitIdFieldCheckBox.IsChecked == true) selectedFields.Add("CommitId");
        if (AuthorFieldCheckBox.IsChecked == true) selectedFields.Add("Author");
        if (DateFieldCheckBox.IsChecked == true) selectedFields.Add("Date");
        if (MessageFieldCheckBox.IsChecked == true) selectedFields.Add("Message");

        // 生成统计数据(如果启用)
        var statsOutput = new StringBuilder();
        if (EnableStatsCheckBox.IsChecked == true)
        {
          statsOutput.AppendLine("\n======== 提交统计 ========\n");
          GenerateStats(statsOutput);
          statsOutput.AppendLine("\n==========================\n");
        }

        string formattedContent = string.Empty;

        // 应用自定义格式
        string format = FormatTextBox.Text;
        if (!string.IsNullOrEmpty(format))
        {
          var formattedOutput = new StringBuilder();
          var displayedRepos = new Dictionary<string, bool>();
          bool showRepeatedRepoNames = ShowRepeatedRepoNamesCheckBox.IsChecked == true;

          foreach (var commit in _allCommits)
          {
            string line = format;

            // 获取当前提交的仓库标识符
            string repoKey = !string.IsNullOrEmpty(commit.RepoFolder) ? commit.RepoFolder : commit.Repository;

            // 替换所有占位符
            line = line.Replace("{Repository}",
                (!showRepeatedRepoNames && displayedRepos.ContainsKey(repoKey)) ?
                new string(' ', repoKey.Length) : commit.Repository);

            line = line.Replace("{RepoPath}", commit.RepoPath);

            line = line.Replace("{RepoFolder}",
                (!showRepeatedRepoNames && displayedRepos.ContainsKey(repoKey)) ?
                new string(' ', repoKey.Length) : commit.RepoFolder);

            line = line.Replace("{CommitId}", commit.CommitId ?? "");
            line = line.Replace("{Author}", commit.Author ?? "");
            line = line.Replace("{Date}", commit.Date ?? "");
            line = line.Replace("{Message}", commit.Message ?? "");

            formattedOutput.AppendLine(line);

            // 标记此仓库已显示
            if (!displayedRepos.ContainsKey(repoKey))
            {
              displayedRepos[repoKey] = true;
            }
          }

          // 合并统计和格式化输出
          formattedContent = statsOutput.ToString() + formattedOutput.ToString();
        }
        else
        {
          // 如果没有指定格式，使用JSON格式
          var filteredCommits = new List<CommitInfo>();
          foreach (var commit in _allCommits)
          {
            var filteredCommit = new CommitInfo();

            if (selectedFields.Contains("Repository")) filteredCommit.Repository = commit.Repository;
            if (selectedFields.Contains("RepoPath")) filteredCommit.RepoPath = commit.RepoPath;
            if (selectedFields.Contains("RepoFolder")) filteredCommit.RepoFolder = commit.RepoFolder;
            if (selectedFields.Contains("CommitId")) filteredCommit.CommitId = commit.CommitId;
            if (selectedFields.Contains("Author")) filteredCommit.Author = commit.Author;
            if (selectedFields.Contains("Date")) filteredCommit.Date = commit.Date;
            if (selectedFields.Contains("Message")) filteredCommit.Message = commit.Message;

            filteredCommits.Add(filteredCommit);
          }

          // 使用JSON格式
          string jsonOutput = JsonConvert.SerializeObject(filteredCommits, Newtonsoft.Json.Formatting.Indented);
          formattedContent = statsOutput.ToString() + Environment.NewLine + jsonOutput;
        }

        formattedResultTextBox.Text = formattedContent;
      }
    }

    // 显示复制成功的消息框（不显示文件相关按钮）
    private void ShowCopySuccessMessageBox(string title, string message)
    {
      _dialogManager.ShowCopySuccessMessageBox(title, message);
    }

    private async Task CollectGitCommits()
    {
      // 获取路径列表（从UI线程读取文本）
      string pathsText = "";
      bool verifyGitPaths = false;
      Dispatcher.Invoke(() =>
      {
        pathsText = PathsTextBox.Text;
        verifyGitPaths = VerifyGitPathsCheckBox.IsChecked == true;
      });
      var paths = pathsText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList();

      // 计算日期范围
      string since = "";
      string until = DateTime.Now.AddDays(1).ToString("yyyy-MM-dd"); // 设置到明天来包含今天的提交

      var timeRange = "";
      Dispatcher.Invoke(() =>
      {
        timeRange = ((ComboBoxItem)TimeRangeComboBox.SelectedItem).Tag.ToString();
      });

      switch (timeRange)
      {
        case "day":
          since = DateTime.Today.ToString("yyyy-MM-dd");
          break;
        case "week":
          // 找到本周的星期一
          var startOfWeek = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek + 1);
          if (startOfWeek.DayOfWeek == DayOfWeek.Sunday) // 如果是周日，回退到上周一
            startOfWeek = startOfWeek.AddDays(-6);
          since = startOfWeek.ToString("yyyy-MM-dd");
          break;
        case "month":
          // 本月第一天
          since = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).ToString("yyyy-MM-dd");
          break;
        case "custom":
          DateTime? startDate = null;
          DateTime? endDate = null;
          Dispatcher.Invoke(() =>
          {
            startDate = StartDatePicker.SelectedDate;
            endDate = EndDatePicker.SelectedDate;
            if (startDate.HasValue && endDate.HasValue)
            {
              since = startDate.Value.ToString("yyyy-MM-dd");
              // 设置结束日期为第二天，以包含结束当天的提交
              until = endDate.Value.AddDays(1).ToString("yyyy-MM-dd");
            }
          });
          break;
        case "all":
          // 不设置since，从仓库创建开始
          since = "";
          break;
      }

      // 获取作者和作者过滤条件
      string author = "";
      string authorFilter = "";
      Dispatcher.Invoke(() =>
      {
        author = AuthorTextBox.Text;
        authorFilter = AuthorFilterTextBox.Text;
      });

      // 使用GitOperationsManager异步收集Git提交
      _allCommits = await _gitOperationsManager.CollectGitCommitsAsync(paths, since, until, author, authorFilter, verifyGitPaths);
    }

    // 删除FindGitRepositories方法，使用GitOperationsManager替代
    private List<DirectoryInfo> FindGitRepositories(string path)
    {
      try
      {
        if (!Directory.Exists(path))
        {
          UpdateOutput(string.Format("路径不存在: {0}", path));
          return new List<DirectoryInfo>();
        }

        // 搜索包含.git目录的文件夹
        var gitDirs = new DirectoryInfo(path).GetDirectories(".git", SearchOption.AllDirectories);

        // 返回包含.git目录的父目录（即Git仓库根目录）
        var gitRepos = gitDirs.Select(gitDir => gitDir.Parent)
            .Where(parent => parent != null)
            .ToList();

        UpdateOutput(string.Format("在路径 '{0}' 下找到 {1} 个Git仓库", path, gitRepos.Count));
        return gitRepos;
      }
      catch (Exception ex)
      {
        UpdateOutput(string.Format("搜索Git仓库时出错: {0}", ex.Message));
        return new List<DirectoryInfo>();
      }
    }

    private void ShowResults()
    {
      // 检查是否已手动停止
      if (!_isRunning)
      {
        UpdateOutput("显示结果过程已手动停止");
        return;
      }

      // 筛选后得到的提交记录
      _filteredCommits = new List<CommitInfo>(_allCommits);

      if (_allCommits.Count == 0)
      {
        Dispatcher.Invoke(() =>
        {
          ResultTextBox.Text = _outputManager.OutputContent;
          var formattedResultTextBox = this.FindName("FormattedResultTextBox") as System.Windows.Controls.TextBox;
          if (formattedResultTextBox != null)
          {
            formattedResultTextBox.Text = string.Empty; // 清空结果页签的内容
          }
          CommitsDataGrid.ItemsSource = null; // 清空表格数据
          _filteredCommits.Clear(); // 清空筛选结果
        });
        return;
      }

      // 获取选择的字段
      List<string> selectedFields = new List<string>();
      Dispatcher.Invoke(() =>
      {
        if (RepositoryFieldCheckBox.IsChecked == true) selectedFields.Add("Repository");
        if (RepoPathFieldCheckBox.IsChecked == true) selectedFields.Add("RepoPath");
        if (RepoFolderFieldCheckBox.IsChecked == true) selectedFields.Add("RepoFolder");
        if (CommitIdFieldCheckBox.IsChecked == true) selectedFields.Add("CommitId");
        if (AuthorFieldCheckBox.IsChecked == true) selectedFields.Add("Author");
        if (DateFieldCheckBox.IsChecked == true) selectedFields.Add("Date");
        if (MessageFieldCheckBox.IsChecked == true) selectedFields.Add("Message");

        // 设置DataGrid的数据源
        CommitsDataGrid.ItemsSource = _allCommits;
        _filteredCommits = new List<CommitInfo>(_allCommits); // 重置筛选结果
        SearchFilterTextBox.Clear(); // 清空搜索框
      });

      // 生成统计数据(如果启用)
      var statsOutput = new StringBuilder();
      bool enableStats = false;
      Dispatcher.Invoke(() =>
      {
        enableStats = EnableStatsCheckBox.IsChecked == true;
      });

      if (enableStats)
      {
        statsOutput.AppendLine("\n======== 提交统计 ========\n");
        GenerateStats(statsOutput);
        statsOutput.AppendLine("\n==========================\n");
      }

      // 应用自定义格式
      string format = "";
      Dispatcher.Invoke(() =>
      {
        format = FormatTextBox.Text;
      });

      // 用于保存格式化后的输出内容
      string formattedContent = string.Empty;

      if (!string.IsNullOrEmpty(format))
      {
        var formattedOutput = new StringBuilder();
        var displayedRepos = new Dictionary<string, bool>();
        bool showRepeatedRepoNames = false;

        Dispatcher.Invoke(() =>
        {
          showRepeatedRepoNames = ShowRepeatedRepoNamesCheckBox.IsChecked == true;
        });

        foreach (var commit in _allCommits)
        {
          string line = format;

          // 获取当前提交的仓库标识符
          string repoKey = !string.IsNullOrEmpty(commit.RepoFolder) ? commit.RepoFolder : commit.Repository;

          // 替换所有占位符
          line = line.Replace("{Repository}",
              (!showRepeatedRepoNames && displayedRepos.ContainsKey(repoKey)) ?
              new string(' ', repoKey.Length) : commit.Repository);

          line = line.Replace("{RepoPath}", commit.RepoPath);

          line = line.Replace("{RepoFolder}",
              (!showRepeatedRepoNames && displayedRepos.ContainsKey(repoKey)) ?
              new string(' ', repoKey.Length) : commit.RepoFolder);

          line = line.Replace("{CommitId}", commit.CommitId ?? "");
          line = line.Replace("{Author}", commit.Author ?? "");
          line = line.Replace("{Date}", commit.Date ?? "");
          line = line.Replace("{Message}", commit.Message ?? "");

          formattedOutput.AppendLine(line);

          // 标记此仓库已显示
          if (!displayedRepos.ContainsKey(repoKey))
          {
            displayedRepos[repoKey] = true;
          }
        }

        // 合并统计和格式化输出
        formattedContent = statsOutput.ToString() + formattedOutput.ToString();

        Dispatcher.Invoke(() =>
        {
          ResultTextBox.Text = _outputManager.OutputContent + Environment.NewLine + formattedContent;
          // 在"结果"页签中仅显示格式化的结果，不包含日志输出
          var formattedResultTextBox = this.FindName("FormattedResultTextBox") as System.Windows.Controls.TextBox;
          if (formattedResultTextBox != null)
          {
            formattedResultTextBox.Text = formattedContent;
          }
        });
      }
      else
      {
        // 如果没有指定格式，显示所有字段
        var filteredCommits = new List<CommitInfo>();
        foreach (var commit in _allCommits)
        {
          var filteredCommit = new CommitInfo();

          if (selectedFields.Contains("Repository")) filteredCommit.Repository = commit.Repository;
          if (selectedFields.Contains("RepoPath")) filteredCommit.RepoPath = commit.RepoPath;
          if (selectedFields.Contains("RepoFolder")) filteredCommit.RepoFolder = commit.RepoFolder;
          if (selectedFields.Contains("CommitId")) filteredCommit.CommitId = commit.CommitId;
          if (selectedFields.Contains("Author")) filteredCommit.Author = commit.Author;
          if (selectedFields.Contains("Date")) filteredCommit.Date = commit.Date;
          if (selectedFields.Contains("Message")) filteredCommit.Message = commit.Message;

          filteredCommits.Add(filteredCommit);
        }

        // 使用JSON格式
        string jsonOutput = JsonConvert.SerializeObject(filteredCommits, Newtonsoft.Json.Formatting.Indented);
        formattedContent = statsOutput.ToString() + Environment.NewLine + jsonOutput;

        Dispatcher.Invoke(() =>
        {
          ResultTextBox.Text = _outputManager.OutputContent + Environment.NewLine + formattedContent;
          // 在"结果"页签中仅显示格式化的结果，不包含日志输出
          var formattedResultTextBox = this.FindName("FormattedResultTextBox") as System.Windows.Controls.TextBox;
          if (formattedResultTextBox != null)
          {
            formattedResultTextBox.Text = formattedContent;
          }
        });
      }
    }

    // 生成统计数据并添加到输出字符串
    private void GenerateStats(StringBuilder output)
    {
      // 检查是否有提交记录可以统计
      if (_allCommits == null || _allCommits.Count == 0)
      {
        output.AppendLine("没有找到可以统计的提交记录。");
        return;
      }

      // 检查是否启用了不同的统计方式
      bool statsByAuthor = StatsByAuthorCheckBox.IsChecked == true;
      bool statsByRepo = StatsByRepoCheckBox.IsChecked == true;
      bool statsByDate = StatsByDateCheckBox.IsChecked == true;

      // 1. 按作者统计
      if (statsByAuthor)
      {
        output.AppendLine("【按作者统计提交数量】");
        var authorStats = _allCommits
            .GroupBy(commit => commit.Author ?? "未知作者")
            .Select(group => new { Author = group.Key, Count = group.Count() })
            .OrderByDescending(item => item.Count)
            .ToList();

        if (authorStats.Count > 0)
        {
          // 计算最大宽度
          int maxAuthorWidth = Math.Max("作者".Length, authorStats.Max(s => s.Author.Length));
          int maxCountWidth = Math.Max("提交数".Length, authorStats.Max(s => s.Count.ToString().Length));

          // 输出表头
          output.AppendLine(string.Format("{0} | {1}",
              "作者".PadRight(maxAuthorWidth),
              "提交数".PadLeft(maxCountWidth)));
          output.AppendLine(string.Format("{0}-+-{1}",
              new string('-', maxAuthorWidth),
              new string('-', maxCountWidth)));

          // 输出数据行
          foreach (var stat in authorStats)
          {
            output.AppendLine(string.Format("{0} | {1}",
                stat.Author.PadRight(maxAuthorWidth),
                stat.Count.ToString().PadLeft(maxCountWidth)));
          }
        }
        else
        {
          output.AppendLine("没有作者数据可以统计。");
        }

        output.AppendLine();
      }

      // 2. 按仓库统计
      if (statsByRepo)
      {
        output.AppendLine("【按仓库统计提交数量】");
        var repoStats = _allCommits
            .GroupBy(commit => !string.IsNullOrEmpty(commit.RepoFolder) ? commit.RepoFolder : commit.Repository)
            .Select(group => new { Repo = group.Key, Count = group.Count() })
            .OrderByDescending(item => item.Count)
            .ToList();

        if (repoStats.Count > 0)
        {
          // 计算最大宽度
          int maxRepoWidth = Math.Max("仓库".Length, repoStats.Max(s => s.Repo.Length));
          int maxCountWidth = Math.Max("提交数".Length, repoStats.Max(s => s.Count.ToString().Length));

          // 输出表头
          output.AppendLine(string.Format("{0} | {1}",
              "仓库".PadRight(maxRepoWidth),
              "提交数".PadLeft(maxCountWidth)));
          output.AppendLine(string.Format("{0}-+-{1}",
              new string('-', maxRepoWidth),
              new string('-', maxCountWidth)));

          // 输出数据行
          foreach (var stat in repoStats)
          {
            output.AppendLine(string.Format("{0} | {1}",
                stat.Repo.PadRight(maxRepoWidth),
                stat.Count.ToString().PadLeft(maxCountWidth)));
          }
        }
        else
        {
          output.AppendLine("没有仓库数据可以统计。");
        }

        output.AppendLine();
      }

      // 3. 按日期统计
      if (statsByDate)
      {
        output.AppendLine("【按日期统计提交数量】");
        var dateStats = _allCommits
            .Select(commit => new
            {
              Date = string.IsNullOrEmpty(commit.Date) ?
                    DateTime.Now.ToString("yyyy-MM-dd") :
                    GetFormattedDate(commit.Date)
            })
            .GroupBy(item => item.Date)
            .Select(group => new { Date = group.Key, Count = group.Count() })
            .OrderByDescending(item => item.Date)
            .ToList();

        if (dateStats.Count > 0)
        {
          // 计算最大宽度
          int maxDateWidth = Math.Max("日期".Length, dateStats.Max(s => s.Date.Length));
          int maxCountWidth = Math.Max("提交数".Length, dateStats.Max(s => s.Count.ToString().Length));

          // 输出表头
          output.AppendLine(string.Format("{0} | {1}",
              "日期".PadRight(maxDateWidth),
              "提交数".PadLeft(maxCountWidth)));
          output.AppendLine(string.Format("{0}-+-{1}",
              new string('-', maxDateWidth),
              new string('-', maxCountWidth)));

          // 输出数据行
          foreach (var stat in dateStats)
          {
            output.AppendLine(string.Format("{0} | {1}",
                stat.Date.PadRight(maxDateWidth),
                stat.Count.ToString().PadLeft(maxCountWidth)));
          }
        }
        else
        {
          output.AppendLine("没有日期数据可以统计。");
        }
      }
    }

    // 辅助方法：安全地解析日期字符串
    private string GetFormattedDate(string dateString)
    {
      try
      {
        return DateTime.Parse(dateString).ToString("yyyy-MM-dd");
      }
      catch (Exception)
      {
        // 如果无法解析日期，返回原始字符串或特定标记
        return "未知日期";
      }
    }

    private void SaveAsTextWithStats(string path, List<Dictionary<string, string>> commits, string statsOutput)
    {
      var sb = new StringBuilder();

      // 先添加统计数据
      sb.Append(statsOutput);

      // 计算每列的最大宽度
      var columnWidths = new Dictionary<string, int>();
      foreach (var key in commits[0].Keys)
      {
        columnWidths[key] = key.Length;
      }

      foreach (var commit in commits)
      {
        foreach (var key in commit.Keys)
        {
          if (commit[key] != null && commit[key].Length > columnWidths[key])
          {
            columnWidths[key] = commit[key].Length;
          }
        }
      }

      // 添加表头
      sb.AppendLine(string.Join(" | ", commits[0].Keys.Select(k => k.PadRight(columnWidths[k]))));
      sb.AppendLine(string.Join("-+-", commits[0].Keys.Select(k => new string('-', columnWidths[k]))));

      // 添加数据行
      foreach (var commit in commits)
      {
        sb.AppendLine(string.Join(" | ", commit.Keys.Select(k => (commit[k] ?? "").PadRight(columnWidths[k]))));
      }

      File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }

    private void SaveAsHtmlWithStats(string path, List<Dictionary<string, string>> commits, string statsOutput)
    {
      if (commits.Count == 0) return;

      var sb = new StringBuilder();

      sb.AppendLine("<!DOCTYPE html>");
      sb.AppendLine("<html>");
      sb.AppendLine("<head>");
      sb.AppendLine("    <meta charset=\"UTF-8\">");
      sb.AppendLine("    <title>Git提交记录</title>");
      sb.AppendLine("    <style>");
      sb.AppendLine("        body { font-family: Arial, sans-serif; margin: 20px; }");
      sb.AppendLine("        table { border-collapse: collapse; width: 100%; margin-bottom: 30px; }");
      sb.AppendLine("        th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }");
      sb.AppendLine("        th { background-color: #f2f2f2; }");
      sb.AppendLine("        tr:nth-child(even) { background-color: #f9f9f9; }");
      sb.AppendLine("        .repo-header { background-color: #e6f7ff; font-weight: bold; }");
      sb.AppendLine("        .stats-section { margin-bottom: 20px; }");
      sb.AppendLine("        .stats-table { width: auto; min-width: 300px; }");
      sb.AppendLine("        h2 { color: #333; margin-top: 30px; }");
      sb.AppendLine("        pre { background-color: #f5f5f5; padding: 10px; white-space: pre-wrap; }");
      sb.AppendLine("    </style>");
      sb.AppendLine("</head>");
      sb.AppendLine("<body>");
      sb.AppendLine("    <h1>Git提交记录</h1>");

      // 添加统计部分
      sb.AppendLine("    <div class=\"stats-section\">");
      sb.AppendLine("        <h2>提交统计</h2>");
      sb.AppendLine("        <pre>" + System.Web.HttpUtility.HtmlEncode(statsOutput) + "</pre>");
      sb.AppendLine("    </div>");

      sb.AppendLine("    <h2>提交详细列表</h2>");
      sb.AppendLine("    <table>");

      // 添加表头
      sb.AppendLine("        <tr>");
      foreach (var key in commits[0].Keys)
      {
        sb.AppendLine(string.Format("            <th>{0}</th>", key));
      }
      sb.AppendLine("        </tr>");

      // 添加数据行
      string lastRepo = "";
      foreach (var commit in commits)
      {
        string currentRepo = commit.ContainsKey("RepoFolder") ? commit["RepoFolder"] :
                            (commit.ContainsKey("Repository") ? commit["Repository"] : "");

        // 如果是新仓库的第一条记录，添加特殊样式
        if (currentRepo != lastRepo)
        {
          sb.AppendLine("        <tr class='repo-header'>");
          lastRepo = currentRepo;
        }
        else
        {
          sb.AppendLine("        <tr>");
        }

        foreach (var key in commit.Keys)
        {
          string value = commit[key] ?? "";
          sb.AppendLine(string.Format("            <td>{0}</td>", System.Web.HttpUtility.HtmlEncode(value)));
        }
        sb.AppendLine("        </tr>");
      }

      sb.AppendLine("    </table>");
      sb.AppendLine(string.Format("    <p>生成时间: {0}</p>", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));
      sb.AppendLine("</body>");
      sb.AppendLine("</html>");

      File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }

    private void SaveAsCsv(string path, List<Dictionary<string, string>> commits)
    {
      if (commits.Count == 0) return;

      var sb = new StringBuilder();

      // 添加CSV头行
      sb.AppendLine(string.Join(",", commits[0].Keys.Select(k => "\"" + k + "\"")));

      // 添加数据行
      foreach (var commit in commits)
      {
        sb.AppendLine(string.Join(",", commit.Values.Select(v => "\"" + (v ?? "").Replace("\"", "\"\"") + "\"")));
      }

      File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }

    private void SaveAsText(string path, List<Dictionary<string, string>> commits)
    {
      if (commits.Count == 0) return;

      var sb = new StringBuilder();

      // 计算每列的最大宽度
      var columnWidths = new Dictionary<string, int>();
      foreach (var key in commits[0].Keys)
      {
        columnWidths[key] = key.Length;
      }

      foreach (var commit in commits)
      {
        foreach (var key in commit.Keys)
        {
          if (commit[key] != null && commit[key].Length > columnWidths[key])
          {
            columnWidths[key] = commit[key].Length;
          }
        }
      }

      // 添加表头
      sb.AppendLine(string.Join(" | ", commits[0].Keys.Select(k => k.PadRight(columnWidths[k]))));
      sb.AppendLine(string.Join("-+-", commits[0].Keys.Select(k => new string('-', columnWidths[k]))));

      // 添加数据行
      foreach (var commit in commits)
      {
        sb.AppendLine(string.Join(" | ", commit.Keys.Select(k => (commit[k] ?? "").PadRight(columnWidths[k]))));
      }

      File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }

    private void SaveAsHtml(string path, List<Dictionary<string, string>> commits)
    {
      if (commits.Count == 0) return;

      var sb = new StringBuilder();

      sb.AppendLine("<!DOCTYPE html>");
      sb.AppendLine("<html>");
      sb.AppendLine("<head>");
      sb.AppendLine("    <meta charset=\"UTF-8\">");
      sb.AppendLine("    <title>Git提交记录</title>");
      sb.AppendLine("    <style>");
      sb.AppendLine("        body { font-family: Arial, sans-serif; margin: 20px; }");
      sb.AppendLine("        table { border-collapse: collapse; width: 100%; }");
      sb.AppendLine("        th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }");
      sb.AppendLine("        th { background-color: #f2f2f2; }");
      sb.AppendLine("        tr:nth-child(even) { background-color: #f9f9f9; }");
      sb.AppendLine("        .repo-header { background-color: #e6f7ff; font-weight: bold; }");
      sb.AppendLine("    </style>");
      sb.AppendLine("</head>");
      sb.AppendLine("<body>");
      sb.AppendLine("    <h1>Git提交记录</h1>");
      sb.AppendLine("    <table>");

      // 添加表头
      sb.AppendLine("        <tr>");
      foreach (var key in commits[0].Keys)
      {
        sb.AppendLine(string.Format("            <th>{0}</th>", key));
      }
      sb.AppendLine("        </tr>");

      // 添加数据行
      string lastRepo = "";
      foreach (var commit in commits)
      {
        string currentRepo = commit.ContainsKey("RepoFolder") ? commit["RepoFolder"] :
                            (commit.ContainsKey("Repository") ? commit["Repository"] : "");

        // 如果是新仓库的第一条记录，添加特殊样式
        if (currentRepo != lastRepo)
        {
          sb.AppendLine("        <tr class='repo-header'>");
          lastRepo = currentRepo;
        }
        else
        {
          sb.AppendLine("        <tr>");
        }

        foreach (var key in commit.Keys)
        {
          string value = commit[key] ?? "";
          sb.AppendLine(string.Format("            <td>{0}</td>", System.Web.HttpUtility.HtmlEncode(value)));
        }
        sb.AppendLine("        </tr>");
      }

      sb.AppendLine("    </table>");
      sb.AppendLine(string.Format("    <p>生成时间: {0}</p>", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));
      sb.AppendLine("</body>");
      sb.AppendLine("</html>");

      File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }

    private void SaveAsXml(string path, List<Dictionary<string, string>> commits)
    {
      if (commits.Count == 0) return;

      var xmlDoc = new XmlDocument();
      var xmlDecl = xmlDoc.CreateXmlDeclaration("1.0", "UTF-8", null);
      xmlDoc.AppendChild(xmlDecl);

      var rootElement = xmlDoc.CreateElement("GitCommits");
      xmlDoc.AppendChild(rootElement);

      foreach (var commit in commits)
      {
        var commitElement = xmlDoc.CreateElement("Commit");

        foreach (var kvp in commit)
        {
          var fieldElement = xmlDoc.CreateElement(kvp.Key);
          fieldElement.InnerText = kvp.Value ?? "";
          commitElement.AppendChild(fieldElement);
        }

        rootElement.AppendChild(commitElement);
      }

      xmlDoc.Save(path);
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
      try
      {
        var selectedItems = CommitsDataGrid.SelectedItems.Cast<CommitInfo>().ToList();
        if (selectedItems.Count == 0) return;

        StringBuilder clipboardText = new StringBuilder();
        foreach (var item in selectedItems)
        {
          string line = "";

          // 使用和显示相同的格式
          if (!string.IsNullOrEmpty(FormatTextBox.Text))
          {
            line = FormatTextBox.Text;
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
        ShowCustomMessageBox("复制成功", string.Format("已复制 {0} 行数据到剪贴板", CommitsDataGrid.SelectedItems.Count), false);
      }
      catch (Exception ex)
      {
        ShowCustomMessageBox("错误", string.Format("复制到剪贴板时出错: {0}", ex.Message), false);
      }
    }

    private void ExportSelectedToClipboard_Click(object sender, RoutedEventArgs e)
    {
      try
      {
        var selectedItems = CommitsDataGrid.SelectedItems.Cast<CommitInfo>().ToList();
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
        ShowCustomMessageBox("导出成功", string.Format("已导出 {0} 行数据到剪贴板 (JSON格式)", CommitsDataGrid.SelectedItems.Count), false);
      }
      catch (Exception ex)
      {
        ShowCustomMessageBox("错误", string.Format("导出到剪贴板时出错: {0}", ex.Message), false);
      }
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
      CommitsDataGrid.SelectAll();
    }

    private void DeselectAll_Click(object sender, RoutedEventArgs e)
    {
      CommitsDataGrid.UnselectAll();
    }

    private void ViewCommitDetails_Click(object sender, RoutedEventArgs e)
    {
      if (CommitsDataGrid.SelectedItem is CommitInfo selectedCommit)
      {
        var details = new StringBuilder();
        details.AppendLine("提交详情：");
        details.AppendLine(string.Format("仓库: {0}", selectedCommit.Repository));
        details.AppendLine(string.Format("仓库路径: {0}", selectedCommit.RepoPath));
        details.AppendLine(string.Format("提交ID: {0}", selectedCommit.CommitId));
        details.AppendLine(string.Format("作者: {0}", selectedCommit.Author));
        details.AppendLine(string.Format("日期: {0}", selectedCommit.Date));
        details.AppendLine(string.Format("消息: {0}", selectedCommit.Message));

        ShowCustomMessageBox("提交详情", details.ToString(), false);
      }
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
        CommitsDataGrid.ItemsSource = _allCommits;
        return;
      }

      // 应用筛选
      _filteredCommits = _allCommits.Where(commit =>
          (commit.Repository != null && commit.Repository.ToLower().Contains(filterText)) ||
          (commit.RepoFolder != null && commit.RepoFolder.ToLower().Contains(filterText)) ||
          (commit.Author != null && commit.Author.ToLower().Contains(filterText)) ||
          (commit.Message != null && commit.Message.ToLower().Contains(filterText)) ||
          (commit.Date != null && commit.Date.ToLower().Contains(filterText)) ||
          (commit.CommitId != null && commit.CommitId.ToLower().Contains(filterText))
      ).ToList();

      // 更新UI
      CommitsDataGrid.ItemsSource = _filteredCommits;

      // 显示筛选结果
      ShowCustomMessageBox("筛选结果", string.Format("找到 {0} 条匹配记录", _filteredCommits.Count), false);
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
      if (_isRunning)
      {
        _isRunning = false;
        _gitOperationsManager.IsRunning = false;
        StartButton.IsEnabled = true;
        UpdateOutput("===== 查询已手动停止 =====");
        ProgressBar.Visibility = Visibility.Collapsed;
        ShowCustomMessageBox("提示", "查询已手动停止", true);
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