using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace GitCommitsWPF.Utils
{
  /// <summary>
  /// 位置管理器，负责管理Git仓库路径和保存路径
  /// </summary>
  public class LocationManager
  {
    private List<string> _recentLocations = new List<string>(); // 保存最近扫描的git仓库的位置
    private const int MaxRecentLocations = 10; // 最多保存10个最近扫描的git仓库的位置
    private List<string> _saveLocations = new List<string>(); // 保存最近保存的文件的位置
    private const int MaxSaveLocations = 10; // 最多保存10个最近保存的文件的位置

    /// <summary>
    /// 初始化位置管理器
    /// </summary>
    public LocationManager()
    {
      LoadRecentLocations();
      LoadSaveLocations();
    }

    /// <summary>
    /// 获取最近的Git仓库位置列表
    /// </summary>
    public List<string> RecentLocations => _recentLocations;

    /// <summary>
    /// 获取最近的保存位置列表
    /// </summary>
    public List<string> SaveLocations => _saveLocations;

    /// <summary>
    /// 加载最近使用的位置
    /// </summary>
    public void LoadRecentLocations()
    {
      try
      {
        string appDataPath = FileUtility.GetAppDataDirectory();
        string recentLocationsFile = Path.Combine(appDataPath, "RecentLocations.txt");

        if (File.Exists(recentLocationsFile))
        {
          _recentLocations = FileUtility.ReadLinesFromFile(recentLocationsFile);
        }
      }
      catch (Exception ex)
      {
        // 忽略错误，使用空的最近位置列表
        _recentLocations = new List<string>();
        Debug.WriteLine("加载最近位置时出错: " + ex.Message);
      }
    }

    /// <summary>
    /// 保存最近使用的位置
    /// </summary>
    public void SaveRecentLocations()
    {
      try
      {
        string appDataPath = FileUtility.GetAppDataDirectory();
        string recentLocationsFile = Path.Combine(appDataPath, "RecentLocations.txt");
        FileUtility.SaveLinesToFile(recentLocationsFile, _recentLocations);
      }
      catch (Exception ex)
      {
        // 忽略错误
        Debug.WriteLine("保存最近位置时出错: " + ex.Message);
      }
    }

    /// <summary>
    /// 添加路径到最近位置列表
    /// </summary>
    /// <param name="path">要添加的路径</param>
    public void AddToRecentLocations(string path)
    {
      if (string.IsNullOrEmpty(path)) return;

      // 移除相同的路径（如果存在）
      _recentLocations.Remove(path);

      // 添加到列表开头
      _recentLocations.Insert(0, path);

      // 限制最近位置数量
      if (_recentLocations.Count > MaxRecentLocations)
      {
        _recentLocations.RemoveAt(_recentLocations.Count - 1);
      }

      // 保存更新后的列表
      SaveRecentLocations();
    }

    /// <summary>
    /// 清除最近位置列表
    /// </summary>
    public void ClearRecentLocations()
    {
      _recentLocations.Clear();
      SaveRecentLocations();
    }

    /// <summary>
    /// 加载最近保存的位置
    /// </summary>
    public void LoadSaveLocations()
    {
      try
      {
        string appDataPath = FileUtility.GetAppDataDirectory();
        string saveLocationsFile = Path.Combine(appDataPath, "SaveLocations.txt");

        if (File.Exists(saveLocationsFile))
        {
          _saveLocations = FileUtility.ReadLinesFromFile(saveLocationsFile);
        }
      }
      catch (Exception ex)
      {
        // 忽略错误，使用空的最近保存位置列表
        _saveLocations = new List<string>();
        Debug.WriteLine("加载最近保存位置时出错: " + ex.Message);
      }
    }

    /// <summary>
    /// 保存最近保存的位置
    /// </summary>
    public void SaveSaveLocations()
    {
      try
      {
        string appDataPath = FileUtility.GetAppDataDirectory();
        string saveLocationsFile = Path.Combine(appDataPath, "SaveLocations.txt");
        FileUtility.SaveLinesToFile(saveLocationsFile, _saveLocations);
      }
      catch (Exception ex)
      {
        // 忽略错误
        Debug.WriteLine("保存最近保存位置时出错: " + ex.Message);
      }
    }

    /// <summary>
    /// 添加路径到最近保存位置列表
    /// </summary>
    /// <param name="path">要添加的路径</param>
    public void AddToSaveLocations(string path)
    {
      if (string.IsNullOrEmpty(path)) return;

      // 移除相同的路径（如果存在）
      _saveLocations.Remove(path);

      // 添加到列表开头
      _saveLocations.Insert(0, path);

      // 限制最近位置数量
      if (_saveLocations.Count > MaxSaveLocations)
      {
        _saveLocations.RemoveAt(_saveLocations.Count - 1);
      }

      // 保存更新后的列表
      SaveSaveLocations();
    }

    /// <summary>
    /// 清除保存位置列表
    /// </summary>
    public void ClearSaveLocations()
    {
      _saveLocations.Clear();
      SaveSaveLocations();
    }
  }
}