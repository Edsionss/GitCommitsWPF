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
        private StringBuilder _outputContent = new StringBuilder();
        private int _repoCount = 0;
        private int _currentRepo = 0;
        private bool _isRunning = false;
        private List<CommitInfo> _filteredCommits = new List<CommitInfo>(); // 添加筛选后的提交列表
        private List<string> _recentLocations = new List<string>(); // 保存最近扫描的git仓库的位置
        private const int MaxRecentLocations = 10; // 最多保存10个最近扫描的git仓库的位置
        private List<string> _saveLocations = new List<string>(); // 保存最近保存的文件的位置
        private const int MaxSaveLocations = 10; // 最多保存10个最近保存的文件的位置
        private List<string> _recentAuthors = new List<string>(); // 保存最近使用的作者
        private List<string> _scannedAuthors = new List<string>(); // 保存扫描出来的作者
        private const int MaxRecentAuthors = 10; // 最多保存10个最近使用的作者
        private string _tempScanPath = ""; // 用于保存临时扫描路径
        public MainWindow()
        {
            InitializeComponent();

            // 设置默认值
            TimeRangeComboBox.SelectedIndex = 0; // 默认选择'所有时间'
            FormatTextBox.Text = "{Repository} : {Message}";

            // 设置日期选择器为当前日期
            StartDatePicker.SelectedDate = DateTime.Today;
            EndDatePicker.SelectedDate = DateTime.Today;

            // 初始化表格视图
            ConfigureDataGrid();

            // 加载最近的位置
            LoadRecentLocations();

            // 加载最近的保存位置
            LoadSaveLocations();

            // 加载最近使用的作者
            LoadRecentAuthors();
            
            // 监听作者文本框变化
            AuthorTextBox.TextChanged += (s, e) => {
                if (!string.IsNullOrWhiteSpace(AuthorTextBox.Text))
                {
                    AddToRecentAuthors(AuthorTextBox.Text);
                }
            };

            // 加载扫描到的作者
            LoadScannedAuthors();
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
            try
            {
                string appDataPath = FileUtility.GetAppDataDirectory();
                string recentLocationsFile = Path.Combine(appDataPath, "RecentLocations.txt");
                
                if (File.Exists(recentLocationsFile))
                {
                    _recentLocations = FileUtility.ReadLinesFromFile(recentLocationsFile);
                }
            }
            catch (Exception ex)
            {
                // 忽略错误，使用空的最近位置列表
                _recentLocations = new List<string>();
                Debug.WriteLine("加载最近位置时出错: " + ex.Message);
            }
        }

        // 保存最近使用的位置
        private void SaveRecentLocations()
        {
            try
            {
                string appDataPath = FileUtility.GetAppDataDirectory();
                string recentLocationsFile = Path.Combine(appDataPath, "RecentLocations.txt");
                FileUtility.SaveLinesToFile(recentLocationsFile, _recentLocations);
            }
            catch (Exception ex)
            {
                // 忽略错误
                Debug.WriteLine("保存最近位置时出错: " + ex.Message);
            }
        }

        // 添加路径到最近位置列表
        private void AddToRecentLocations(string path)
        {
            if (string.IsNullOrEmpty(path)) return;

            // 移除相同的路径（如果存在）
            _recentLocations.Remove(path);

            // 添加到列表开头
            _recentLocations.Insert(0, path);

            // 限制最近位置数量
            if (_recentLocations.Count > MaxRecentLocations)
            {
                _recentLocations.RemoveAt(_recentLocations.Count - 1);
            }

            // 保存更新后的列表
            SaveRecentLocations();
        }

        // 检查路径是否为Git仓库
        private bool IsGitRepository(string path)
        {
            return GitService.IsGitRepository(path);
        }

        // 验证路径是否为Git仓库，如果启用了验证
        private bool ValidatePath(string path)
        {
            bool verifyGitPaths = false;
            Dispatcher.Invoke(() =>
            {
                verifyGitPaths = VerifyGitPathsCheckBox.IsChecked == true;
            });

            if (!verifyGitPaths)
                return true; // 如果未启用验证，直接返回true

            if (IsGitRepository(path))
                return true;

            Dispatcher.Invoke(() =>
            {
                ShowCustomMessageBox("验证失败", string.Format("路径未通过验证，不是Git仓库: {0}", path), false);
            });
            return false;
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

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isRunning)
            {
                ShowCustomMessageBox("提示", "查询正在进行中，请等待完成。", false);
                return;
            }

            // 检查路径输入
            if (string.IsNullOrWhiteSpace(PathsTextBox.Text))
            {
                ShowCustomMessageBox("错误", "请至少输入一个要扫描的路径。", false);
                return;
            }

            // 检查自定义时间范围
            if (((ComboBoxItem)TimeRangeComboBox.SelectedItem).Tag.ToString() == "custom")
            {
                if (!StartDatePicker.SelectedDate.HasValue || !EndDatePicker.SelectedDate.HasValue)
                {
                    ShowCustomMessageBox("错误", "自定义时间范围模式下，必须选择开始和结束日期。", false);
                    return;
                }

                if (StartDatePicker.SelectedDate > EndDatePicker.SelectedDate)
                {
                    ShowCustomMessageBox("错误", "开始日期不能晚于结束日期。", false);
                    return;
                }
            }

            // 保存最近使用的路径
            foreach (var path in PathsTextBox.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                {
                    AddToRecentLocations(path);
                }
            }

            // 准备开始查询
            _allCommits.Clear();
            // 不再清除日志内容，添加分隔线
            _outputContent.AppendLine("\n===== 开始新查询 " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " =====\n");
            ResultTextBox.Text = _outputContent.ToString(); // 更新显示
            CommitsDataGrid.ItemsSource = null;
            SaveButton.IsEnabled = false;

            // 显示进度条
            ProgressBar.Visibility = Visibility.Visible;
            ProgressBar.Value = 5; // 设置初始进度值
            StartButton.IsEnabled = false;
            _isRunning = true;

            try
            {
                // 异步执行查询
                await Task.Run(() => CollectGitCommits());
                // 查询完成后，显示结果并更新UI
                await Task.Run(() => ShowResults());

                Dispatcher.Invoke(() =>
                {
                    SaveButton.IsEnabled = true;
                    int commitCount = _allCommits.Count;
                    string commitText = commitCount == 1 ? "条提交记录" : "条提交记录";
                    UpdateOutput(string.Format("===== 扫描完成，找到 {0} {1} =====", commitCount, commitText));
                });
            }
            catch (Exception ex)
            {
                ShowCustomMessageBox("错误", string.Format("执行过程中发生错误：{0}", ex.Message), false);
            }
            finally
            {
                _isRunning = false;
                Dispatcher.Invoke(() =>
                {
                    StartButton.IsEnabled = true;
                    ProgressBar.Visibility = Visibility.Collapsed;
                });
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(OutputPathTextBox.Text))
            {
                // 创建默认文件名：Git提交记录_年月日.csv
                string defaultFileName = string.Format("Git提交记录_{0}.txt", DateTime.Now.ToString("yyyyMMdd"));

                var dialog = new SaveFileDialog
                {
                    Title = "保存结果",
                    Filter = "CSV文件|*.csv|文本文件|*.txt|HTML文件|*.html|JSON文件|*.json|XML文件|*.xml|所有文件|*.*",
                    DefaultExt = ".txt",
                    FileName = defaultFileName
                };

                if (dialog.ShowDialog() == true)
                {
                    OutputPathTextBox.Text = dialog.FileName;
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

                // 使用自定义消息窗口替代MessageBox
                ShowCustomMessageBox("结果已成功保存", string.Format("结果已成功保存到：{0}", filePath), true);
            }
            catch (Exception ex)
            {
                // 使用自定义消息窗口替代MessageBox
                ShowCustomMessageBox("保存错误", string.Format("保存文件时发生错误：{0}", ex.Message), false);
            }
        }

        private void CollectGitCommits()
        {
            // 获取路径列表（从UI线程读取文本）
            string pathsText = "";
            bool verifyGitPaths = false;
            Dispatcher.Invoke(() =>
            {
                pathsText = PathsTextBox.Text;
                verifyGitPaths = VerifyGitPathsCheckBox.IsChecked == true;
                // 立即设置初始进度值，让用户知道程序已开始运行
                UpdateProgressBar(10);
                UpdateOutput("正在初始化Git仓库扫描...");
            });
            var paths = pathsText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            // 计算日期范围
            string since = "";
            string until = DateTime.Now.AddDays(1).ToString("yyyy-MM-dd"); // 设置到明天来包含今天的提交
            
            // 增加进度到15%，表示日期处理阶段
            UpdateProgressBar(15);

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

            // 获取选择的Git字段
            List<string> gitFields = new List<string>();
            // 直接添加所有必要的Git字段
            gitFields.Add("CommitId");
            gitFields.Add("Author");
            gitFields.Add("Date");
            gitFields.Add("Message");

            // 构建Git格式字符串
            var gitFormatParts = new List<string>();
            var gitFieldMap = new Dictionary<string, string>
            {
                { "CommitId", "%H" },
                { "Author", "%an" },
                { "Date", "%ad" },
                { "Message", "%s" }
            };

            foreach (var field in gitFields)
            {
                if (gitFieldMap.ContainsKey(field))
                {
                    gitFormatParts.Add(gitFieldMap[field]);
                }
            }
            string gitFormat = string.Join("|", gitFormatParts);
            
            // 增加进度到20%，表示准备阶段完成
            UpdateProgressBar(20);

            // 查找所有Git仓库
            UpdateOutput("正在搜索Git仓库，请稍后...");
            var gitRepos = new List<DirectoryInfo>();
            
            // 搜索仓库阶段，分配20%-40%的进度
            int pathIndex = 0;
            int totalPaths = paths.Length;

            foreach (var path in paths)
            {
                // 检查是否已手动停止
                if (!_isRunning) 
                {
                    UpdateOutput("扫描已手动停止");
                    return;
                }
                
                pathIndex++;
                // 更新搜索阶段的进度
                int searchProgress = 20 + (int)((pathIndex / (double)totalPaths) * 20);
                UpdateProgressBar(searchProgress);
                UpdateOutput(string.Format("正在搜索路径 [{0}/{1}]: {2}", pathIndex, totalPaths, path));
                
                try
                {
                    var dir = new DirectoryInfo(path);
                    if (!dir.Exists)
                    {
                        UpdateOutput(string.Format("警告: 路径不存在: {0}", path));
                        continue;
                    }

                    if (verifyGitPaths)
                    {
                        // 如果启用了Git目录验证，就直接将输入的路径视为Git仓库
                        if (IsGitRepository(path))
                        {
                            gitRepos.Add(dir);
                            UpdateOutput(string.Format("添加Git仓库: {0}", path));
                        }
                        else
                        {
                            UpdateOutput(string.Format("警告: 路径不是Git仓库: {0}", path));
                        }
                    }
                    else
                    {
                        // 没有启用验证，按原来的方式搜索所有子目录中的.git
                        var pathRepos = dir.GetDirectories(".git", SearchOption.AllDirectories)
                            .Select(gitDir => gitDir.Parent)
                            .Where(parent => parent != null)
                            .ToList();

                        UpdateOutput(string.Format("在路径 '{0}' 下找到 {1} 个Git仓库", path, pathRepos.Count));
                        gitRepos.AddRange(pathRepos);
                    }
                }
                catch (Exception ex)
                {
                    UpdateOutput(string.Format("扫描路径时出错: {0}", ex.Message));
                }
            }

            _repoCount = gitRepos.Count;
            UpdateOutput(string.Format("总共找到 {0} 个Git仓库", _repoCount));
            
            // 如果没有找到仓库，设置为较高进度并退出
            if (_repoCount == 0)
            {
                UpdateProgressBar(90);
                UpdateOutput("未找到任何Git仓库，扫描结束");
                return;
            }
            
            // 设置为40%进度，表示开始处理仓库
            UpdateProgressBar(40);

            // 处理每个Git仓库，分配40%-95%的进度
            _currentRepo = 0;
            DateTime? earliestRepoDate = null;

            foreach (var repo in gitRepos)
            {
                // 检查是否已手动停止
                if (!_isRunning) 
                {
                    UpdateOutput("处理仓库过程已手动停止");
                    return;
                }
                
                _currentRepo++;
                // 更新处理仓库阶段的进度
                int percentComplete = 40 + (int)((_currentRepo / (double)_repoCount) * 55);
                UpdateProgressBar(percentComplete);
                UpdateOutput(string.Format("处理仓库 [{0}/{1}]: {2}", _currentRepo, _repoCount, repo.FullName));

                try
                {
                    string currentDirectory = Directory.GetCurrentDirectory();
                    Directory.SetCurrentDirectory(repo.FullName);

                    // 获取仓库创建时间（第一次提交的时间）
                    var firstCommitProcess = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "git",
                            Arguments = "log --reverse --format=\"%ad\" --date=format:\"%Y-%m-%d %H:%M:%S\"",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            CreateNoWindow = true,
                            StandardOutputEncoding = Encoding.UTF8
                        }
                    };

                    firstCommitProcess.Start();
                    string firstCommitDateStr = firstCommitProcess.StandardOutput.ReadLine();
                    firstCommitProcess.WaitForExit();

                    if (!string.IsNullOrEmpty(firstCommitDateStr))
                    {
                        DateTime repoCreateDate = DateTime.ParseExact(firstCommitDateStr, "yyyy-MM-dd HH:mm:ss", null);
                        if (earliestRepoDate == null || repoCreateDate < earliestRepoDate)
                        {
                            earliestRepoDate = repoCreateDate;
                        }

                        UpdateOutput(string.Format("   仓库创建时间: {0}", firstCommitDateStr));
                    }

                    // 构建Git命令参数
                    var arguments = "log";

                    if (!string.IsNullOrEmpty(since))
                    {
                        arguments += " --since=\"" + since + "\"";
                    }

                    arguments += " --until=\"" + until + "\"";
                    arguments += " --pretty=format:\"" + gitFormat + "\"";
                    arguments += " --date=format:\"%Y-%m-%d %H:%M:%S\"";

                    if (!string.IsNullOrEmpty(author))
                    {
                        arguments += " --author=\"" + author + "\"";
                    }

                    // 执行Git命令
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "git",
                            Arguments = arguments,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true,
                            StandardOutputEncoding = Encoding.UTF8,
                            StandardErrorEncoding = Encoding.UTF8
                        }
                    };

                    process.Start();
                    var output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();

                    if (!string.IsNullOrEmpty(output))
                    {
                        var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                        foreach (var line in lines)
                        {
                            // 检查是否已手动停止
                            if (!_isRunning) 
                            {
                                UpdateOutput("处理提交数据过程已手动停止");
                                Directory.SetCurrentDirectory(currentDirectory);
                                return;
                            }
                            
                            if (string.IsNullOrEmpty(line))
                                continue;

                            try
                            {
                                var parts = line.Split('|');
                                if (parts.Length >= gitFields.Count)
                                {
                                    // 创建一个提交信息对象
                                    var commitObj = new CommitInfo
                                    {
                                        Repository = repo.Name,
                                        RepoPath = repo.FullName,
                                        RepoFolder = Path.GetFileName(repo.FullName)
                                    };

                                    // 根据选择的字段添加属性
                                    for (int i = 0; i < gitFields.Count; i++)
                                    {
                                        if (i < parts.Length) // 确保下标不会越界
                                        {
                                            var fieldName = gitFields[i];
                                            var fieldValue = parts[i];

                                            switch (fieldName)
                                            {
                                                case "CommitId":
                                                    commitObj.CommitId = fieldValue;
                                                    break;
                                                case "Author":
                                                    commitObj.Author = fieldValue;
                                                    break;
                                                case "Date":
                                                    commitObj.Date = fieldValue;
                                                    break;
                                                case "Message":
                                                    commitObj.Message = fieldValue;
                                                    break;
                                            }
                                        }
                                    }

                                    // 应用作者筛选
                                    bool shouldInclude = true;
                                    if (!string.IsNullOrEmpty(authorFilter) && commitObj.Author != null)
                                    {
                                        shouldInclude = false;
                                        var authors = authorFilter.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                                        foreach (var authorPattern in authors)
                                        {
                                            string trimmedPattern = authorPattern.Trim();
                                            if (!string.IsNullOrEmpty(trimmedPattern) &&
                                                commitObj.Author.IndexOf(trimmedPattern, StringComparison.OrdinalIgnoreCase) >= 0)
                                            {
                                                shouldInclude = true;
                                                break;
                                            }
                                        }
                                    }

                                    if (shouldInclude)
                                    {
                                        _allCommits.Add(commitObj);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                UpdateOutput(string.Format("处理提交记录时出错: {0}", ex.Message));
                            }
                        }
                    }

                    // 恢复当前目录
                    Directory.SetCurrentDirectory(currentDirectory);
                }
                catch (Exception ex)
                {
                    UpdateOutput(string.Format("警告: 处理仓库 '{0}' 时出错: {1}", repo.FullName, ex.Message));
                }
            }

            // 替换进度条完成消息
            UpdateOutput("所有仓库处理完成 (100%)");

            // 输出扫描信息
            UpdateOutput("==== 扫描信息 ====");
            if (string.IsNullOrEmpty(since))
            {
                if (earliestRepoDate.HasValue)
                {
                    string earliestDateStr = earliestRepoDate.Value.ToString("yyyy-MM-dd HH:mm:ss");
                    UpdateOutput(string.Format("时间范围: 从仓库创建开始 ({0}) 至 {1}", earliestDateStr, DateTime.Now.ToString("yyyy-MM-dd")));
                }
                else
                {
                    UpdateOutput(string.Format("时间范围: 从仓库创建开始 至 {0}", DateTime.Now.ToString("yyyy-MM-dd")));
                }
            }
            else
            {
                // 显示真实的日期范围，而不是调整后的日期
                string displayUntil = DateTime.Parse(until).AddDays(-1).ToString("yyyy-MM-dd");
                UpdateOutput(string.Format("时间范围: {0} 至 {1}", since, displayUntil));
            }

            if (string.IsNullOrEmpty(author))
            {
                UpdateOutput("提交作者: 所有作者");
            }
            else
            {
                UpdateOutput(string.Format("提交作者: {0}", author));
            }

            if (!string.IsNullOrEmpty(authorFilter))
            {
                UpdateOutput(string.Format("作者过滤: {0}", authorFilter));
            }

            UpdateOutput(string.Format("提取的Git字段: {0}", string.Join(", ", gitFields)));
            UpdateOutput("===================");

            // 输出结果数量
            UpdateOutput(string.Format("在指定时间范围内共找到 {0} 个提交", _allCommits.Count));

            // 添加到最近使用的作者
            if (!string.IsNullOrEmpty(author))
            {
                AddToRecentAuthors(author);
            }

            // 设置进度条到完成状态
            UpdateProgressBar(100);
            UpdateOutput("Git提交记录收集完成");
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
                    ResultTextBox.Text = _outputContent.ToString();
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

                Dispatcher.Invoke(() =>
                {
                    ResultTextBox.Text = _outputContent.ToString() + Environment.NewLine +
                statsOutput.ToString() + formattedOutput.ToString();
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

                Dispatcher.Invoke(() =>
                {
                    ResultTextBox.Text = _outputContent.ToString() + Environment.NewLine +
                statsOutput.ToString() +
                JsonConvert.SerializeObject(filteredCommits, Newtonsoft.Json.Formatting.Indented);
                });
            }
        }

        private void SaveResults(string outputPath)
        {
            if (_allCommits.Count == 0)
            {
                File.WriteAllText(outputPath, _outputContent.ToString(), Encoding.UTF8);
                return;
            }

            // 获取选择的字段
            List<string> selectedFields = new List<string>();
            if (RepositoryFieldCheckBox.IsChecked == true) selectedFields.Add("Repository");
            if (RepoPathFieldCheckBox.IsChecked == true) selectedFields.Add("RepoPath");
            if (RepoFolderFieldCheckBox.IsChecked == true) selectedFields.Add("RepoFolder");
            if (CommitIdFieldCheckBox.IsChecked == true) selectedFields.Add("CommitId");
            if (AuthorFieldCheckBox.IsChecked == true) selectedFields.Add("Author");
            if (DateFieldCheckBox.IsChecked == true) selectedFields.Add("Date");
            if (MessageFieldCheckBox.IsChecked == true) selectedFields.Add("Message");

            // 生成统计数据字符串(如果启用)
            var statsOutput = new StringBuilder();
            if (EnableStatsCheckBox.IsChecked == true)
            {
                statsOutput.AppendLine("\n======== 提交统计 ========\n");
                GenerateStats(statsOutput);
                statsOutput.AppendLine("\n==========================\n");
            }

            // 应用格式并保存
            string format = FormatTextBox.Text;
            if (!string.IsNullOrEmpty(format))
            {
                var formattedOutput = new StringBuilder();
                var displayedRepos = new Dictionary<string, bool>();

                // 如果启用了统计，先添加统计数据
                if (EnableStatsCheckBox.IsChecked == true)
                {
                    formattedOutput.Append(statsOutput.ToString());
                }

                foreach (var commit in _allCommits)
                {
                    string line = format;

                    // 获取当前提交的仓库标识符
                    string repoKey = !string.IsNullOrEmpty(commit.RepoFolder) ? commit.RepoFolder : commit.Repository;

                    // 替换所有占位符
                    line = line.Replace("{Repository}",
                        (!ShowRepeatedRepoNamesCheckBox.IsChecked.Value && displayedRepos.ContainsKey(repoKey)) ?
                        new string(' ', repoKey.Length) : commit.Repository);

                    line = line.Replace("{RepoPath}", commit.RepoPath);

                    line = line.Replace("{RepoFolder}",
                        (!ShowRepeatedRepoNamesCheckBox.IsChecked.Value && displayedRepos.ContainsKey(repoKey)) ?
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

                File.WriteAllText(outputPath, formattedOutput.ToString(), Encoding.UTF8);
            }
            else
            {
                // 如果没有指定格式，根据文件扩展名保存
                string extension = Path.GetExtension(outputPath).ToLower();
                var filteredCommits = new List<Dictionary<string, string>>();

                // 创建筛选后的对象列表
                foreach (var commit in _allCommits)
                {
                    var filteredCommit = new Dictionary<string, string>();

                    if (selectedFields.Contains("Repository")) filteredCommit["Repository"] = commit.Repository;
                    if (selectedFields.Contains("RepoPath")) filteredCommit["RepoPath"] = commit.RepoPath;
                    if (selectedFields.Contains("RepoFolder")) filteredCommit["RepoFolder"] = commit.RepoFolder;
                    if (selectedFields.Contains("CommitId")) filteredCommit["CommitId"] = commit.CommitId;
                    if (selectedFields.Contains("Author")) filteredCommit["Author"] = commit.Author;
                    if (selectedFields.Contains("Date")) filteredCommit["Date"] = commit.Date;
                    if (selectedFields.Contains("Message")) filteredCommit["Message"] = commit.Message;

                    filteredCommits.Add(filteredCommit);
                }

                // 如果启用了统计，先保存统计内容
                if (EnableStatsCheckBox.IsChecked == true && (extension == ".txt" || extension == ".html"))
                {
                    switch (extension)
                    {
                        case ".txt":
                            SaveAsTextWithStats(outputPath, filteredCommits, statsOutput.ToString());
                            break;
                        case ".html":
                            SaveAsHtmlWithStats(outputPath, filteredCommits, statsOutput.ToString());
                            break;
                        default:
                            // 其他格式不支持统计数据的结构化表示，仍使用原来的保存方式
                            switch (extension)
                            {
                                case ".csv":
                                    SaveAsCsv(outputPath, filteredCommits);
                                    break;
                                case ".json":
                                    File.WriteAllText(outputPath, JsonConvert.SerializeObject(filteredCommits, Newtonsoft.Json.Formatting.Indented), Encoding.UTF8);
                                    break;
                                case ".xml":
                                    SaveAsXml(outputPath, filteredCommits);
                                    break;
                                default:
                                    // 默认保存为文本
                                    SaveAsText(outputPath, filteredCommits);
                                    break;
                            }
                            break;
                    }
                }
                else
                {
                    // 没有启用统计，使用原来的保存方式
                    switch (extension)
                    {
                        case ".csv":
                            SaveAsCsv(outputPath, filteredCommits);
                            break;
                        case ".json":
                            File.WriteAllText(outputPath, JsonConvert.SerializeObject(filteredCommits, Newtonsoft.Json.Formatting.Indented), Encoding.UTF8);
                            break;
                        case ".txt":
                            SaveAsText(outputPath, filteredCommits);
                            break;
                        case ".html":
                            SaveAsHtml(outputPath, filteredCommits);
                            break;
                        case ".xml":
                            SaveAsXml(outputPath, filteredCommits);
                            break;
                        default:
                            // 默认保存为文本
                            SaveAsText(outputPath, filteredCommits);
                            break;
                    }
                }
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

        private void UpdateOutput(string message)
        {
            // 添加时间戳
            string timeStamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string formattedMessage = string.Format("[{0}] {1}", timeStamp, message);
            
            _outputContent.AppendLine(formattedMessage);

            // 使用BeginInvoke避免死锁问题
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ResultTextBox.Text = _outputContent.ToString();
                ResultTextBox.ScrollToEnd();
            }));
        }

        private void UpdateProgressBar(int value)
        {
            // 使用BeginInvoke避免死锁问题
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ProgressBar.Value = value;
            }));
        }

        // 添加最近位置按钮点击事件
        private void AddRecentLocation_Click(object sender, RoutedEventArgs e)
        {
            if (_recentLocations.Count == 0)
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
                ItemsSource = _recentLocations
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
                _recentLocations.Clear();
                SaveRecentLocations();
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
            bool result = false;

            // 创建自定义确认窗口
            var confirmWindow = new Window
            {
                Title = title,
                Width = 360,
                Height = 180,
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
                Text = message,
                Margin = new Thickness(20),
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
                Margin = new Thickness(0, 0, 0, 20)
            };
            Grid.SetRow(buttonPanel, 1);

            // 确定按钮
            var yesButton = new Button
            {
                Content = "确定",
                Padding = new Thickness(15, 5, 15, 5),
                Margin = new Thickness(10),
                MinWidth = 80
            };
            yesButton.Click += (s, e) => { result = true; confirmWindow.Close(); };

            // 取消按钮
            var noButton = new Button
            {
                Content = "取消",
                Padding = new Thickness(15, 5, 15, 5),
                Margin = new Thickness(10),
                MinWidth = 80
            };
            noButton.Click += (s, e) => { result = false; confirmWindow.Close(); };

            buttonPanel.Children.Add(yesButton);
            buttonPanel.Children.Add(noButton);
            grid.Children.Add(buttonPanel);
            confirmWindow.Content = grid;

            // 显示窗口并等待结果
            confirmWindow.ShowDialog();
            return result;
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
            // 创建自定义消息窗口
            var customMessageWindow = new Window
            {
                Title = title,
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
                Text = message,
                Margin = new Thickness(20),
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
                Margin = new Thickness(0, 0, 0, 20)
            };
            Grid.SetRow(buttonPanel, 1);

            // 确定按钮
            var okButton = new Button
            {
                Content = "确定",
                Padding = new Thickness(15, 5, 15, 5),
                Margin = new Thickness(10),
                MinWidth = 80
            };
            okButton.Click += (s, e) => customMessageWindow.Close();

            buttonPanel.Children.Add(okButton);

            // 如果是成功消息且保存了文件，添加打开文件按钮
            if (isSuccess && !string.IsNullOrEmpty(OutputPathTextBox.Text))
            {
                // 添加保存位置到最近保存位置列表
                AddToSaveLocations(OutputPathTextBox.Text);

                // 添加打开文件按钮
                var openFileButton = new Button
                {
                    Content = "打开文件",
                    Padding = new Thickness(15, 5, 15, 5),
                    Margin = new Thickness(10),
                    MinWidth = 80
                };

                openFileButton.Click += (s, e) =>
                {
                    try
                    {
                        // 打开保存的文件
                        var filePath = OutputPathTextBox.Text;
                        if (File.Exists(filePath))
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = filePath,
                                UseShellExecute = true
                            });
                        }
                        customMessageWindow.Close();
                    }
                    catch (Exception ex)
                    {
                        messageText.Text = string.Format("无法打开文件：{0}", ex.Message);
                    }
                };

                buttonPanel.Children.Add(openFileButton);
            }

            grid.Children.Add(buttonPanel);
            customMessageWindow.Content = grid;

            // 显示窗口
            customMessageWindow.ShowDialog();
        }

        // 加载最近保存的位置
        private void LoadSaveLocations()
        {
            try
            {
                string appDataPath = FileUtility.GetAppDataDirectory();
                string saveLocationsFile = Path.Combine(appDataPath, "SaveLocations.txt");
                
                if (File.Exists(saveLocationsFile))
                {
                    _saveLocations = FileUtility.ReadLinesFromFile(saveLocationsFile);
                }
            }
            catch (Exception ex)
            {
                // 忽略错误，使用空的最近保存位置列表
                _saveLocations = new List<string>();
                Debug.WriteLine("加载最近保存位置时出错: " + ex.Message);
            }
        }

        // 保存最近保存的位置
        private void SaveSaveLocations()
        {
            try
            {
                string appDataPath = FileUtility.GetAppDataDirectory();
                string saveLocationsFile = Path.Combine(appDataPath, "SaveLocations.txt");
                FileUtility.SaveLinesToFile(saveLocationsFile, _saveLocations);
            }
            catch (Exception ex)
            {
                // 忽略错误
                Debug.WriteLine("保存最近保存位置时出错: " + ex.Message);
            }
        }

        // 添加路径到最近保存位置列表
        private void AddToSaveLocations(string path)
        {
            if (string.IsNullOrEmpty(path)) return;

            // 移除相同的路径（如果存在）
            _saveLocations.Remove(path);

            // 添加到列表开头
            _saveLocations.Insert(0, path);

            // 限制最近位置数量
            if (_saveLocations.Count > MaxSaveLocations)
            {
                _saveLocations.RemoveAt(_saveLocations.Count - 1);
            }

            // 保存更新后的列表
            SaveSaveLocations();
        }

        private void LoadLastPath_Click(object sender, RoutedEventArgs e)
        {
            if (_saveLocations.Count == 0)
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
                ItemsSource = _saveLocations
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
                _saveLocations.Clear();
                SaveSaveLocations();
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
                _outputContent.AppendLine("\n===== 开始扫描Git作者 " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " =====\n");

                // 自动查找本地git作者
                UpdateOutput("开始扫描本地Git作者信息...");

                // 存储找到的Git作者
                List<string> authors = new List<string>();

                // 显示进度条
                Dispatcher.Invoke(() =>
                {
                    ProgressBar.Visibility = Visibility.Visible;
                    ProgressBar.Value = 10;  // 设置一个基础进度值，让用户知道程序开始运行
                    UpdateOutput("正在初始化扫描过程...");
                });

                // 异步扫描所有路径
                await Task.Run(() =>
                {
                    int pathCount = paths.Count;
                    int currentPath = 0;
                    int totalGitRepos = 0;
                    int processedRepos = 0;
                    
                    // 第一阶段：计算所有路径下的Git仓库总数 (分配总进度的20%)
                    foreach (var path in paths)
                    {
                        if (Directory.Exists(path))
                        {
                            UpdateOutput(string.Format("正在搜索路径: {0}", path));
                            // 计算每个路径的搜索进度
                            var repos = FindGitRepositories(path);
                            totalGitRepos += repos.Count;
                            
                            // 更新进度，第一阶段分配10-30%的进度
                            currentPath++;
                            int searchProgress = 10 + (int)(20.0 * currentPath / pathCount);
                            UpdateProgressBar(searchProgress);
                        }
                    }
                    
                    // 避免除以零错误
                    if (totalGitRepos == 0)
                    {
                        totalGitRepos = 1;
                        UpdateProgressBar(90);  // 如果没有仓库，进度直接到90%
                    }
                    
                    UpdateOutput(string.Format("共找到 {0} 个Git仓库待扫描", totalGitRepos));
                    // 扫描阶段起始进度为30%
                    int startProgress = 30;
                    
                    // 重置当前路径计数器
                    currentPath = 0;
                    foreach (var path in paths)
                    {
                        currentPath++;
                        
                        try
                        {
                            if (!Directory.Exists(path))
                            {
                                UpdateOutput(string.Format("警告: 路径不存在: {0}", path));
                                continue;
                            }

                            UpdateOutput(string.Format("正在扫描路径 [{0}/{1}]: {2}", currentPath, pathCount, path));

                            // 搜索Git仓库
                            var gitRepos = FindGitRepositories(path);
                            UpdateOutput(string.Format("在路径 '{0}' 下找到 {1} 个Git仓库", path, gitRepos.Count));

                            // 从每个Git仓库获取作者信息
                            foreach (var repo in gitRepos)
                            {
                                processedRepos++;
                                // 更新进度条，基于处理的仓库数量计算进度百分比
                                // 剩余的70%进度分配给实际扫描阶段
                                int percentComplete = startProgress + (int)Math.Round((processedRepos / (double)totalGitRepos) * 70);
                                UpdateProgressBar(percentComplete);
                                UpdateOutput(string.Format("扫描仓库 [{0}/{1}]: {2}", processedRepos, totalGitRepos, repo.FullName));
                                
                                ScanGitAuthors(repo, authors);
                            }
                        }
                        catch (Exception ex)
                        {
                            UpdateOutput(string.Format("扫描路径时出错: {0}", ex.Message));
                        }
                    }
                    
                    // 扫描完成，设置进度为100%
                    UpdateProgressBar(100);
                });

                // 隐藏进度条
                Dispatcher.Invoke(() =>
                {
                    ProgressBar.Visibility = Visibility.Collapsed;
                });

                // 移除重复的作者
                authors = authors.Distinct().OrderBy(a => a).ToList();

                UpdateOutput(string.Format("扫描完成，共找到 {0} 个不同的Git作者", authors.Count));

                // 显示作者选择对话框
                if (authors.Count > 0)
                {
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
            if (_recentLocations.Count == 0)
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
                ItemsSource = _recentLocations
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

        // 查找Git仓库
        private List<DirectoryInfo> FindGitRepositories(string path)
        {
            try
            {
                if (!Directory.Exists(path))
                {
                    UpdateOutput(string.Format("路径不存在: {0}", path));
                    return new List<DirectoryInfo>();
                }

                // 使用GitService查找仓库
                List<DirectoryInfo> gitRepos = GitService.FindGitRepositories(path);
                UpdateOutput(string.Format("在路径 '{0}' 下找到 {1} 个Git仓库", path, gitRepos.Count));
                return gitRepos;
            }
            catch (Exception ex)
            {
                UpdateOutput(string.Format("搜索Git仓库时出错: {0}", ex.Message));
                return new List<DirectoryInfo>();
            }
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
                foreach (var author in newAuthors)
                {
                    if (!_scannedAuthors.Contains(author))
                    {
                        _scannedAuthors.Add(author);
                    }
                }
                // 保存扫描到的作者
                SaveScannedAuthors();
                
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
            // 创建一个选择窗口
            var selectWindow = new Window
            {
                Title = "选择Git作者",
                Width = 400,
                Height = 500,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // 添加搜索框
            var searchPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10) };
            var searchLabel = new TextBlock { Text = "搜索:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 5, 0) };
            var searchBox = new System.Windows.Controls.TextBox { Width = 300, Margin = new Thickness(5, 0, 0, 0) };
            searchPanel.Children.Add(searchLabel);
            searchPanel.Children.Add(searchBox);
            Grid.SetRow(searchPanel, 0);
            grid.Children.Add(searchPanel);

            // 作者列表
            var listBox = new ListBox
            {
                Margin = new Thickness(10),
                ItemsSource = authors
            };
            Grid.SetRow(listBox, 1);
            grid.Children.Add(listBox);

            // 实现搜索功能
            searchBox.TextChanged += (s, e) =>
            {
                string filter = searchBox.Text.ToLower();
                if (string.IsNullOrEmpty(filter))
                {
                    listBox.ItemsSource = authors;
                }
                else
                {
                    listBox.ItemsSource = authors.Where(a => a.ToLower().Contains(filter)).ToList();
                }
            };

            // 按钮面板
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid.SetRow(buttonPanel, 2);

            // 选择按钮
            var selectButton = new Button
            {
                Content = "选择",
                Padding = new Thickness(15, 5, 15, 5),
                Margin = new Thickness(5),
                MinWidth = 80
            };

            selectButton.Click += (s, evt) =>
            {
                if (listBox.SelectedItem is string selectedAuthor)
                {
                    AuthorTextBox.Text = selectedAuthor;
                    selectWindow.Close();
                }
                else
                {
                    ShowCustomMessageBox("提示", "请先选择一个作者", false);
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
                selectWindow.Close();
            };

            buttonPanel.Children.Add(selectButton);
            buttonPanel.Children.Add(cancelButton);
            grid.Children.Add(buttonPanel);

            selectWindow.Content = grid;
            selectWindow.ShowDialog();
        }

        // 复制日志
        private void CopyLog_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 获取日志内容
                string logContent = ResultTextBox.Text;
                if (string.IsNullOrEmpty(logContent))
                {
                    ShowCustomMessageBox("提示", "日志内容为空，无法复制。", false);
                    return;
                }

                // 复制到剪贴板
                System.Windows.Clipboard.SetText(logContent);
                ShowCustomMessageBox("成功", "日志内容已复制到剪贴板。", false);
            }
            catch (Exception ex)
            {
                ShowCustomMessageBox("错误", string.Format("复制日志时出错: {0}", ex.Message), false);
            }
        }

        // 保存日志
        private void SaveLog_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 获取日志内容
                string logContent = ResultTextBox.Text;
                if (string.IsNullOrEmpty(logContent))
                {
                    ShowCustomMessageBox("提示", "日志内容为空，无法保存。", false);
                    return;
                }

                // 创建默认文件名：Git日志_年月日时分秒.log
                string defaultFileName = string.Format("Git日志_{0}.log", DateTime.Now.ToString("yyyyMMdd_HHmmss"));

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
                    ShowCustomMessageBox("成功", string.Format("日志已保存到: {0}", dialog.FileName), true);
                }
            }
            catch (Exception ex)
            {
                ShowCustomMessageBox("错误", string.Format("保存日志时出错: {0}", ex.Message), false);
            }
        }

        // 清空日志
        private void CleanLog_Click(object sender, RoutedEventArgs e)
        {
            // 确认是否清空日志
            if (ShowCustomConfirmDialog("确认清空", "确定要清空所有日志记录吗？此操作不可撤销。"))
            {
                try
                {
                    // 清空StringBuilder和文本框
                    _outputContent.Clear();
                    ResultTextBox.Text = "";
                    UpdateOutput("日志已清空。");
                }
                catch (Exception ex)
                {
                    ShowCustomMessageBox("错误", string.Format("清空日志时出错: {0}", ex.Message), false);
                }
            }
        }

        // 停止查询
        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
          if (_isRunning)
            {
                _isRunning = false;
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
            // 如果最近作者列表为空，则尝试从已有提交中收集
            if (_recentAuthors.Count == 0)
            {
                // 从已有提交中收集作者信息
                HashSet<string> uniqueAuthors = new HashSet<string>();
                foreach (var commit in _allCommits)
                {
                    if (!string.IsNullOrEmpty(commit.Author))
                    {
                        uniqueAuthors.Add(commit.Author);
                    }
                }
                
                // 如果没有找到作者
                if (uniqueAuthors.Count == 0)
                {
                    ShowCustomMessageBox("提示", "尚未找到任何作者信息。请先执行查询或添加作者。", false);
                    return;
                }
                
                // 添加找到的作者到最近作者列表
                foreach (var author in uniqueAuthors)
                {
                    AddToRecentAuthors(author);
                }
            }
            
            // 创建作者选择窗口
            var selectWindow = new Window
            {
                Title = "选择作者",
                Width = 500,
                Height = 400,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };
            
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            
            // 创建Tab控件
            var tabControl = new TabControl 
            { 
                Margin = new Thickness(10)
            };
            Grid.SetRow(tabControl, 0);
            
            // 第一个Tab：最近使用的作者
            var recentTab = new TabItem
            {
                Header = "最近使用",
                IsSelected = true
            };
            
            // 最近使用的作者列表
            var recentListBox = new ListBox
            {
                Margin = new Thickness(5),
                ItemsSource = _recentAuthors
            };
            recentTab.Content = recentListBox;
            
            // 第二个Tab：扫描到的作者
            var scannedTab = new TabItem
            {
                Header = "扫描记录"
            };
            
            // 扫描到的作者列表及搜索框
            var scannedPanel = new DockPanel();
            var searchPanel = new StackPanel 
            { 
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(5)
            };
            DockPanel.SetDock(searchPanel, Dock.Top);
            
            var searchLabel = new TextBlock
            {
                Text = "搜索:",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 5, 0)
            };
            
            var searchBox = new System.Windows.Controls.TextBox
            {
                Width = 300,
                Margin = new Thickness(5, 0, 0, 0)
            };
            
            searchPanel.Children.Add(searchLabel);
            searchPanel.Children.Add(searchBox);
            scannedPanel.Children.Add(searchPanel);
            
            // 扫描到的作者列表
            var scannedListBox = new ListBox
            {
                Margin = new Thickness(5),
                ItemsSource = _scannedAuthors.OrderBy(a => a).ToList()
            };
            DockPanel.SetDock(scannedListBox, Dock.Top);
            scannedPanel.Children.Add(scannedListBox);
            
            // 实现搜索功能
            searchBox.TextChanged += (s, evt) =>
            {
                string filter = searchBox.Text.ToLower();
                if (string.IsNullOrEmpty(filter))
                {
                    scannedListBox.ItemsSource = _scannedAuthors.OrderBy(a => a).ToList();
                }
                else
                {
                    scannedListBox.ItemsSource = _scannedAuthors
                        .Where(a => a.ToLower().Contains(filter))
                        .OrderBy(a => a)
                        .ToList();
                }
            };
            
            scannedTab.Content = scannedPanel;
            
            // 添加Tab到TabControl
            tabControl.Items.Add(recentTab);
            tabControl.Items.Add(scannedTab);
            grid.Children.Add(tabControl);
            
            // 按钮面板
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid.SetRow(buttonPanel, 1);
            
            // 选择按钮
            var selectButton = new Button
            {
                Content = "选择",
                Padding = new Thickness(15, 5, 15, 5),
                Margin = new Thickness(5),
                MinWidth = 80
            };
            
            selectButton.Click += (s, evt) =>
            {
                ListBox selectedListBox = null;
                if (tabControl.SelectedItem == recentTab)
                {
                    selectedListBox = recentListBox;
                }
                else
                {
                    selectedListBox = scannedListBox;
                }
                
                if (selectedListBox != null && selectedListBox.SelectedItem is string selectedAuthor)
                {
                    AuthorTextBox.Text = selectedAuthor;
                    
                    // 如果选择的是扫描作者，添加到最近作者列表
                    if (tabControl.SelectedItem == scannedTab)
                    {
                        AddToRecentAuthors(selectedAuthor);
                    }
                    
                    selectWindow.Close();
                    
                    // 应用筛选
                    ApplyFilter();
                }
                else
                {
                    ShowCustomMessageBox("提示", "请先选择一个作者", false);
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
                if (tabControl.SelectedItem == recentTab)
                {
                    // 清除最近作者
                    _recentAuthors.Clear();
                    SaveRecentAuthors();
                    recentListBox.ItemsSource = null;
                    recentListBox.Items.Clear();
                    ShowCustomMessageBox("信息", "已清除最近作者记录。", false);
                }
                else
                {
                    // 清除扫描作者
                    _scannedAuthors.Clear();
                    SaveScannedAuthors();
                    scannedListBox.ItemsSource = null;
                    scannedListBox.Items.Clear();
                    ShowCustomMessageBox("信息", "已清除扫描作者记录。", false);
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
                selectWindow.Close();
            };
            
            buttonPanel.Children.Add(selectButton);
            buttonPanel.Children.Add(clearButton);
            buttonPanel.Children.Add(cancelButton);
            
            grid.Children.Add(buttonPanel);
            
            selectWindow.Content = grid;
            selectWindow.ShowDialog();
        }

        // 加载最近使用的作者
        private void LoadRecentAuthors()
        {
            try
            {
                string appDataPath = FileUtility.GetAppDataDirectory();
                string recentAuthorsFile = Path.Combine(appDataPath, "RecentAuthors.txt");
                
                if (File.Exists(recentAuthorsFile))
                {
                    _recentAuthors = FileUtility.ReadLinesFromFile(recentAuthorsFile);
                }
            }
            catch (Exception ex)
            {
                // 忽略错误，使用空的最近作者列表
                _recentAuthors = new List<string>();
                Debug.WriteLine("加载最近作者时出错: " + ex.Message);
            }
        }

        // 保存最近使用的作者
        private void SaveRecentAuthors()
        {
            try
            {
                string appDataPath = FileUtility.GetAppDataDirectory();
                string recentAuthorsFile = Path.Combine(appDataPath, "RecentAuthors.txt");
                FileUtility.SaveLinesToFile(recentAuthorsFile, _recentAuthors);
            }
            catch (Exception ex)
            {
                // 忽略错误
                Debug.WriteLine("保存最近作者时出错: " + ex.Message);
            }
        }

        // 添加作者到最近作者列表
        private void AddToRecentAuthors(string author)
        {
            if (string.IsNullOrEmpty(author)) return;

            // 移除相同的作者（如果存在）
            _recentAuthors.Remove(author);

            // 添加到列表开头
            _recentAuthors.Insert(0, author);

            // 限制最近作者数量
            if (_recentAuthors.Count > MaxRecentAuthors)
            {
                _recentAuthors.RemoveAt(_recentAuthors.Count - 1);
            }

            // 保存更新后的列表
            SaveRecentAuthors();
        }

        // 加载扫描到的作者
        private void LoadScannedAuthors()
        {
            try
            {
                string appDataPath = FileUtility.GetAppDataDirectory();
                string scannedAuthorsFile = Path.Combine(appDataPath, "ScannedAuthors.txt");
                
                if (File.Exists(scannedAuthorsFile))
                {
                    _scannedAuthors = FileUtility.ReadLinesFromFile(scannedAuthorsFile);
                }
            }
            catch (Exception ex)
            {
                // 忽略错误，使用空的扫描作者列表
                _scannedAuthors = new List<string>();
                Debug.WriteLine("加载扫描作者时出错: " + ex.Message);
            }
        }

        // 保存扫描到的作者
        private void SaveScannedAuthors()
        {
            try
            {
                string appDataPath = FileUtility.GetAppDataDirectory();
                string scannedAuthorsFile = Path.Combine(appDataPath, "ScannedAuthors.txt");
                FileUtility.SaveLinesToFile(scannedAuthorsFile, _scannedAuthors);
            }
            catch (Exception ex)
            {
                // 忽略错误
                Debug.WriteLine("保存扫描作者时出错: " + ex.Message);
            }
        }
  }
}