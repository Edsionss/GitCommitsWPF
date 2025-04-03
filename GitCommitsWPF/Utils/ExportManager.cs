using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using Newtonsoft.Json;
using GitCommitsWPF.Models;

namespace GitCommitsWPF.Utils
{
  /// <summary>
  /// 处理导出和保存结果相关功能的管理器类
  /// </summary>
  public class ExportManager
  {
    private readonly DialogManager _dialogManager;

    /// <summary>
    /// 初始化导出管理器
    /// </summary>
    /// <param name="dialogManager">对话框管理器</param>
    public ExportManager(DialogManager dialogManager)
    {
      _dialogManager = dialogManager ?? throw new ArgumentNullException(nameof(dialogManager));
    }

    /// <summary>
    /// 显示保存选项对话框
    /// </summary>
    /// <param name="owner">对话框所有者窗口</param>
    /// <param name="onSaveToFile">保存到文件回调</param>
    /// <param name="onCopyToClipboard">复制到剪贴板回调</param>
    public void ShowSaveOptionsDialog(Window owner, Action onSaveToFile, Action onCopyToClipboard)
    {
      // 创建自定义对话框，提供两个选项：保存到副本和复制到剪贴板
      var saveOptionsWindow = new Window
      {
        Title = "保存选项",
        Width = 350,
        Height = 180,
        WindowStartupLocation = WindowStartupLocation.CenterOwner,
        Owner = owner,
        ResizeMode = ResizeMode.NoResize,
        Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#f0f0f0"))
      };

      // 创建内容面板
      var grid = new Grid();
      grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
      grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

      // 提示文本
      var promptText = new TextBlock
      {
        Text = "请选择保存方式：",
        Margin = new Thickness(20, 20, 20, 10),
        TextWrapping = TextWrapping.Wrap,
        FontSize = 14,
        VerticalAlignment = VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Center
      };
      Grid.SetRow(promptText, 0);
      grid.Children.Add(promptText);

      // 按钮面板
      var buttonPanel = new StackPanel
      {
        Orientation = Orientation.Horizontal,
        HorizontalAlignment = HorizontalAlignment.Center,
        Margin = new Thickness(0, 10, 0, 20)
      };
      Grid.SetRow(buttonPanel, 1);

      // 保存到副本按钮
      var saveToFileButton = new Button
      {
        Content = "保存到副本",
        Padding = new Thickness(15, 8, 15, 8),
        Margin = new Thickness(10),
        MinWidth = 120,
        Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4CAF50")),
        Foreground = System.Windows.Media.Brushes.White
      };
      saveToFileButton.Click += (s, args) =>
      {
        saveOptionsWindow.Close();
        onSaveToFile?.Invoke();
      };

      // 复制到剪贴板按钮
      var copyToClipboardButton = new Button
      {
        Content = "复制到剪贴板",
        Padding = new Thickness(15, 8, 15, 8),
        Margin = new Thickness(10),
        MinWidth = 120,
        Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2196F3")),
        Foreground = System.Windows.Media.Brushes.White
      };
      copyToClipboardButton.Click += (s, args) =>
      {
        saveOptionsWindow.Close();
        onCopyToClipboard?.Invoke();
      };

      buttonPanel.Children.Add(saveToFileButton);
      buttonPanel.Children.Add(copyToClipboardButton);
      grid.Children.Add(buttonPanel);

      saveOptionsWindow.Content = grid;
      saveOptionsWindow.ShowDialog();
    }

    /// <summary>
    /// 复制结果到剪贴板
    /// </summary>
    /// <param name="commits">提交记录</param>
    /// <param name="statsOutput">统计输出</param>
    /// <param name="isStats">是否包含统计信息</param>
    /// <param name="formatTemplate">格式化模板，如果不为空则使用模板</param>
    /// <param name="selectedFields">选择的字段</param>
    public void CopyResultToClipboard(List<CommitInfo> commits, string statsOutput, bool isStats, string formatTemplate = null, List<string> selectedFields = null)
    {
      try
      {
        // 判断是否有提交记录
        if (commits == null || commits.Count == 0)
        {
          _dialogManager.ShowCustomMessageBox("提示", "没有可复制的提交记录。", false);
          return;
        }

        string formattedContent;

        // 如果有格式模板，使用模板格式化输出
        if (!string.IsNullOrEmpty(formatTemplate))
        {
          var sb = new StringBuilder();
          var displayedRepos = new Dictionary<string, bool>();

          // 如果启用了统计，先添加统计信息
          if (isStats)
          {
            sb.AppendLine("\n======== 提交统计 ========\n");
            sb.AppendLine(statsOutput);
            sb.AppendLine("\n==========================\n");
          }

          foreach (var commit in commits)
          {
            string line = formatTemplate;

            // 获取当前提交的仓库标识符
            string repoKey = !string.IsNullOrEmpty(commit.RepoFolder) ? commit.RepoFolder : commit.Repository;

            // 替换所有占位符，处理重复仓库名的显示逻辑
            line = line.Replace("{Repository}", displayedRepos.ContainsKey(repoKey) ? new string(' ', repoKey.Length) : commit.Repository);

            line = line.Replace("{RepoPath}", commit.RepoPath);

            line = line.Replace("{RepoFolder}", displayedRepos.ContainsKey(repoKey) ? new string(' ', repoKey.Length) : commit.RepoFolder);

            line = line.Replace("{CommitId}", commit.CommitId ?? "");
            line = line.Replace("{Author}", commit.Author ?? "");
            line = line.Replace("{Date}", commit.Date ?? "");
            line = line.Replace("{Message}", commit.Message ?? "");

            sb.AppendLine(line);

            // 标记此仓库已显示
            if (!displayedRepos.ContainsKey(repoKey))
            {
              displayedRepos[repoKey] = true;
            }
          }

          formattedContent = sb.ToString();
        }
        else
        {
          // 如果没有格式模板，使用JSON格式
          var filteredCommits = new List<Dictionary<string, string>>();

          // 创建筛选后的对象列表
          foreach (var commit in commits)
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

          var sb = new StringBuilder();

          // 添加统计信息（如果启用）
          if (isStats)
          {
            sb.AppendLine("\n======== 提交统计 ========\n");
            sb.AppendLine(statsOutput);
            sb.AppendLine("\n==========================\n");
          }

          // 序列化对象为JSON
          string json = JsonConvert.SerializeObject(filteredCommits, Newtonsoft.Json.Formatting.Indented);
          sb.AppendLine(json);

          formattedContent = sb.ToString();
        }

        // 复制到剪贴板
        try
        {
          // 创建可重试的剪贴板复制逻辑，多次尝试以解决剪贴板可能被其他程序占用的情况
          int maxRetries = 5;
          int retryDelayMs = 100;
          Exception lastException = null;

          for (int retry = 0; retry < maxRetries; retry++)
          {
            try
            {
              if (retry > 0)
              {
                System.Threading.Thread.Sleep(retryDelayMs * retry); // 增加重试等待时间
              }

              // 先清空剪贴板，再设置文本
              System.Windows.Clipboard.Clear();
              System.Windows.Clipboard.SetDataObject(formattedContent, true);
              lastException = null;
              break; // 成功则跳出循环
            }
            catch (Exception ex)
            {
              lastException = ex;
              // 继续重试，除非已达到最大重试次数
            }
          }

          // 检查是否最终失败
          if (lastException != null)
          {
            throw lastException;
          }

          _dialogManager.ShowCustomMessageBox("复制成功", "结果已复制到剪贴板", false);
        }
        catch (Exception ex)
        {
          _dialogManager.ShowCustomMessageBox("复制失败", $"复制到剪贴板时出错: {ex.Message}", false);
        }
      }
      catch (Exception ex)
      {
        _dialogManager.ShowCustomMessageBox("复制失败", $"复制到剪贴板时出错: {ex.Message}", false);
      }
    }

    /// <summary>
    /// 保存结果到指定路径，使用用户的格式模板
    /// </summary>
    /// <param name="outputPath">输出路径</param>
    /// <param name="commits">提交记录</param>
    /// <param name="statsOutput">统计输出</param>
    /// <param name="isStats">是否包含统计信息</param>
    /// <param name="formatTemplate">格式化模板，如果不为空则使用模板</param>
    /// <param name="selectedFields">选择的字段</param>
    public void SaveResults(string outputPath, List<CommitInfo> commits, string statsOutput, bool isStats, string formatTemplate = null, List<string> selectedFields = null)
    {
      try
      {
        string extension = Path.GetExtension(outputPath).ToLower();
        string formattedContent;

        // 如果有格式模板，使用模板格式化输出
        if (!string.IsNullOrEmpty(formatTemplate))
        {
          var sb = new StringBuilder();
          var displayedRepos = new Dictionary<string, bool>();

          // 如果启用了统计，先添加统计信息
          if (isStats)
          {
            sb.AppendLine("\n======== 提交统计 ========\n");
            sb.AppendLine(statsOutput);
            sb.AppendLine("\n==========================\n");
          }

          foreach (var commit in commits)
          {
            string line = formatTemplate;

            // 获取当前提交的仓库标识符
            string repoKey = !string.IsNullOrEmpty(commit.RepoFolder) ? commit.RepoFolder : commit.Repository;

            // 替换所有占位符，处理重复仓库名的显示逻辑
            line = line.Replace("{Repository}", displayedRepos.ContainsKey(repoKey) ? new string(' ', repoKey.Length) : commit.Repository);

            line = line.Replace("{RepoPath}", commit.RepoPath);

            line = line.Replace("{RepoFolder}", displayedRepos.ContainsKey(repoKey) ? new string(' ', repoKey.Length) : commit.RepoFolder);

            line = line.Replace("{CommitId}", commit.CommitId ?? "");
            line = line.Replace("{Author}", commit.Author ?? "");
            line = line.Replace("{Date}", commit.Date ?? "");
            line = line.Replace("{Message}", commit.Message ?? "");

            sb.AppendLine(line);

            // 标记此仓库已显示
            if (!displayedRepos.ContainsKey(repoKey))
            {
              displayedRepos[repoKey] = true;
            }
          }

          formattedContent = sb.ToString();

          // 根据文件扩展名保存
          if (extension == ".html")
          {
            SaveFormattedContentAsHtml(outputPath, formattedContent);
          }
          else
          {
            File.WriteAllText(outputPath, formattedContent, Encoding.UTF8);
          }
        }
        else
        {
          // 如果没有格式模板，使用JSON格式
          var filteredCommits = new List<Dictionary<string, string>>();

          // 创建筛选后的对象列表
          foreach (var commit in commits)
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

          var sb = new StringBuilder();

          // 添加统计信息（如果启用）
          if (isStats)
          {
            sb.AppendLine("\n======== 提交统计 ========\n");
            sb.AppendLine(statsOutput);
            sb.AppendLine("\n==========================\n");
          }

          // 序列化对象为JSON
          string json = JsonConvert.SerializeObject(filteredCommits, Newtonsoft.Json.Formatting.Indented);
          sb.AppendLine(json);

          formattedContent = sb.ToString();

          // 根据文件扩展名保存
          switch (extension)
          {
            case ".html":
              SaveFormattedContentAsHtml(outputPath, formattedContent);
              break;
            case ".xml":
              if (formattedContent.Contains("======== 提交统计 ========"))
              {
                // 如果包含统计信息，直接保存为文本格式
                File.WriteAllText(outputPath, formattedContent, Encoding.UTF8);
              }
              else
              {
                // 否则转换为XML格式
                SaveAsXml(outputPath, JsonConvert.DeserializeObject<List<Dictionary<string, string>>>(json));
              }
              break;
            case ".csv":
              if (formattedContent.Contains("======== 提交统计 ========"))
              {
                // 如果包含统计信息，直接保存为文本格式
                File.WriteAllText(outputPath, formattedContent, Encoding.UTF8);
              }
              else
              {
                // 否则转换为CSV格式
                SaveAsCsv(outputPath, JsonConvert.DeserializeObject<List<Dictionary<string, string>>>(json));
              }
              break;
            default:
              // 保存为文本格式
              File.WriteAllText(outputPath, formattedContent, Encoding.UTF8);
              break;
          }
        }

        _dialogManager.ShowCustomMessageBox("保存成功", $"结果已保存到: {outputPath}", true, outputPath);
      }
      catch (Exception ex)
      {
        _dialogManager.ShowCustomMessageBox("保存失败", $"保存结果时出错: {ex.Message}", false);
      }
    }

    /// <summary>
    /// 将格式化内容保存为HTML文件
    /// </summary>
    /// <param name="path">保存路径</param>
    /// <param name="content">格式化内容</param>
    private void SaveFormattedContentAsHtml(string path, string content)
    {
      StringBuilder sb = new StringBuilder();
      sb.AppendLine("<!DOCTYPE html>");
      sb.AppendLine("<html>");
      sb.AppendLine("<head>");
      sb.AppendLine("<meta charset=\"UTF-8\">");
      sb.AppendLine("<title>Git提交记录</title>");
      sb.AppendLine("<style>");
      sb.AppendLine("body { font-family: Arial, sans-serif; margin: 20px; }");
      sb.AppendLine("h1 { color: #333; }");
      sb.AppendLine("pre { background-color: #f5f5f5; padding: 10px; white-space: pre-wrap; }");
      sb.AppendLine("</style>");
      sb.AppendLine("</head>");
      sb.AppendLine("<body>");

      sb.AppendLine("<h1>Git提交记录</h1>");
      sb.AppendLine("<pre>");
      sb.AppendLine(content);
      sb.AppendLine("</pre>");

      sb.AppendLine("</body>");
      sb.AppendLine("</html>");

      File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }

    /// <summary>
    /// 保存为JSON文件
    /// </summary>
    /// <param name="path">保存路径</param>
    /// <param name="commits">提交记录</param>
    private void SaveAsJson(string path, List<Dictionary<string, string>> commits)
    {
      string json = JsonConvert.SerializeObject(commits, Newtonsoft.Json.Formatting.Indented);
      File.WriteAllText(path, json, Encoding.UTF8);
    }

    /// <summary>
    /// 保存为带统计信息的文本文件
    /// </summary>
    /// <param name="path">保存路径</param>
    /// <param name="commits">提交记录</param>
    /// <param name="statsOutput">统计输出</param>
    private void SaveAsTextWithStats(string path, List<Dictionary<string, string>> commits, string statsOutput)
    {
      StringBuilder sb = new StringBuilder();
      sb.AppendLine("Git提交记录");
      sb.AppendLine("==========================================");
      sb.AppendLine();

      foreach (var commit in commits)
      {
        sb.AppendLine($"提交: {commit["hash"]}");
        sb.AppendLine($"作者: {commit["author"]}");
        sb.AppendLine($"日期: {commit["date"]}");
        sb.AppendLine($"消息: {commit["message"]}");
        sb.AppendLine("------------------------------------------");
      }

      sb.AppendLine();
      sb.AppendLine("统计信息");
      sb.AppendLine("==========================================");
      sb.AppendLine(statsOutput);

      File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }

    /// <summary>
    /// 保存为带统计信息的HTML文件
    /// </summary>
    /// <param name="path">保存路径</param>
    /// <param name="commits">提交记录</param>
    /// <param name="statsOutput">统计输出</param>
    private void SaveAsHtmlWithStats(string path, List<Dictionary<string, string>> commits, string statsOutput)
    {
      StringBuilder sb = new StringBuilder();
      sb.AppendLine("<!DOCTYPE html>");
      sb.AppendLine("<html>");
      sb.AppendLine("<head>");
      sb.AppendLine("<meta charset=\"UTF-8\">");
      sb.AppendLine("<title>Git提交记录</title>");
      sb.AppendLine("<style>");
      sb.AppendLine("body { font-family: Arial, sans-serif; margin: 20px; }");
      sb.AppendLine("h1, h2 { color: #333; }");
      sb.AppendLine(".commit { border: 1px solid #ddd; margin-bottom: 10px; padding: 10px; border-radius: 5px; }");
      sb.AppendLine(".commit-header { font-weight: bold; margin-bottom: 5px; }");
      sb.AppendLine(".stats { background-color: #f9f9f9; padding: 10px; border-radius: 5px; white-space: pre-wrap; }");
      sb.AppendLine("</style>");
      sb.AppendLine("</head>");
      sb.AppendLine("<body>");

      sb.AppendLine("<h1>Git提交记录</h1>");

      foreach (var commit in commits)
      {
        sb.AppendLine("<div class=\"commit\">");
        sb.AppendLine($"<div class=\"commit-header\">提交: {commit["hash"]}</div>");
        sb.AppendLine($"<div>作者: {commit["author"]}</div>");
        sb.AppendLine($"<div>日期: {commit["date"]}</div>");
        sb.AppendLine($"<div>消息: {commit["message"]}</div>");
        sb.AppendLine("</div>");
      }

      sb.AppendLine("<h2>统计信息</h2>");
      sb.AppendLine("<div class=\"stats\">");
      sb.AppendLine(statsOutput.Replace("\n", "<br/>"));
      sb.AppendLine("</div>");

      sb.AppendLine("</body>");
      sb.AppendLine("</html>");

      File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }

    /// <summary>
    /// 保存为CSV文件
    /// </summary>
    /// <param name="path">保存路径</param>
    /// <param name="commits">提交记录</param>
    private void SaveAsCsv(string path, List<Dictionary<string, string>> commits)
    {
      StringBuilder sb = new StringBuilder();
      sb.AppendLine("提交ID,作者,日期,消息");

      foreach (var commit in commits)
      {
        string hash = commit["hash"];
        string author = commit["author"].Replace(",", ";");
        string date = commit["date"];
        string message = commit["message"].Replace(",", ";").Replace("\n", " ").Replace("\r", "");

        sb.AppendLine($"{hash},{author},{date},{message}");
      }

      File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }

    /// <summary>
    /// 保存为文本文件
    /// </summary>
    /// <param name="path">保存路径</param>
    /// <param name="commits">提交记录</param>
    private void SaveAsText(string path, List<Dictionary<string, string>> commits)
    {
      StringBuilder sb = new StringBuilder();
      sb.AppendLine("Git提交记录");
      sb.AppendLine("==========================================");
      sb.AppendLine();

      foreach (var commit in commits)
      {
        sb.AppendLine($"提交: {commit["hash"]}");
        sb.AppendLine($"作者: {commit["author"]}");
        sb.AppendLine($"日期: {commit["date"]}");
        sb.AppendLine($"消息: {commit["message"]}");
        sb.AppendLine("------------------------------------------");
      }

      File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }

    /// <summary>
    /// 保存为HTML文件
    /// </summary>
    /// <param name="path">保存路径</param>
    /// <param name="commits">提交记录</param>
    private void SaveAsHtml(string path, List<Dictionary<string, string>> commits)
    {
      StringBuilder sb = new StringBuilder();
      sb.AppendLine("<!DOCTYPE html>");
      sb.AppendLine("<html>");
      sb.AppendLine("<head>");
      sb.AppendLine("<meta charset=\"UTF-8\">");
      sb.AppendLine("<title>Git提交记录</title>");
      sb.AppendLine("<style>");
      sb.AppendLine("body { font-family: Arial, sans-serif; margin: 20px; }");
      sb.AppendLine("h1 { color: #333; }");
      sb.AppendLine(".commit { border: 1px solid #ddd; margin-bottom: 10px; padding: 10px; border-radius: 5px; }");
      sb.AppendLine(".commit-header { font-weight: bold; margin-bottom: 5px; }");
      sb.AppendLine("</style>");
      sb.AppendLine("</head>");
      sb.AppendLine("<body>");

      sb.AppendLine("<h1>Git提交记录</h1>");

      foreach (var commit in commits)
      {
        sb.AppendLine("<div class=\"commit\">");
        sb.AppendLine($"<div class=\"commit-header\">提交: {commit["hash"]}</div>");
        sb.AppendLine($"<div>作者: {commit["author"]}</div>");
        sb.AppendLine($"<div>日期: {commit["date"]}</div>");
        sb.AppendLine($"<div>消息: {commit["message"]}</div>");
        sb.AppendLine("</div>");
      }

      sb.AppendLine("</body>");
      sb.AppendLine("</html>");

      File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }

    /// <summary>
    /// 保存为XML文件
    /// </summary>
    /// <param name="path">保存路径</param>
    /// <param name="commits">提交记录</param>
    private void SaveAsXml(string path, List<Dictionary<string, string>> commits)
    {
      XmlDocument doc = new XmlDocument();
      XmlElement root = doc.CreateElement("GitCommits");
      doc.AppendChild(root);

      foreach (var commit in commits)
      {
        XmlElement commitElement = doc.CreateElement("Commit");

        XmlElement hashElement = doc.CreateElement("Hash");
        hashElement.InnerText = commit["hash"];
        commitElement.AppendChild(hashElement);

        XmlElement authorElement = doc.CreateElement("Author");
        authorElement.InnerText = commit["author"];
        commitElement.AppendChild(authorElement);

        XmlElement dateElement = doc.CreateElement("Date");
        dateElement.InnerText = commit["date"];
        commitElement.AppendChild(dateElement);

        XmlElement messageElement = doc.CreateElement("Message");
        messageElement.InnerText = commit["message"];
        commitElement.AppendChild(messageElement);

        root.AppendChild(commitElement);
      }

      doc.Save(path);
    }

    /// <summary>
    /// 显示保存文件对话框并返回选择的文件路径
    /// </summary>
    /// <param name="defaultFileName">默认文件名</param>
    /// <returns>选择的文件路径，如取消则返回null</returns>
    public string ShowSaveFileDialog(string defaultFileName = "Git提交记录")
    {
      SaveFileDialog saveDialog = new SaveFileDialog
      {
        Title = "保存提交记录",
        Filter = "文本文件(*.txt)|*.txt|HTML文件(*.html)|*.html|XML文件(*.xml)|*.xml|CSV文件(*.csv)|*.csv|JSON文件(*.json)|*.json|所有文件(*.*)|*.*",
        DefaultExt = ".txt",
        AddExtension = true,
        FileName = defaultFileName
      };

      if (saveDialog.ShowDialog() == true)
      {
        return saveDialog.FileName;
      }

      return null;
    }
  }
}