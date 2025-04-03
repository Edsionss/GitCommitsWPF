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
  }
}