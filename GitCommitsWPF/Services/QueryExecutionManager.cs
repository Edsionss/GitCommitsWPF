using System;
using System.Collections.Generic;
using System.IO;
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
    private readonly ResultFormattingManager _resultFormattingManager;

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
        DataGridManager dataGridManager,
        ResultFormattingManager resultFormattingManager)
    {
      _outputManager = outputManager;
      _gitOperationsManager = gitOperationsManager;
      _dialogManager = dialogManager;
      _statisticsManager = statisticsManager;
      _formattingManager = formattingManager;
      _dataGridManager = dataGridManager;
      _resultFormattingManager = resultFormattingManager;
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
        Func<List<string>> getSelectedFieldsFunc,
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
      // 在开始新查询前，清空之前的数据，防止不同作者数据混合
      _allCommits.Clear();
      _filteredCommits.Clear();

      // 强制清理内存，确保没有残留引用
      GC.Collect();
      GC.WaitForPendingFinalizers();

      // 设置运行状态
      _isRunning = true;
      // 确保GitOperationsManager也是清理状态
      _gitOperationsManager.IsRunning = true;

      // 设置UI状态
      disableStartButton();
      enableStopButton();

      // 记录开始时间
      DateTime startTime = DateTime.Now;

      try
      {
        // 检查路径是否为空
        if (string.IsNullOrWhiteSpace(pathsText))
        {
          _dialogManager.ShowCustomMessageBox("错误", "请先输入要扫描的Git仓库路径", false);
          return false;
        }

        // 额外的路径验证和格式化 - 清除空路径和重复
        var paths = pathsText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (paths.Count == 0)
        {
          _dialogManager.ShowCustomMessageBox("错误", "请先输入有效的Git仓库路径", false);
          return false;
        }

        // 检查作者筛选参数是否合法
        if (!string.IsNullOrEmpty(authorFilter))
        {
          var authorPatterns = authorFilter.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
              .Select(p => p.Trim())
              .Where(p => !string.IsNullOrEmpty(p))
              .ToArray();

          if (authorPatterns.Length == 0)
          {
            _outputManager.OutputWarning("作者关键词筛选参数为空，将忽略作者关键词筛选");
            authorFilter = string.Empty;
          }
        }

        // 先添加一个分隔线
        _outputManager.AddSeparator();

        // 输出扫描配置信息
        _outputManager.OutputHighlight("===== 开始Git提交扫描 =====");
        _outputManager.OutputHighlight("扫描配置:");

        // 输出路径信息 - 使用处理后的paths
        _outputManager.OutputInfo($"- 扫描路径数量: {paths.Count}");

        // 如果路径超过5个，只显示前5个，然后显示省略数量
        if (paths.Count <= 5)
        {
          foreach (var path in paths)
          {
            _outputManager.OutputInfo($"  - {path}");
          }
        }
        else
        {
          for (int i = 0; i < 5; i++)
          {
            _outputManager.OutputInfo($"  - {paths[i]}");
          }
          _outputManager.OutputInfo($"  - ...以及其他 {paths.Count - 5} 个路径");
        }

        // 输出时间范围信息
        string timeRangeDesc = GetTimeRangeDescription(timeRange, startDate, endDate);
        _outputManager.OutputInfo($"- 时间范围: {timeRangeDesc}");

        // 输出作者信息
        _outputManager.OutputInfo($"- 作者: {(string.IsNullOrEmpty(author) ? "所有作者" : author)}");
        if (!string.IsNullOrEmpty(authorFilter))
        {
          _outputManager.OutputInfo($"- 作者过滤: {authorFilter}");
        }

        // 输出其他配置
        _outputManager.OutputInfo($"- 验证Git路径: {(verifyGitPaths ? "是" : "否")}");
        _outputManager.OutputInfo($"- 启用统计: {(getEnableStats() ? "是" : "否")}");
        if (getEnableStats())
        {
          _outputManager.OutputInfo($"  - 按作者统计: {(getStatsByAuthor() ? "是" : "否")}");
          _outputManager.OutputInfo($"  - 按仓库统计: {(getStatsByRepo() ? "是" : "否")}");
          _outputManager.OutputInfo($"  - 按日期统计: {(getStatsByDate() ? "是" : "否")}");
        }

        _outputManager.OutputHighlight("===== 开始执行扫描 =====");

        // 异步执行查询 - 使用验证后的pathsText
        await CollectGitCommits(
            string.Join(Environment.NewLine, paths),
            timeRange,
            startDate,
            endDate,
            author,
            authorFilter,
            verifyGitPaths);

        // 检查是否提前停止
        if (!_isRunning)
        {
          _outputManager.OutputWarning("查询已手动停止");
          return false;
        }

        // 计算扫描用时
        TimeSpan duration = DateTime.Now - startTime;
        string durationText = FormatDuration(duration);

        // 输出扫描完成和用时信息
        _outputManager.OutputSuccess($"===== 扫描完成，用时: {durationText} =====");

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
        _outputManager.OutputSuccess(string.Format("===== 扫描完成，找到 {0} {1}，总用时: {2} =====，点击结果页签查看",
            commitCount, commitText, durationText));

        return true;
      }
      catch (Exception ex)
      {
        _dialogManager.ShowCustomMessageBox("错误", string.Format("执行过程中发生错误：{0}", ex.Message), false);
        _outputManager.OutputError($"执行过程中发生错误: {ex.Message}");
        // 详细记录异常堆栈，便于调试
        _outputManager.OutputError($"异常详情: {ex}");
        return false;
      }
      finally
      {
        _isRunning = false;
        // 确保GitOperationsManager也停止运行
        _gitOperationsManager.IsRunning = false;
        // 禁用停止按钮，启用开始按钮
        disableStopButton();
        enableStartButton();
        _outputManager.HideProgressBar();
      }
    }

    /// <summary>
    /// 获取时间范围的描述
    /// </summary>
    private string GetTimeRangeDescription(string timeRange, DateTime? startDate, DateTime? endDate)
    {
      switch (timeRange)
      {
        case "day":
          // 对于"今天"选项，明确说明包含全天提交
          return $"今天 ({DateTime.Today.ToString("yyyy-MM-dd")} 全天)";
        case "week":
          // 计算本周一和本周日
          DateTime today = DateTime.Today;
          int daysUntilMonday = ((int)today.DayOfWeek == 0 ? 7 : (int)today.DayOfWeek) - 1;
          DateTime monday = today.AddDays(-daysUntilMonday);
          DateTime sunday = monday.AddDays(6);
          return $"本周 ({monday.ToString("yyyy-MM-dd")} 至 {sunday.ToString("yyyy-MM-dd")} 全天)";
        case "month":
          // 计算当月第一天和最后一天
          DateTime currentDate = DateTime.Today;
          DateTime firstDayOfMonth = new DateTime(currentDate.Year, currentDate.Month, 1);
          DateTime lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);
          return $"本月 ({firstDayOfMonth.ToString("yyyy-MM-dd")} 至 {lastDayOfMonth.ToString("yyyy-MM-dd")} 全天)";
        case "year":
          DateTime oneYearAgo = DateTime.Today.AddYears(-1);
          return $"近一年 ({oneYearAgo.ToString("yyyy-MM-dd")} 至 {DateTime.Today.ToString("yyyy-MM-dd")} 全天)";
        case "custom":
          if (startDate.HasValue && endDate.HasValue)
          {
            return $"自定义 ({startDate.Value.ToString("yyyy-MM-dd")} 至 {endDate.Value.ToString("yyyy-MM-dd")} 全天)";
          }
          else if (startDate.HasValue)
          {
            return $"自定义 ({startDate.Value.ToString("yyyy-MM-dd")} 至 今天 全天)";
          }
          else if (endDate.HasValue)
          {
            return $"自定义 (全部 至 {endDate.Value.ToString("yyyy-MM-dd")} 全天)";
          }
          return "自定义";
        default: // "all"
          return "所有时间";
      }
    }

    /// <summary>
    /// 格式化时间间隔
    /// </summary>
    private string FormatDuration(TimeSpan duration)
    {
      // 格式化为 时:分:秒 格式，如果时间小于1小时则只显示分:秒
      if (duration.TotalHours >= 1)
      {
        return $"{(int)duration.TotalHours}小时{duration.Minutes}分{duration.Seconds}秒";
      }
      else if (duration.TotalMinutes >= 1)
      {
        return $"{duration.Minutes}分{duration.Seconds}秒";
      }
      else
      {
        return $"{duration.Seconds}秒";
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
      // 清空之前的结果，确保数据干净
      _allCommits.Clear();
      _filteredCommits.Clear();

      // 获取路径列表
      var paths = pathsText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList();

      // 使用TimeRangeManager转换时间参数
      var (since, until) = TimeRangeManager.ConvertToGitTimeArgs(timeRange, startDate, endDate);

      // 如果until为空，设置为明天的日期，确保包含今天的提交
      if (string.IsNullOrEmpty(until))
      {
        until = DateTime.Now.AddDays(1).ToString("yyyy-MM-dd");
      }

      // 记录时间范围信息，方便调试
      _outputManager.OutputHighlight($"时间范围设置 - 开始: {(string.IsNullOrEmpty(since) ? "全部" : since)}，结束: {until}");

      // 输出当前筛选条件，帮助调试
      _outputManager.UpdateOutput($"筛选条件 - 作者: {(string.IsNullOrEmpty(author) ? "全部" : author)}");
      if (!string.IsNullOrEmpty(authorFilter))
      {
        _outputManager.UpdateOutput($"筛选条件 - 作者关键词: {authorFilter}");
      }

      try
      {
        // 使用GitOperationsManager异步收集Git提交
        _allCommits = await _gitOperationsManager.CollectGitCommitsAsync(paths, since, until, author, authorFilter, verifyGitPaths);

        // 检查结果是否为空
        if (_allCommits == null || _allCommits.Count == 0)
        {
          _outputManager.OutputWarning("没有找到符合条件的提交记录");
          _allCommits = new List<CommitInfo>();
          return;
        }

        // 对整个结果集进行日期验证和再次排序
        // 记录时间戳
        DateTime startTime = DateTime.Now;
        _outputManager.OutputInfo("正在对所有提交记录再次验证时间范围...");

        // 解析时间范围，用于验证提交日期
        DateTime? sinceDate = null;
        if (!string.IsNullOrEmpty(since))
        {
          if (DateTime.TryParse(since, out DateTime parsedSinceDate))
          {
            sinceDate = parsedSinceDate;
          }
        }

        DateTime? untilDate = null;
        if (!string.IsNullOrEmpty(until))
        {
          if (DateTime.TryParse(until, out DateTime parsedUntilDate))
          {
            untilDate = parsedUntilDate;
          }
        }

        // 再次过滤结果，确保提交日期在指定范围内
        _allCommits = _allCommits.Where(commit =>
        {
          if (DateTime.TryParse(commit.Date, out DateTime commitDate))
          {
            // 检查开始日期
            if (sinceDate.HasValue && commitDate < sinceDate.Value)
            {
              // 提交日期早于开始日期，排除
              return false;
            }

            // 检查结束日期 - until日期是不包含的(TimeRangeManager已经将传入的日期+1天)
            if (untilDate.HasValue && commitDate >= untilDate.Value)
            {
              // 提交日期大于等于结束日期，排除
              return false;
            }

            return true;
          }

          // 如果无法解析日期，保留该提交
          return true;
        }).ToList();

        // 再次按仓库分组排序
        var commitsByRepo = _allCommits.GroupBy(c => c.RepoPath ?? "Unknown").ToList();
        _outputManager.OutputInfo($"提交记录按 {commitsByRepo.Count} 个仓库分组");

        // 创建一个新的排序后的列表
        var sortedCommits = new List<CommitInfo>();

        // 对每个仓库内部的提交按日期排序
        foreach (var repoGroup in commitsByRepo)
        {
          // 按日期降序排序
          var repoCommits = repoGroup.OrderByDescending(c =>
          {
            if (DateTime.TryParse(c.Date, out DateTime date))
              return date;
            return DateTime.MinValue;
          }).ToList();

          // 添加到结果列表
          sortedCommits.AddRange(repoCommits);
        }

        // 用排序后的列表替换原来的列表
        _allCommits = sortedCommits;

        // 显示处理时间
        TimeSpan processingTime = DateTime.Now - startTime;
        _outputManager.OutputSuccess($"所有提交记录验证和排序完成，处理 {_allCommits.Count} 条记录，用时 {processingTime.TotalSeconds:F2} 秒");
      }
      catch (Exception ex)
      {
        _outputManager.OutputError($"收集Git提交时发生错误: {ex.Message}");
        // 确保结果不为null
        _allCommits = new List<CommitInfo>();
      }
    }

    /// <summary>
    /// 显示查询结果
    /// </summary>
    private void ShowResults(
        Func<List<string>> getSelectedFieldsFunc,
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
          // 查找FormattedResultTextBox
          var formattedResultTextBox = Application.Current.Windows.OfType<Window>()
              .FirstOrDefault(w => w.IsActive)?.FindName("FormattedResultTextBox") as TextBox;

          if (formattedResultTextBox != null)
          {
            // 清空文本框
            formattedResultTextBox.Text = string.Empty;
          }
          else
          {
            // 清空结果页签的内容（回退机制）
            updateFormattedResultTextBox(new TextBox { Text = string.Empty });
          }

          // 使用DataGridManager更新数据源
          _dataGridManager.UpdateDataSource(null);
          _filteredCommits.Clear(); // 清空筛选结果
        });
        return;
      }

      // 获取选择的字段和其他配置项
      List<string> selectedFields = new List<string>();
      bool enableStats = false;
      bool statsByAuthor = false;
      bool statsByRepo = false;
      bool statsByDate = false;
      string format = "";
      bool showRepeatedRepoNames = false;

      // 确保每个提交记录的仓库信息正确设置
      foreach (var commit in _allCommits)
      {
        if (string.IsNullOrEmpty(commit.Repository) && !string.IsNullOrEmpty(commit.RepoPath))
        {
          commit.Repository = Path.GetFileName(commit.RepoPath);
        }

        if (string.IsNullOrEmpty(commit.RepoFolder) && !string.IsNullOrEmpty(commit.RepoPath))
        {
          commit.RepoFolder = Path.GetFileName(commit.RepoPath);
        }
      }

      // 在UI线程上获取所有配置项
      Application.Current.Dispatcher.Invoke(() =>
      {
        selectedFields = getSelectedFieldsFunc();
        enableStats = getEnableStats();
        statsByAuthor = getStatsByAuthor();
        statsByRepo = getStatsByRepo();
        statsByDate = getStatsByDate();
        format = getFormatText();
        showRepeatedRepoNames = getShowRepeatedRepoNames();

        // 然后更新数据源
        _dataGridManager.UpdateDataSource(_allCommits);
        _filteredCommits = new List<CommitInfo>(_allCommits); // 重置筛选结果
        clearSearchFilter(); // 清空搜索框
      });

      // 格式化内容并显示在结果页签中
      string formattedContent = string.Empty;

      // 生成统计数据(如果启用)
      var statsOutput = new StringBuilder();

      if (enableStats)
      {
        statsOutput.AppendLine("\n======== 提交统计 ========\n");

        // 使用StatisticsManager生成统计信息
        _statisticsManager.GenerateStats(
            _allCommits,
            statsOutput,
            statsByAuthor,
            statsByRepo,
            statsByDate);

        statsOutput.AppendLine("\n==========================\n");
      }

      if (!string.IsNullOrEmpty(format))
      {
        // 使用FormattingManager格式化提交记录
        string formattedOutput = _formattingManager.FormatCommits(
            _allCommits,
            format,
            showRepeatedRepoNames);

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
        string jsonOutput = JsonConvert.SerializeObject(filteredCommits, Formatting.Indented);
        formattedContent = _formattingManager.CombineOutput(statsOutput.ToString(), jsonOutput, enableStats);
      }

      // 在UI线程上更新结果页签
      Application.Current.Dispatcher.Invoke(() =>
      {
        // 查找FormattedResultTextBox
        var formattedResultTextBox = Application.Current.Windows.OfType<Window>()
            .FirstOrDefault(w => w.IsActive)?.FindName("FormattedResultTextBox") as TextBox;

        if (formattedResultTextBox != null)
        {
          // 使用ResultFormattingManager直接格式化和更新文本框
          _resultFormattingManager.UpdateFormattedResultTextBox(
              formattedResultTextBox,
              _allCommits,
              selectedFields,
              enableStats,
              statsByAuthor,
              statsByRepo,
              statsByDate,
              format,
              showRepeatedRepoNames);
        }
        else
        {
          // 回退到原来的方式
          updateFormattedResultTextBox(new TextBox { Text = formattedContent });
        }
      });
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