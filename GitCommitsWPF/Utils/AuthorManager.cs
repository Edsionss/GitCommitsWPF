using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace GitCommitsWPF.Utils
{
  /// <summary>
  /// 作者管理器，负责处理Git作者相关操作
  /// </summary>
  public class AuthorManager
  {
    private List<string> _recentAuthors = new List<string>(); // 保存最近使用的作者
    private List<string> _scannedAuthors = new List<string>(); // 保存扫描出来的作者
    private const int MaxRecentAuthors = 10; // 最多保存10个最近使用的作者

    /// <summary>
    /// 初始化作者管理器
    /// </summary>
    public AuthorManager()
    {
      LoadRecentAuthors();
      LoadScannedAuthors();
    }

    /// <summary>
    /// 获取最近使用的作者列表
    /// </summary>
    public List<string> RecentAuthors => _recentAuthors;

    /// <summary>
    /// 获取扫描到的作者列表
    /// </summary>
    public List<string> ScannedAuthors => _scannedAuthors;

    /// <summary>
    /// 加载最近使用的作者
    /// </summary>
    public void LoadRecentAuthors()
    {
      try
      {
        string appDataPath = FileUtility.GetAppDataDirectory();
        string recentAuthorsFile = Path.Combine(appDataPath, "RecentAuthors.txt");

        if (File.Exists(recentAuthorsFile))
        {
          _recentAuthors = FileUtility.ReadLinesFromFile(recentAuthorsFile);
        }
      }
      catch (Exception ex)
      {
        // 忽略错误，使用空的最近作者列表
        _recentAuthors = new List<string>();
        Debug.WriteLine("加载最近作者时出错: " + ex.Message);
      }
    }

    /// <summary>
    /// 保存最近使用的作者
    /// </summary>
    public void SaveRecentAuthors()
    {
      try
      {
        string appDataPath = FileUtility.GetAppDataDirectory();
        string recentAuthorsFile = Path.Combine(appDataPath, "RecentAuthors.txt");
        FileUtility.SaveLinesToFile(recentAuthorsFile, _recentAuthors);
      }
      catch (Exception ex)
      {
        // 忽略错误
        Debug.WriteLine("保存最近作者时出错: " + ex.Message);
      }
    }

    /// <summary>
    /// 添加作者到最近作者列表
    /// </summary>
    /// <param name="author">作者名称</param>
    public void AddToRecentAuthors(string author)
    {
      if (string.IsNullOrEmpty(author)) return;

      // 移除相同的作者（如果存在）
      _recentAuthors.Remove(author);

      // 添加到列表开头
      _recentAuthors.Insert(0, author);

      // 限制最近作者数量
      if (_recentAuthors.Count > MaxRecentAuthors)
      {
        _recentAuthors.RemoveAt(_recentAuthors.Count - 1);
      }

      // 保存更新后的列表
      SaveRecentAuthors();
    }

    /// <summary>
    /// 加载扫描到的作者
    /// </summary>
    public void LoadScannedAuthors()
    {
      try
      {
        string appDataPath = FileUtility.GetAppDataDirectory();
        string scannedAuthorsFile = Path.Combine(appDataPath, "ScannedAuthors.txt");

        if (File.Exists(scannedAuthorsFile))
        {
          _scannedAuthors = FileUtility.ReadLinesFromFile(scannedAuthorsFile);
        }
      }
      catch (Exception ex)
      {
        // 忽略错误，使用空的扫描作者列表
        _scannedAuthors = new List<string>();
        Debug.WriteLine("加载扫描作者时出错: " + ex.Message);
      }
    }

    /// <summary>
    /// 保存扫描到的作者
    /// </summary>
    public void SaveScannedAuthors()
    {
      try
      {
        string appDataPath = FileUtility.GetAppDataDirectory();
        string scannedAuthorsFile = Path.Combine(appDataPath, "ScannedAuthors.txt");
        FileUtility.SaveLinesToFile(scannedAuthorsFile, _scannedAuthors);
      }
      catch (Exception ex)
      {
        // 忽略错误
        Debug.WriteLine("保存扫描作者时出错: " + ex.Message);
      }
    }

    /// <summary>
    /// 添加扫描到的作者
    /// </summary>
    /// <param name="author">作者名称</param>
    public void AddScannedAuthor(string author)
    {
      if (string.IsNullOrEmpty(author) || _scannedAuthors.Contains(author)) return;

      _scannedAuthors.Add(author);
      SaveScannedAuthors();
    }

    /// <summary>
    /// 添加多个扫描到的作者
    /// </summary>
    /// <param name="authors">作者列表</param>
    public void AddScannedAuthors(IEnumerable<string> authors)
    {
      if (authors == null) return;

      bool changed = false;
      foreach (var author in authors)
      {
        if (!string.IsNullOrEmpty(author) && !_scannedAuthors.Contains(author))
        {
          _scannedAuthors.Add(author);
          changed = true;
        }
      }

      if (changed)
      {
        SaveScannedAuthors();
      }
    }
  }
}