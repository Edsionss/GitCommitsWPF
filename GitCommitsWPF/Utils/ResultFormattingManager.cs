using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Controls;
using Newtonsoft.Json;
using GitCommitsWPF.Models;

namespace GitCommitsWPF.Utils
{
  /// <summary>
  /// 结果格式化管理器，负责管理结果格式化和显示
  /// </summary>
  public class ResultFormattingManager
  {
    private readonly StatisticsManager _statisticsManager;
    private readonly FormattingManager _formattingManager;

    /// <summary>
    /// 初始化结果格式化管理器
    /// </summary>
    /// <param name="statisticsManager">统计管理器</param>
    /// <param name="formattingManager">格式化管理器</param>
    public ResultFormattingManager(StatisticsManager statisticsManager, FormattingManager formattingManager)
    {
      _statisticsManager = statisticsManager ?? throw new ArgumentNullException(nameof(statisticsManager));
      _formattingManager = formattingManager ?? throw new ArgumentNullException(nameof(formattingManager));
    }

    /// <summary>
    /// 更新格式化结果文本框
    /// </summary>
    /// <param name="formattedResultTextBox">结果文本框控件</param>
    /// <param name="allCommits">所有提交记录</param>
    /// <param name="selectedFields">选择的字段</param>
    /// <param name="enableStats">是否启用统计</param>
    /// <param name="statsByAuthor">是否按作者统计</param>
    /// <param name="statsByRepo">是否按仓库统计</param>
    /// <param name="statsByDate">是否按日期统计</param>
    /// <param name="formatText">格式化模板文本</param>
    /// <param name="showRepeatedRepoNames">是否显示重复的仓库名称</param>
    public void UpdateFormattedResultTextBox(
        TextBox formattedResultTextBox,
        List<CommitInfo> allCommits,
        List<string> selectedFields,
        bool enableStats,
        bool statsByAuthor,
        bool statsByRepo,
        bool statsByDate,
        string formatText,
        bool showRepeatedRepoNames)
    {
      if (formattedResultTextBox == null)
        return;

      // 生成统计数据(如果启用)
      var statsOutput = new StringBuilder();

      if (enableStats)
      {
        statsOutput.AppendLine("\n======== 提交统计 ========\n");

        // 使用StatisticsManager生成统计信息
        _statisticsManager.GenerateStats(
            allCommits,
            statsOutput,
            statsByAuthor,
            statsByRepo,
            statsByDate);

        statsOutput.AppendLine("\n==========================\n");
      }

      string formattedContent = string.Empty;

      // 应用自定义格式
      if (!string.IsNullOrEmpty(formatText))
      {
        // 使用FormattingManager格式化提交记录
        string formattedOutput = _formattingManager.FormatCommits(
            allCommits,
            formatText,
            showRepeatedRepoNames);

        // 合并统计和格式化输出
        formattedContent = _formattingManager.CombineOutput(statsOutput.ToString(), formattedOutput, enableStats);
      }
      else
      {
        // 如果没有指定格式，使用JSON格式
        var filteredCommits = new List<CommitInfo>();
        foreach (var commit in allCommits)
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

    /// <summary>
    /// 创建用于更新文本框的委托
    /// </summary>
    /// <param name="formattedResultTextBox">结果文本框控件</param>
    /// <returns>更新文本框的委托</returns>
    public Action<TextBox> CreateUpdateTextBoxDelegate(TextBox formattedResultTextBox)
    {
      return (textBox) =>
      {
        if (formattedResultTextBox != null)
        {
          formattedResultTextBox.Text = textBox.Text;
        }
      };
    }

    /// <summary>
    /// 创建用于更新文本框的委托，用于格式化特定内容
    /// </summary>
    /// <param name="formattedResultTextBox">结果文本框控件</param>
    /// <param name="allCommits">提交列表</param>
    /// <param name="settings">格式化设置</param>
    /// <returns>更新文本框的委托</returns>
    public Action<TextBox> CreateUpdateTextBoxDelegate(
        TextBox formattedResultTextBox,
        List<CommitInfo> allCommits,
        FormattingSettings settings)
    {
      return (textBox) =>
      {
        if (formattedResultTextBox != null)
        {
          // 使用ResultFormattingManager格式化并更新文本
          UpdateFormattedResultTextBox(
              formattedResultTextBox,
              allCommits,
              settings.SelectedFields,
              settings.EnableStats,
              settings.StatsByAuthor,
              settings.StatsByRepo,
              settings.StatsByDate,
              settings.FormatText,
              settings.ShowRepeatedRepoNames);
        }
      };
    }

    /// <summary>
    /// 格式化设置
    /// </summary>
    public class FormattingSettings
    {
      public List<string> SelectedFields { get; set; }
      public bool EnableStats { get; set; }
      public bool StatsByAuthor { get; set; }
      public bool StatsByRepo { get; set; }
      public bool StatsByDate { get; set; }
      public string FormatText { get; set; }
      public bool ShowRepeatedRepoNames { get; set; }
    }
  }
}