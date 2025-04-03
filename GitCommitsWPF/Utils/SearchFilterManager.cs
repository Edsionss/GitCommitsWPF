using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GitCommitsWPF.Models;

namespace GitCommitsWPF.Utils
{
  /// <summary>
  /// 管理Git提交记录搜索和过滤功能的类
  /// </summary>
  public class SearchFilterManager
  {
    /// <summary>
    /// 应用过滤条件到提交列表
    /// </summary>
    /// <param name="allCommits">所有提交记录</param>
    /// <param name="filterText">过滤文本</param>
    /// <returns>过滤后的提交列表</returns>
    public List<CommitInfo> ApplyFilter(List<CommitInfo> allCommits, string filterText)
    {
      // 如果过滤文本为空，返回所有提交记录
      if (string.IsNullOrEmpty(filterText))
      {
        return new List<CommitInfo>(allCommits);
      }

      // 转换为小写以便不区分大小写搜索
      filterText = filterText.Trim().ToLower();

      // 应用过滤
      return allCommits.Where(commit =>
          (commit.Repository != null && commit.Repository.ToLower().Contains(filterText)) ||
          (commit.RepoFolder != null && commit.RepoFolder.ToLower().Contains(filterText)) ||
          (commit.Author != null && commit.Author.ToLower().Contains(filterText)) ||
          (commit.Message != null && commit.Message.ToLower().Contains(filterText)) ||
          (commit.Date != null && commit.Date.ToLower().Contains(filterText)) ||
          (commit.CommitId != null && commit.CommitId.ToLower().Contains(filterText))
      ).ToList();
    }

    /// <summary>
    /// 获取过滤结果的统计数据
    /// </summary>
    /// <param name="filteredCommits">过滤后的提交列表</param>
    /// <returns>包含过滤统计信息的字符串</returns>
    public string GetFilterStats(List<CommitInfo> filteredCommits)
    {
      return string.Format("找到 {0} 条匹配记录", filteredCommits.Count);
    }
  }
}