using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using GitCommitsWPF.Models;
using GitCommitsWPF.Utils;
using Newtonsoft.Json;
using Formatting = Newtonsoft.Json.Formatting;

namespace GitCommitsWPF.Services
{
  public class QueryExecutionManager
  {
    private readonly OutputManager _outputManager;
    private readonly GitOperationsManager _gitOperationsManager;
    private readonly DialogManager _dialogManager;
    private readonly StatisticsManager _statisticsManager;
    private readonly FormattingManager _formattingManager;
    private readonly DataGridManager _dataGridManager;

    private List<CommitInfo> _allCommits = new List<CommitInfo>();
    private List<CommitInfo> _filteredCommits = new List<CommitInfo>();
    private bool _isRunning = false;

    public List<CommitInfo> AllCommits => _allCommits;
    public List<CommitInfo> FilteredCommits => _filteredCommits;
    public bool IsRunning
    {
      get => _isRunning;
      set
      {
        _isRunning = value;
        if (!_isRunning)
        {
          _gitOperationsManager.IsRunning = false;
        }
      }
    }

    public QueryExecutionManager(
        OutputManager outputManager,
        GitOperationsManager gitOperationsManager,
        DialogManager dialogManager,
        StatisticsManager statisticsManager,
        FormattingManager formattingManager,
        DataGridManager dataGridManager)
    {
      _outputManager = outputManager;
      _gitOperationsManager = gitOperationsManager;
      _dialogManager = dialogManager;
      _statisticsManager = statisticsManager;
      _formattingManager = formattingManager;
      _dataGridManager = dataGridManager;
    }

    /// <summary>
    /// 执行Git提交查询
    /// </summary>
    public async Task<bool> ExecuteQuery(
        string pathsText,
        string timeRange,
        DateTime? startDate,
        DateTime? endDate,
        string author,
        string authorFilter,
        bool verifyGitPaths,
        Func<bool, bool, bool, bool, bool, bool, bool, List<string>> getSelectedFieldsFunc,
        Func<string> getFormatText,
        Func<bool> getShowRepeatedRepoNames,
        Func<bool> getEnableStats,
        Func<bool> getStatsByAuthor,
        Func<bool> getStatsByRepo,
        Func<bool> getStatsByDate,
        Action<TextBox> updateFormattedResultTextBox,
        Action disableStartButton,
        Action enableStopButton,
        Action disableStopButton,
        Action enableStartButton,
        Action setSaveButtonEnabled,
        Action clearSearchFilter)
    {
      // 检查路径是否为空
      if (string.IsNullOrWhiteSpace(pathsText))
      {
        _dialogManager.ShowCustomMessageBox("错误", "请先输入要扫描的Git仓库路径", false);
        return false;
      }

      // 清空现有的结果
      _allCommits.Clear();
      _filteredCommits.Clear();
      _isRunning = true;

      // 禁用开始按钮，启用停止按钮
      disableStartButton();
      enableStopButton();

      try
      {
        // 先添加一个分隔线
        _outputManager.AddSeparator();

        // 异步执行查询
        await CollectGitCommits(
            pathsText,
            timeRange,
            startDate,
            endDate,
            author,
            authorFilter,
            verifyGitPaths);

        // 查询完成后，显示结果并更新UI
        ShowResults(
            getSelectedFieldsFunc,
            getFormatText,
            getShowRepeatedRepoNames,
            getEnableStats,
            getStatsByAuthor,
            getStatsByRepo,
            getStatsByDate,
            updateFormattedResultTextBox,
            clearSearchFilter);

        // 更新完成消息
        setSaveButtonEnabled();
        int commitCount = _allCommits.Count;
        string commitText = commitCount == 1 ? "条提交记录" : "条提交记录";
        _outputManager.UpdateOutput(string.Format("===== 扫描完成，找到 {0} {1} =====，点击结果页签查看", commitCount, commitText));

        return true;
      }
      catch (Exception ex)
      {
        _dialogManager.ShowCustomMessageBox("错误", string.Format("执行过程中发生错误：{0}", ex.Message), false);
        return false;
      }
      finally
      {
        _isRunning = false;
        // 禁用停止按钮，启用开始按钮
        disableStopButton();
        enableStartButton();
        _outputManager.HideProgressBar();
      }
    }

    /// <summary>
    /// 收集Git提交信息
    /// </summary>
    private async Task CollectGitCommits(
        string pathsText,
        string timeRange,
        DateTime? startDate,
        DateTime? endDate,
        string author,
        string authorFilter,
        bool verifyGitPaths)
    {
      // 获取路径列表
      var paths = pathsText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList();

      // 计算日期范围
      string since = "";
      string until = DateTime.Now.AddDays(1).ToString("yyyy-MM-dd"); // 设置到明天来包含今天的提交

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
          if (startDate.HasValue && endDate.HasValue)
          {
            since = startDate.Value.ToString("yyyy-MM-dd");
            // 设置结束日期为第二天，以包含结束当天的提交
            until = endDate.Value.AddDays(1).ToString("yyyy-MM-dd");
          }
          break;
        case "all":
          // 不设置since，从仓库创建开始
          since = "";
          break;
      }

      // 使用GitOperationsManager异步收集Git提交
      _allCommits = await _gitOperationsManager.CollectGitCommitsAsync(paths, since, until, author, authorFilter, verifyGitPaths);
    }

    /// <summary>
    /// 显示查询结果
    /// </summary>
    private void ShowResults(
        Func<bool, bool, bool, bool, bool, bool, bool, List<string>> getSelectedFieldsFunc,
        Func<string> getFormatText,
        Func<bool> getShowRepeatedRepoNames,
        Func<bool> getEnableStats,
        Func<bool> getStatsByAuthor,
        Func<bool> getStatsByRepo,
        Func<bool> getStatsByDate,
        Action<TextBox> updateFormattedResultTextBox,
        Action clearSearchFilter)
    {
      // 检查是否已手动停止
      if (!_isRunning)
      {
        _outputManager.UpdateOutput("显示结果过程已手动停止");
        return;
      }

      // 筛选后得到的提交记录
      _filteredCommits = new List<CommitInfo>(_allCommits);

      if (_allCommits.Count == 0)
      {
        // 在UI线程上执行
        Application.Current.Dispatcher.Invoke(() =>
        {
          var formattedResultTextBox = updateFormattedResultTextBox;
          if (formattedResultTextBox != null)
          {
            // 清空结果页签的内容
          }
          // 使用DataGridManager更新数据源
          _dataGridManager.UpdateDataSource(null);
          _filteredCommits.Clear(); // 清空筛选结果
        });
        return;
      }

      // 获取选择的字段
      List<string> selectedFields = new List<string>();
      Application.Current.Dispatcher.Invoke(() =>
      {
        selectedFields = getSelectedFieldsFunc(true, true, true, true, true, true, true);

        // 使用DataGridManager更新数据源
        _dataGridManager.UpdateDataSource(_allCommits);
        _filteredCommits = new List<CommitInfo>(_allCommits); // 重置筛选结果
        clearSearchFilter(); // 清空搜索框
      });

      // 生成统计数据(如果启用)
      var statsOutput = new StringBuilder();
      bool enableStats = false;
      Application.Current.Dispatcher.Invoke(() =>
      {
        enableStats = getEnableStats();
      });

      if (enableStats)
      {
        statsOutput.AppendLine("\n======== 提交统计 ========\n");

        // 使用StatisticsManager生成统计信息
        _statisticsManager.GenerateStats(
            _allCommits,
            statsOutput,
            getStatsByAuthor(),
            getStatsByRepo(),
            getStatsByDate());

        statsOutput.AppendLine("\n==========================\n");
      }

      // 应用自定义格式
      string format = "";
      bool showRepeatedRepoNames = false;

      Application.Current.Dispatcher.Invoke(() =>
      {
        format = getFormatText();
        showRepeatedRepoNames = getShowRepeatedRepoNames();
      });

      // 用于保存格式化后的输出内容
      string formattedContent = string.Empty;

      if (!string.IsNullOrEmpty(format))
      {
        // 使用FormattingManager格式化提交记录
        string formattedOutput = _formattingManager.FormatCommits(
            _allCommits,
            format,
            showRepeatedRepoNames);

        // 合并统计和格式化输出
        formattedContent = _formattingManager.CombineOutput(statsOutput.ToString(), formattedOutput, enableStats);

        Application.Current.Dispatcher.Invoke(() =>
        {
          // 在"结果"页签中仅显示格式化的结果，不包含日志输出
          updateFormattedResultTextBox(new TextBox { Text = formattedContent });
        });
      }
      else
      {
        // 如果没有指定格式，显示所有字段
        var filteredCommits = new List<CommitInfo>();
        foreach (var commit in _allCommits)
        {
          // 使用FormattingManager创建过滤后的提交信息
          filteredCommits.Add(_formattingManager.CreateFilteredCommit(commit, selectedFields));
        }

        // 使用JSON格式
        string jsonOutput = JsonConvert.SerializeObject(filteredCommits, Formatting.Indented);
        formattedContent = _formattingManager.CombineOutput(statsOutput.ToString(), jsonOutput, enableStats);

        Application.Current.Dispatcher.Invoke(() =>
        {
          // 在"结果"页签中仅显示格式化的结果，不包含日志输出
          updateFormattedResultTextBox(new TextBox { Text = formattedContent });
        });
      }
    }

    /// <summary>
    /// 生成统计数据并添加到输出字符串
    /// </summary>
    public void GenerateStats(StringBuilder output, bool statsByAuthor, bool statsByRepo, bool statsByDate)
    {
      // 使用StatisticsManager生成统计信息
      _statisticsManager.GenerateStats(
          _allCommits,
          output,
          statsByAuthor,
          statsByRepo,
          statsByDate);
    }
  }
}