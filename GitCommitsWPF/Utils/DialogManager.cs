using System;
using System.IO;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GitCommitsWPF.Utils
{
  /// <summary>
  /// 对话框管理器，负责管理自定义对话框的显示
  /// </summary>
  public class DialogManager
  {
    private Window _ownerWindow;

    /// <summary>
    /// 初始化对话框管理器
    /// </summary>
    /// <param name="ownerWindow">作为对话框所有者的窗口</param>
    public DialogManager(Window ownerWindow)
    {
      _ownerWindow = ownerWindow;
    }

    /// <summary>
    /// 显示自定义消息对话框
    /// </summary>
    /// <param name="title">对话框标题</param>
    /// <param name="message">显示的消息内容</param>
    /// <param name="isSuccess">是否为成功消息（影响样式）</param>
    /// <param name="filePath">可选的文件路径，用于添加"打开文件"按钮</param>
    public void ShowCustomMessageBox(string title, string message, bool isSuccess, string filePath = null)
    {
      // 创建自定义消息窗口
      var customMessageWindow = new Window
      {
        Title = title,
        Width = 400,
        Height = 200,
        WindowStartupLocation = WindowStartupLocation.CenterOwner,
        Owner = _ownerWindow,
        ResizeMode = ResizeMode.NoResize,
        Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#f0f0f0"))
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
      if (isSuccess && !string.IsNullOrEmpty(filePath))
      {
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

    /// <summary>
    /// 显示复制成功的消息框
    /// </summary>
    /// <param name="title">对话框标题</param>
    /// <param name="message">显示的消息内容</param>
    public void ShowCopySuccessMessageBox(string title, string message)
    {
      // 创建自定义消息窗口
      var customMessageWindow = new Window
      {
        Title = title,
        Width = 400,
        Height = 200,
        WindowStartupLocation = WindowStartupLocation.CenterOwner,
        Owner = _ownerWindow,
        ResizeMode = ResizeMode.NoResize,
        Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#f0f0f0"))
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
      grid.Children.Add(buttonPanel);
      customMessageWindow.Content = grid;

      // 显示窗口
      customMessageWindow.ShowDialog();
    }

    /// <summary>
    /// 显示自定义确认对话框
    /// </summary>
    /// <param name="title">对话框标题</param>
    /// <param name="message">确认消息内容</param>
    /// <returns>用户选择结果，确认返回true，取消返回false</returns>
    public bool ShowCustomConfirmDialog(string title, string message)
    {
      bool result = false;

      // 创建自定义确认窗口
      var confirmWindow = new Window
      {
        Title = title,
        Width = 360,
        Height = 180,
        WindowStartupLocation = WindowStartupLocation.CenterOwner,
        Owner = _ownerWindow,
        ResizeMode = ResizeMode.NoResize,
        Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#f0f0f0"))
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

    /// <summary>
    /// 显示自定义消息对话框，支持可选的Action回调
    /// </summary>
    /// <param name="title">对话框标题</param>
    /// <param name="message">显示的消息内容</param>
    /// <param name="isSuccess">是否为成功消息（影响样式）</param>
    /// <param name="action">按钮点击时执行的回调</param>
    public void ShowCustomMessageBox(string title, string message, bool isSuccess, Action action)
    {
      // 创建自定义消息窗口
      var customMessageWindow = new Window
      {
        Title = title,
        Width = 400,
        Height = 200,
        WindowStartupLocation = WindowStartupLocation.CenterOwner,
        Owner = _ownerWindow,
        ResizeMode = ResizeMode.NoResize,
        Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#f0f0f0"))
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
      okButton.Click += (s, e) =>
      {
        customMessageWindow.Close();
        action?.Invoke();
      };

      buttonPanel.Children.Add(okButton);
      grid.Children.Add(buttonPanel);
      customMessageWindow.Content = grid;

      // 显示窗口
      customMessageWindow.ShowDialog();
    }

    /// <summary>
    /// 显示临时警告消息框，1秒后自动关闭
    /// </summary>
    /// <param name="title">对话框标题</param>
    /// <param name="message">显示的警告内容</param>
    public void ShowTemporaryWarningMessageBox(string title, string message)
    {
      try
      {
        // 创建一个轻量级的消息提示控件
        var messageControl = new Border
        {
          Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF4C0")), // 黄色警告背景色
          BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5CC7A")),
          BorderThickness = new Thickness(1),
          CornerRadius = new CornerRadius(4),
          Padding = new Thickness(10),
          Margin = new Thickness(0, 10, 0, 0),
          HorizontalAlignment = HorizontalAlignment.Center,
          VerticalAlignment = VerticalAlignment.Top,
          MaxWidth = 400,
          MinWidth = 250
        };

        // 设置阴影效果
        messageControl.Effect = new System.Windows.Media.Effects.DropShadowEffect
        {
          Color = Colors.Gray,
          BlurRadius = 10,
          ShadowDepth = 3,
          Opacity = 0.3
        };

        // 创建内容面板
        var panel = new StackPanel { Orientation = Orientation.Horizontal };

        // 警告图标
        var warningIcon = new TextBlock
        {
          Text = "⚠️",
          FontSize = 18,
          Margin = new Thickness(0, 0, 10, 0),
          VerticalAlignment = VerticalAlignment.Center
        };
        panel.Children.Add(warningIcon);

        // 消息文本
        var messageText = new TextBlock
        {
          Text = message,
          TextWrapping = TextWrapping.Wrap,
          VerticalAlignment = VerticalAlignment.Center,
          Margin = new Thickness(0)
        };
        panel.Children.Add(messageText);

        messageControl.Child = panel;

        // 查找通知容器
        var notificationContainer = FindNotificationContainer();
        if (notificationContainer != null)
        {
          // 添加到通知容器
          notificationContainer.Children.Add(messageControl);

          // 设置动画淡出效果
          var animation = new System.Windows.Media.Animation.DoubleAnimation
          {
            From = 1.0,
            To = 0.0,
            Duration = new Duration(TimeSpan.FromMilliseconds(500))
          };

          animation.Completed += (s, e) =>
          {
            // 动画完成后移除控件
            notificationContainer.Children.Remove(messageControl);
          };

          // 设置计时器延迟淡出
          var timer = new System.Windows.Threading.DispatcherTimer
          {
            Interval = TimeSpan.FromSeconds(1) // 1秒后开始淡出动画
          };
          timer.Tick += (s, e) =>
          {
            timer.Stop();
            messageControl.BeginAnimation(UIElement.OpacityProperty, animation);
          };
          timer.Start();
        }
      }
      catch (Exception)
      {
        // 处理异常，避免闪退
        // 即使无法显示提示，程序也应该继续运行
      }
    }

    /// <summary>
    /// 查找主窗口中的通知容器
    /// </summary>
    /// <returns>通知容器Grid</returns>
    private Grid FindNotificationContainer()
    {
      // 如果窗口为空，返回null
      if (_ownerWindow == null)
        return null;

      // 尝试找到命名为NotificationContainer的Grid
      return _ownerWindow.FindName("NotificationContainer") as Grid;
    }
  }
}