using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GitCommitsWPF.Models;

namespace GitCommitsWPF.Utils
{
  /// <summary>
  /// 管理Git提交记录统计相关功能的类
  /// </summary>
  public class StatisticsManager
  {
    /// <summary>
    /// 生成统计数据并添加到输出字符串
    /// </summary>
    /// <param name="commits">提交记录列表</param>
    /// <param name="output">输出StringBuilder</param>
    /// <param name="statsByAuthor">是否按作者统计</param>
    /// <param name="statsByRepo">是否按仓库统计</param>
    /// <param name="statsByDate">是否按日期统计</param>
    public void GenerateStats(List<CommitInfo> commits, StringBuilder output, bool statsByAuthor, bool statsByRepo, bool statsByDate)
    {
      // 检查是否有提交记录可以统计
      if (commits == null || commits.Count == 0)
      {
        output.AppendLine("没有找到可以统计的提交记录。");
        return;
      }

      // 1. 按作者统计
      if (statsByAuthor)
      {
        output.AppendLine("【按作者统计提交数量】");
        var authorStats = commits
            .GroupBy(commit => commit.Author ?? "未知作者")
            .Select(group => new { Author = group.Key, Count = group.Count() })
            .OrderByDescending(item => item.Count)
            .ToList();

        if (authorStats.Count > 0)
        {
          // 计算最大宽度
          int maxAuthorWidth = Math.Max("作者".Length, authorStats.Max(s => s.Author.Length));
          int maxCountWidth = Math.Max("提交数".Length, authorStats.Max(s => s.Count.ToString().Length));

          // 输出表头
          output.AppendLine(string.Format("{0} | {1}",
              "作者".PadRight(maxAuthorWidth),
              "提交数".PadLeft(maxCountWidth)));
          output.AppendLine(string.Format("{0}-+-{1}",
              new string('-', maxAuthorWidth),
              new string('-', maxCountWidth)));

          // 输出数据行
          foreach (var stat in authorStats)
          {
            output.AppendLine(string.Format("{0} | {1}",
                stat.Author.PadRight(maxAuthorWidth),
                stat.Count.ToString().PadLeft(maxCountWidth)));
          }
        }
        else
        {
          output.AppendLine("没有作者数据可以统计。");
        }

        output.AppendLine();
      }

      // 2. 按仓库统计
      if (statsByRepo)
      {
        output.AppendLine("【按仓库统计提交数量】");
        var repoStats = commits
            .GroupBy(commit => !string.IsNullOrEmpty(commit.RepoFolder) ? commit.RepoFolder : commit.Repository)
            .Select(group => new { Repo = group.Key, Count = group.Count() })
            .OrderByDescending(item => item.Count)
            .ToList();

        if (repoStats.Count > 0)
        {
          // 计算最大宽度
          int maxRepoWidth = Math.Max("仓库".Length, repoStats.Max(s => s.Repo.Length));
          int maxCountWidth = Math.Max("提交数".Length, repoStats.Max(s => s.Count.ToString().Length));

          // 输出表头
          output.AppendLine(string.Format("{0} | {1}",
              "仓库".PadRight(maxRepoWidth),
              "提交数".PadLeft(maxCountWidth)));
          output.AppendLine(string.Format("{0}-+-{1}",
              new string('-', maxRepoWidth),
              new string('-', maxCountWidth)));

          // 输出数据行
          foreach (var stat in repoStats)
          {
            output.AppendLine(string.Format("{0} | {1}",
                stat.Repo.PadRight(maxRepoWidth),
                stat.Count.ToString().PadLeft(maxCountWidth)));
          }
        }
        else
        {
          output.AppendLine("没有仓库数据可以统计。");
        }

        output.AppendLine();
      }

      // 3. 按日期统计
      if (statsByDate)
      {
        output.AppendLine("【按日期统计提交数量】");
        var dateStats = commits
            .Select(commit => new
            {
              Date = string.IsNullOrEmpty(commit.Date) ?
                    DateTime.Now.ToString("yyyy-MM-dd") :
                    GetFormattedDate(commit.Date)
            })
            .GroupBy(item => item.Date)
            .Select(group => new { Date = group.Key, Count = group.Count() })
            .OrderByDescending(item => item.Date)
            .ToList();

        if (dateStats.Count > 0)
        {
          // 计算最大宽度
          int maxDateWidth = Math.Max("日期".Length, dateStats.Max(s => s.Date.Length));
          int maxCountWidth = Math.Max("提交数".Length, dateStats.Max(s => s.Count.ToString().Length));

          // 输出表头
          output.AppendLine(string.Format("{0} | {1}",
              "日期".PadRight(maxDateWidth),
              "提交数".PadLeft(maxCountWidth)));
          output.AppendLine(string.Format("{0}-+-{1}",
              new string('-', maxDateWidth),
              new string('-', maxCountWidth)));

          // 输出数据行
          foreach (var stat in dateStats)
          {
            output.AppendLine(string.Format("{0} | {1}",
                stat.Date.PadRight(maxDateWidth),
                stat.Count.ToString().PadLeft(maxCountWidth)));
          }
        }
        else
        {
          output.AppendLine("没有日期数据可以统计。");
        }
      }
    }

    /// <summary>
    /// 安全地解析日期字符串
    /// </summary>
    /// <param name="dateString">日期字符串</param>
    /// <returns>格式化后的日期字符串</returns>
    private string GetFormattedDate(string dateString)
    {
      try
      {
        return DateTime.Parse(dateString).ToString("yyyy-MM-dd");
      }
      catch (Exception)
      {
        // 如果无法解析日期，返回原始字符串或特定标记
        return "未知日期";
      }
    }
  }
}