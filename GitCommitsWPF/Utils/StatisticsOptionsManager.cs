using System;
using System.Windows.Controls;

namespace GitCommitsWPF.Utils
{
  /// <summary>
  /// 统计选项管理器，用于管理统计相关的选项
  /// </summary>
  public class StatisticsOptionsManager
  {
    private CheckBox _enableStatsCheckBox;
    private CheckBox _statsByAuthorCheckBox;
    private CheckBox _statsByRepoCheckBox;
    private CheckBox _statsByDateCheckBox;

    /// <summary>
    /// 初始化统计选项管理器
    /// </summary>
    /// <param name="enableStatsCheckBox">启用统计复选框</param>
    /// <param name="statsByAuthorCheckBox">按作者统计复选框</param>
    /// <param name="statsByRepoCheckBox">按仓库统计复选框</param>
    /// <param name="statsByDateCheckBox">按日期统计复选框</param>
    public void Initialize(
        CheckBox enableStatsCheckBox,
        CheckBox statsByAuthorCheckBox,
        CheckBox statsByRepoCheckBox,
        CheckBox statsByDateCheckBox)
    {
      _enableStatsCheckBox = enableStatsCheckBox;
      _statsByAuthorCheckBox = statsByAuthorCheckBox;
      _statsByRepoCheckBox = statsByRepoCheckBox;
      _statsByDateCheckBox = statsByDateCheckBox;

      // 绑定启用统计复选框状态变更事件
      _enableStatsCheckBox.Checked += EnableStatsCheckBox_CheckedChanged;
      _enableStatsCheckBox.Unchecked += EnableStatsCheckBox_CheckedChanged;

      // 初始化状态
      UpdateStatsOptionState();
    }

    /// <summary>
    /// 启用统计复选框状态变更事件处理
    /// </summary>
    private void EnableStatsCheckBox_CheckedChanged(object sender, System.Windows.RoutedEventArgs e)
    {
      UpdateStatsOptionState();
    }

    /// <summary>
    /// 更新统计选项状态
    /// </summary>
    private void UpdateStatsOptionState()
    {
      bool isEnabled = _enableStatsCheckBox.IsChecked == true;

      // 更新子选项的启用状态
      _statsByAuthorCheckBox.IsEnabled = isEnabled;
      _statsByRepoCheckBox.IsEnabled = isEnabled;
      _statsByDateCheckBox.IsEnabled = isEnabled;
    }

    /// <summary>
    /// 获取是否启用统计
    /// </summary>
    /// <returns>是否启用统计</returns>
    public bool IsStatsEnabled()
    {
      return _enableStatsCheckBox.IsChecked == true;
    }

    /// <summary>
    /// 获取是否按作者统计
    /// </summary>
    /// <returns>是否按作者统计</returns>
    public bool IsStatsByAuthorEnabled()
    {
      return _statsByAuthorCheckBox.IsChecked == true;
    }

    /// <summary>
    /// 获取是否按仓库统计
    /// </summary>
    /// <returns>是否按仓库统计</returns>
    public bool IsStatsByRepoEnabled()
    {
      return _statsByRepoCheckBox.IsChecked == true;
    }

    /// <summary>
    /// 获取是否按日期统计
    /// </summary>
    /// <returns>是否按日期统计</returns>
    public bool IsStatsByDateEnabled()
    {
      return _statsByDateCheckBox.IsChecked == true;
    }

    /// <summary>
    /// 设置统计选项状态
    /// </summary>
    /// <param name="enableStats">是否启用统计</param>
    /// <param name="statsByAuthor">是否按作者统计</param>
    /// <param name="statsByRepo">是否按仓库统计</param>
    /// <param name="statsByDate">是否按日期统计</param>
    public void SetStatsOptions(bool enableStats, bool statsByAuthor, bool statsByRepo, bool statsByDate)
    {
      _enableStatsCheckBox.IsChecked = enableStats;
      _statsByAuthorCheckBox.IsChecked = statsByAuthor;
      _statsByRepoCheckBox.IsChecked = statsByRepo;
      _statsByDateCheckBox.IsChecked = statsByDate;

      // 更新子选项的启用状态
      UpdateStatsOptionState();
    }
  }
}