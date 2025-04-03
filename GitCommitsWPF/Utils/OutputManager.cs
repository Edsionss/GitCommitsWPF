using System;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;

namespace GitCommitsWPF.Utils
{
  /// <summary>
  /// 日志输出颜色类型
  /// </summary>
  public enum LogColor
  {
    Normal,   // 普通日志
    Success,  // 成功消息
    Warning,  // 警告消息
    Error,    // 错误消息
    Info,     // 信息消息
    Highlight // 高亮消息
  }

  /// <summary>
  /// 输出管理器，负责管理日志输出和进度条更新
  /// </summary>
  public class OutputManager
  {
    private StringBuilder _plainTextContent = new StringBuilder();
    private RichTextBox _resultRichTextBox;
    private TextBox _resultTextBox;
    private ProgressBar _progressBar;
    private Dispatcher _dispatcher;
    private bool _useRichText = false;

    // 日志颜色映射
    private static readonly Brush NormalBrush = Brushes.Black;
    private static readonly Brush SuccessBrush = Brushes.Green;
    private static readonly Brush WarningBrush = Brushes.Orange;
    private static readonly Brush ErrorBrush = Brushes.Red;
    private static readonly Brush InfoBrush = Brushes.Blue;
    private static readonly Brush HighlightBrush = Brushes.Purple;

    /// <summary>
    /// 初始化输出管理器 - 使用普通TextBox
    /// </summary>
    /// <param name="resultTextBox">结果文本框</param>
    /// <param name="progressBar">进度条</param>
    /// <param name="dispatcher">UI线程调度器</param>
    public OutputManager(TextBox resultTextBox, ProgressBar progressBar, Dispatcher dispatcher)
    {
      _resultTextBox = resultTextBox;
      _progressBar = progressBar;
      _dispatcher = dispatcher;
      _useRichText = false;
    }

    /// <summary>
    /// 初始化输出管理器 - 使用RichTextBox
    /// </summary>
    /// <param name="resultRichTextBox">富文本结果框</param>
    /// <param name="progressBar">进度条</param>
    /// <param name="dispatcher">UI线程调度器</param>
    public OutputManager(RichTextBox resultRichTextBox, ProgressBar progressBar, Dispatcher dispatcher)
    {
      _resultRichTextBox = resultRichTextBox;
      _progressBar = progressBar;
      _dispatcher = dispatcher;
      _useRichText = true;
    }

    /// <summary>
    /// 获取输出内容
    /// </summary>
    public string OutputContent => _plainTextContent.ToString();

    /// <summary>
    /// 清空输出内容
    /// </summary>
    public void ClearOutput()
    {
      _plainTextContent.Clear();

      if (_useRichText)
      {
        _dispatcher.BeginInvoke(new Action(() =>
        {
          _resultRichTextBox.Document.Blocks.Clear();
        }));
      }
      else
      {
        UpdateResultTextBox();
      }
    }

    /// <summary>
    /// 添加分隔线
    /// </summary>
    public void AddSeparator()
    {
      string separatorText = "\n===== " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " =====\n";
      _plainTextContent.AppendLine(separatorText);

      if (_useRichText)
      {
        _dispatcher.BeginInvoke(new Action(() =>
        {
          Paragraph para = new Paragraph();
          para.Inlines.Add(new Run(separatorText) { Foreground = HighlightBrush, FontWeight = FontWeights.Bold });
          _resultRichTextBox.Document.Blocks.Add(para);
          _resultRichTextBox.ScrollToEnd();
        }));
      }
      else
      {
        UpdateResultTextBox();
      }
    }

    /// <summary>
    /// 添加新查询分隔线
    /// </summary>
    public void AddNewQuerySeparator()
    {
      string separatorText = "\n===== 开始新查询 " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " =====\n";
      _plainTextContent.AppendLine(separatorText);

      if (_useRichText)
      {
        _dispatcher.BeginInvoke(new Action(() =>
        {
          Paragraph para = new Paragraph();
          para.Inlines.Add(new Run(separatorText) { Foreground = HighlightBrush, FontWeight = FontWeights.Bold });
          _resultRichTextBox.Document.Blocks.Add(para);
          _resultRichTextBox.ScrollToEnd();
        }));
      }
      else
      {
        UpdateResultTextBox();
      }
    }

    /// <summary>
    /// 更新输出内容
    /// </summary>
    /// <param name="message">要添加的消息</param>
    public void UpdateOutput(string message)
    {
      UpdateOutputWithColor(message, LogColor.Normal);
    }

    /// <summary>
    /// 更新输出内容 - 带颜色
    /// </summary>
    /// <param name="message">要添加的消息</param>
    /// <param name="color">日志颜色</param>
    public void UpdateOutputWithColor(string message, LogColor color)
    {
      // 添加时间戳
      string timeStamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
      string formattedMessage = string.Format("[{0}] {1}", timeStamp, message);

      // 保存到纯文本内容中
      _plainTextContent.AppendLine(formattedMessage);

      if (_useRichText)
      {
        _dispatcher.BeginInvoke(new Action(() =>
        {
          Paragraph para = new Paragraph();

          // 根据日志类型设置颜色
          Brush textBrush = GetBrushFromLogColor(color);

          para.Inlines.Add(new Run(formattedMessage) { Foreground = textBrush });
          _resultRichTextBox.Document.Blocks.Add(para);
          _resultRichTextBox.ScrollToEnd();
        }));
      }
      else
      {
        UpdateResultTextBox();
      }
    }

    /// <summary>
    /// 输出成功消息
    /// </summary>
    /// <param name="message">消息内容</param>
    public void OutputSuccess(string message)
    {
      UpdateOutputWithColor(message, LogColor.Success);
    }

    /// <summary>
    /// 输出警告消息
    /// </summary>
    /// <param name="message">消息内容</param>
    public void OutputWarning(string message)
    {
      UpdateOutputWithColor(message, LogColor.Warning);
    }

    /// <summary>
    /// 输出错误消息
    /// </summary>
    /// <param name="message">消息内容</param>
    public void OutputError(string message)
    {
      UpdateOutputWithColor(message, LogColor.Error);
    }

    /// <summary>
    /// 输出信息消息
    /// </summary>
    /// <param name="message">消息内容</param>
    public void OutputInfo(string message)
    {
      UpdateOutputWithColor(message, LogColor.Info);
    }

    /// <summary>
    /// 输出高亮消息
    /// </summary>
    /// <param name="message">消息内容</param>
    public void OutputHighlight(string message)
    {
      UpdateOutputWithColor(message, LogColor.Highlight);
    }

    /// <summary>
    /// 按日志颜色类型获取对应的画刷
    /// </summary>
    private Brush GetBrushFromLogColor(LogColor color)
    {
      switch (color)
      {
        case LogColor.Success:
          return SuccessBrush;
        case LogColor.Warning:
          return WarningBrush;
        case LogColor.Error:
          return ErrorBrush;
        case LogColor.Info:
          return InfoBrush;
        case LogColor.Highlight:
          return HighlightBrush;
        case LogColor.Normal:
        default:
          return NormalBrush;
      }
    }

    /// <summary>
    /// 更新进度条
    /// </summary>
    /// <param name="value">进度值(0-100)</param>
    public void UpdateProgressBar(int value)
    {
      // 使用BeginInvoke避免死锁问题
      _dispatcher.BeginInvoke(new Action(() =>
      {
        _progressBar.Value = value;
      }));
    }

    /// <summary>
    /// 显示进度条
    /// </summary>
    public void ShowProgressBar()
    {
      _dispatcher.BeginInvoke(new Action(() =>
      {
        _progressBar.Visibility = Visibility.Visible;
        _progressBar.Value = 0;
      }));
    }

    /// <summary>
    /// 隐藏进度条
    /// </summary>
    public void HideProgressBar()
    {
      _dispatcher.BeginInvoke(new Action(() =>
      {
        _progressBar.Visibility = Visibility.Collapsed;
      }));
    }

    /// <summary>
    /// 更新结果文本框
    /// </summary>
    private void UpdateResultTextBox()
    {
      if (_useRichText)
        return;

      // 使用BeginInvoke避免死锁问题
      _dispatcher.BeginInvoke(new Action(() =>
      {
        _resultTextBox.Text = _plainTextContent.ToString();
        _resultTextBox.ScrollToEnd();
      }));
    }

    /// <summary>
    /// 异步更新输出内容
    /// </summary>
    /// <param name="message">要添加的消息</param>
    public Task UpdateOutputAsync(string message)
    {
      UpdateOutput(message);
      return Task.CompletedTask;
    }

    /// <summary>
    /// 异步更新带颜色的输出内容
    /// </summary>
    /// <param name="message">要添加的消息</param>
    /// <param name="color">日志颜色</param>
    public Task UpdateOutputWithColorAsync(string message, LogColor color)
    {
      UpdateOutputWithColor(message, color);
      return Task.CompletedTask;
    }

    /// <summary>
    /// 异步输出成功消息
    /// </summary>
    public Task OutputSuccessAsync(string message)
    {
      OutputSuccess(message);
      return Task.CompletedTask;
    }

    /// <summary>
    /// 异步输出警告消息
    /// </summary>
    public Task OutputWarningAsync(string message)
    {
      OutputWarning(message);
      return Task.CompletedTask;
    }

    /// <summary>
    /// 异步输出错误消息
    /// </summary>
    public Task OutputErrorAsync(string message)
    {
      OutputError(message);
      return Task.CompletedTask;
    }

    /// <summary>
    /// 异步输出信息消息
    /// </summary>
    public Task OutputInfoAsync(string message)
    {
      OutputInfo(message);
      return Task.CompletedTask;
    }

    /// <summary>
    /// 异步输出高亮消息
    /// </summary>
    public Task OutputHighlightAsync(string message)
    {
      OutputHighlight(message);
      return Task.CompletedTask;
    }

    /// <summary>
    /// 异步更新进度条
    /// </summary>
    /// <param name="value">进度值(0-100)</param>
    public Task UpdateProgressBarAsync(int value)
    {
      UpdateProgressBar(value);
      return Task.CompletedTask;
    }

    /// <summary>
    /// 异步显示进度条
    /// </summary>
    public Task ShowProgressBarAsync()
    {
      ShowProgressBar();
      return Task.CompletedTask;
    }

    /// <summary>
    /// 异步隐藏进度条
    /// </summary>
    public Task HideProgressBarAsync()
    {
      HideProgressBar();
      return Task.CompletedTask;
    }
  }
}