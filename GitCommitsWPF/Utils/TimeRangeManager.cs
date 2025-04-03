using System;
using System.Windows.Controls;

namespace GitCommitsWPF.Utils
{
  /// <summary>
  /// 时间范围管理器，用于管理时间范围选择和相关功能
  /// </summary>
  public class TimeRangeManager
  {
    private ComboBox _timeRangeComboBox;
    private DatePicker _startDatePicker;
    private DatePicker _endDatePicker;

    /// <summary>
    /// 初始化时间范围管理器
    /// </summary>
    /// <param name="timeRangeComboBox">时间范围下拉框</param>
    /// <param name="startDatePicker">开始日期选择器</param>
    /// <param name="endDatePicker">结束日期选择器</param>
    public void Initialize(ComboBox timeRangeComboBox, DatePicker startDatePicker, DatePicker endDatePicker)
    {
      _timeRangeComboBox = timeRangeComboBox;
      _startDatePicker = startDatePicker;
      _endDatePicker = endDatePicker;

      // 设置日期选择器为当前日期
      _startDatePicker.SelectedDate = DateTime.Today;
      _endDatePicker.SelectedDate = DateTime.Today;

      // 设置默认选择
      _timeRangeComboBox.SelectedIndex = 0; // 默认选择'所有时间'

      // 注册事件处理器
      _timeRangeComboBox.SelectionChanged += TimeRangeComboBox_SelectionChanged;

      // 初始时根据默认选择项更新日期选择器状态
      UpdateDatePickersState();
    }

    /// <summary>
    /// 时间范围下拉框选择变更事件处理
    /// </summary>
    private void TimeRangeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      UpdateDatePickersState();
    }

    /// <summary>
    /// 更新日期选择器状态
    /// </summary>
    private void UpdateDatePickersState()
    {
      var selectedItem = _timeRangeComboBox.SelectedItem as ComboBoxItem;
      if (selectedItem != null)
      {
        var timeRange = selectedItem.Tag?.ToString();

        // 更新日期选择器的状态和值
        switch (timeRange)
        {
          case "day":
            _startDatePicker.SelectedDate = DateTime.Today;
            _endDatePicker.SelectedDate = DateTime.Today;
            _startDatePicker.IsEnabled = false;
            _endDatePicker.IsEnabled = false;
            break;
          case "week":
            // 计算本周一和本周日
            DateTime today = DateTime.Today;
            int daysUntilMonday = ((int)today.DayOfWeek == 0 ? 7 : (int)today.DayOfWeek) - 1;
            DateTime monday = today.AddDays(-daysUntilMonday);
            DateTime sunday = monday.AddDays(6);

            _startDatePicker.SelectedDate = monday;
            _endDatePicker.SelectedDate = sunday;
            _startDatePicker.IsEnabled = false;
            _endDatePicker.IsEnabled = false;
            break;
          case "month":
            _startDatePicker.SelectedDate = DateTime.Today.AddMonths(-1);
            _endDatePicker.SelectedDate = DateTime.Today;
            _startDatePicker.IsEnabled = false;
            _endDatePicker.IsEnabled = false;
            break;
          case "year":
            _startDatePicker.SelectedDate = DateTime.Today.AddYears(-1);
            _endDatePicker.SelectedDate = DateTime.Today;
            _startDatePicker.IsEnabled = false;
            _endDatePicker.IsEnabled = false;
            break;
          case "custom":
            // 对于自定义范围，保持已选日期并启用控件
            _startDatePicker.IsEnabled = true;
            _endDatePicker.IsEnabled = true;
            break;
          default: // "all"
            // 设置为较早的日期，实际不会使用
            _startDatePicker.SelectedDate = null;
            _endDatePicker.SelectedDate = DateTime.Today;
            _startDatePicker.IsEnabled = false;
            _endDatePicker.IsEnabled = false;
            break;
        }
      }
    }

    /// <summary>
    /// 获取选定的时间范围
    /// </summary>
    /// <returns>时间范围标识</returns>
    public string GetSelectedTimeRange()
    {
      var selectedItem = _timeRangeComboBox.SelectedItem as ComboBoxItem;
      return selectedItem?.Tag?.ToString() ?? "all";
    }

    /// <summary>
    /// 获取开始日期
    /// </summary>
    /// <returns>开始日期</returns>
    public DateTime? GetStartDate()
    {
      return _startDatePicker.SelectedDate;
    }

    /// <summary>
    /// 获取结束日期
    /// </summary>
    /// <returns>结束日期</returns>
    public DateTime? GetEndDate()
    {
      return _endDatePicker.SelectedDate;
    }

    /// <summary>
    /// 将时间范围转换为Git命令参数
    /// </summary>
    /// <param name="timeRange">时间范围</param>
    /// <param name="startDate">开始日期</param>
    /// <param name="endDate">结束日期</param>
    /// <returns>包含since和until参数的元组</returns>
    public static (string since, string until) ConvertToGitTimeArgs(string timeRange, DateTime? startDate, DateTime? endDate)
    {
      string since = string.Empty;
      string until = string.Empty;

      // 根据时间范围设置参数
      switch (timeRange)
      {
        case "day":
          // 使用指定的startDate，如果为null则使用当天
          since = startDate?.ToString("yyyy-MM-dd") ?? DateTime.Today.ToString("yyyy-MM-dd");
          // 使用指定的endDate，如果为null则使用当天
          until = (endDate?.AddDays(1) ?? DateTime.Today.AddDays(1)).ToString("yyyy-MM-dd");
          break;
        case "week":
          if (startDate.HasValue && endDate.HasValue)
          {
            // 使用用户选择的日期范围
            since = startDate.Value.ToString("yyyy-MM-dd");
            until = endDate.Value.AddDays(1).ToString("yyyy-MM-dd"); // 增加一天以包括结束日期
          }
          else
          {
            // 计算本周一和下周一
            DateTime today = DateTime.Today;
            int daysUntilMonday = ((int)today.DayOfWeek == 0 ? 7 : (int)today.DayOfWeek) - 1;
            DateTime monday = today.AddDays(-daysUntilMonday);
            DateTime nextMonday = monday.AddDays(7);

            since = monday.ToString("yyyy-MM-dd");
            until = nextMonday.ToString("yyyy-MM-dd");
          }
          break;
        case "month":
          // 使用指定的startDate，如果为null则使用1个月前
          since = startDate?.ToString("yyyy-MM-dd") ?? DateTime.Today.AddMonths(-1).ToString("yyyy-MM-dd");
          // 使用指定的endDate，如果为null则使用当天
          until = (endDate?.AddDays(1) ?? DateTime.Today.AddDays(1)).ToString("yyyy-MM-dd");
          break;
        case "year":
          // 使用指定的startDate，如果为null则使用1年前
          since = startDate?.ToString("yyyy-MM-dd") ?? DateTime.Today.AddYears(-1).ToString("yyyy-MM-dd");
          // 使用指定的endDate，如果为null则使用当天
          until = (endDate?.AddDays(1) ?? DateTime.Today.AddDays(1)).ToString("yyyy-MM-dd");
          break;
        case "custom":
          if (startDate.HasValue)
          {
            since = startDate.Value.ToString("yyyy-MM-dd");
          }
          if (endDate.HasValue)
          {
            // Git的--until参数是不包含当天的，所以我们需要加1天
            until = endDate.Value.AddDays(1).ToString("yyyy-MM-dd");
          }
          break;
        default: // "all"
          break;
      }

      return (since, until);
    }
  }
}