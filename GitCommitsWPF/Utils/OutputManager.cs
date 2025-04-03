using System;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace GitCommitsWPF.Utils
{
  /// <summary>
  /// 输出管理器，负责管理日志输出和进度条更新
  /// </summary>
  public class OutputManager
  {
    private StringBuilder _outputContent = new StringBuilder();
    private TextBox _resultTextBox;
    private ProgressBar _progressBar;
    private Dispatcher _dispatcher;

    /// <summary>
    /// 初始化输出管理器
    /// </summary>
    /// <param name="resultTextBox">结果文本框</param>
    /// <param name="progressBar">进度条</param>
    /// <param name="dispatcher">UI线程调度器</param>
    public OutputManager(TextBox resultTextBox, ProgressBar progressBar, Dispatcher dispatcher)
    {
      _resultTextBox = resultTextBox;
      _progressBar = progressBar;
      _dispatcher = dispatcher;
    }

    /// <summary>
    /// 获取输出内容
    /// </summary>
    public string OutputContent => _outputContent.ToString();

    /// <summary>
    /// 清空输出内容
    /// </summary>
    public void ClearOutput()
    {
      _outputContent.Clear();
      UpdateResultTextBox();
    }

    /// <summary>
    /// 添加分隔线
    /// </summary>
    public void AddSeparator()
    {
      _outputContent.AppendLine("\n===== " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " =====\n");
      UpdateResultTextBox();
    }

    /// <summary>
    /// 添加新查询分隔线
    /// </summary>
    public void AddNewQuerySeparator()
    {
      _outputContent.AppendLine("\n===== 开始新查询 " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " =====\n");
      UpdateResultTextBox();
    }

    /// <summary>
    /// 更新输出内容
    /// </summary>
    /// <param name="message">要添加的消息</param>
    public void UpdateOutput(string message)
    {
      // 添加时间戳
      string timeStamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
      string formattedMessage = string.Format("[{0}] {1}", timeStamp, message);

      _outputContent.AppendLine(formattedMessage);
      UpdateResultTextBox();
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
      // 使用BeginInvoke避免死锁问题
      _dispatcher.BeginInvoke(new Action(() =>
      {
        _resultTextBox.Text = _outputContent.ToString();
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