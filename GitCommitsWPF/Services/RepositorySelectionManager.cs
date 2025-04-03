using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using GitCommitsWPF.Models;
using GitCommitsWPF.Services;
using GitCommitsWPF.Utils;

namespace GitCommitsWPF.Services
{
  /// <summary>
  /// 负责管理仓库选择相关功能
  /// </summary>
  public class RepositorySelectionManager
  {
    private TextBox _pathsTextBox;
    private CheckBox _verifyGitPathsCheckBox;
    private CheckBox _chooseSystemCheckBox;
    private string _tempScanPath;

    private PathBrowserManager _pathBrowserManager;
    private DialogManager _dialogManager;
    private LocationManager _locationManager;
    private GitOperationsManager _gitOperationsManager;
    private OutputManager _outputManager;

    /// <summary>
    /// 获取或设置临时扫描路径
    /// </summary>
    public string TempScanPath
    {
      get { return _tempScanPath; }
      set { _tempScanPath = value; }
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    public RepositorySelectionManager(
        DialogManager dialogManager,
        LocationManager locationManager,
        GitOperationsManager gitOperationsManager,
        OutputManager outputManager)
    {
      _dialogManager = dialogManager;
      _locationManager = locationManager;
      _gitOperationsManager = gitOperationsManager;
      _outputManager = outputManager;
      _tempScanPath = string.Empty;
    }

    /// <summary>
    /// 初始化仓库选择管理器
    /// </summary>
    public void Initialize(
        TextBox pathsTextBox,
        CheckBox verifyGitPathsCheckBox,
        CheckBox chooseSystemCheckBox,
        PathBrowserManager pathBrowserManager)
    {
      _pathsTextBox = pathsTextBox;
      _verifyGitPathsCheckBox = verifyGitPathsCheckBox;
      _chooseSystemCheckBox = chooseSystemCheckBox;
      _pathBrowserManager = pathBrowserManager;
    }

    /// <summary>
    /// 清空路径文本框
    /// </summary>
    public void ClearPaths()
    {
      if (_pathsTextBox != null)
      {
        _pathsTextBox.Text = string.Empty;
      }
    }

    /// <summary>
    /// 清空临时扫描路径
    /// </summary>
    public void ClearTempScanPath()
    {
      _tempScanPath = string.Empty;
    }

    /// <summary>
    /// 验证路径是否为Git仓库
    /// </summary>
    public bool ValidatePath(string path)
    {
      if (string.IsNullOrEmpty(path))
        return false;

      if (_verifyGitPathsCheckBox != null && _verifyGitPathsCheckBox.IsChecked == true)
      {
        bool isValid = _gitOperationsManager.IsGitRepository(path);

        if (!isValid)
        {
          _dialogManager.ShowCustomMessageBox("验证失败", $"路径未通过验证，不是Git仓库: {path}", false);
        }

        return isValid;
      }

      return true; // 如果不验证，则总是返回true
    }

    /// <summary>
    /// 获取当前路径列表
    /// </summary>
    public List<string> GetPathsList()
    {
      List<string> paths = new List<string>();

      if (_pathsTextBox != null && !string.IsNullOrWhiteSpace(_pathsTextBox.Text))
      {
        paths.AddRange(_pathsTextBox.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
      }

      return paths;
    }

    /// <summary>
    /// 添加路径到路径文本框
    /// </summary>
    public void AddPath(string path)
    {
      // 确保路径不为空
      string trimmedPath = path?.Trim();

      if (_pathsTextBox != null && !string.IsNullOrEmpty(trimmedPath))
      {
        if (!string.IsNullOrEmpty(_pathsTextBox.Text))
        {
          _pathsTextBox.Text += Environment.NewLine;
        }
        _pathsTextBox.Text += trimmedPath;
      }
    }

    /// <summary>
    /// 显示Git路径确认对话框
    /// </summary>
    public bool ShowGitPathConfirmDialog()
    {
      return _dialogManager.ShowCustomConfirmDialog(
          "未指定路径",
          "您没有指定任何路径。是否希望选择一个Git仓库路径？");
    }

    /// <summary>
    /// 检查是否有路径，如果没有则提示用户
    /// </summary>
    public bool CheckAndPromptForPaths()
    {
      List<string> paths = GetPathsList();

      if (paths.Count == 0)
      {
        ClearTempScanPath();

        bool shouldContinue = ShowGitPathConfirmDialog();
        if (!shouldContinue)
        {
          return false;
        }

        // 尝试浏览文件夹
        _pathBrowserManager.BrowseFolder();

        // 重新获取路径
        paths = GetPathsList();

        if (paths.Count == 0)
        {
          _outputManager.UpdateOutput("未选择任何路径，操作取消。");
          return false;
        }
      }

      return true;
    }

    /// <summary>
    /// 检查并移除文本框内容中的重复路径
    /// </summary>
    public void CheckAndRemoveDuplicatePaths()
    {
      if (_pathsTextBox == null || string.IsNullOrWhiteSpace(_pathsTextBox.Text))
        return;

      // 捕获当前文本，确保中途不会修改
      string currentText = _pathsTextBox.Text;

      // 获取当前所有路径
      var paths = currentText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

      // 使用HashSet去重，保持添加顺序
      var uniquePaths = new List<string>();
      var pathSet = new HashSet<string>();

      foreach (var path in paths)
      {
        // 确保路径不为空白
        string trimmedPath = path.Trim();
        if (!string.IsNullOrWhiteSpace(trimmedPath) && !pathSet.Contains(trimmedPath))
        {
          pathSet.Add(trimmedPath);
          uniquePaths.Add(trimmedPath);
        }
      }

      // 如果存在重复路径或空白路径，则显示提示并更新文本框
      if (paths.Length != uniquePaths.Count)
      {
        // 使用Dispatcher确保在UI线程上更新UI
        _pathsTextBox.Dispatcher.Invoke(() =>
        {
          // 如果文本没有被其他操作改变
          if (_pathsTextBox.Text == currentText)
          {
            // 先记录当前光标位置
            int caretIndex = _pathsTextBox.CaretIndex;

            // 更新文本框内容为去重后的路径
            _pathsTextBox.Text = string.Join(Environment.NewLine, uniquePaths);

            // 尝试恢复光标位置（如果可能）
            if (caretIndex <= _pathsTextBox.Text.Length)
            {
              _pathsTextBox.CaretIndex = caretIndex;
            }
            else
            {
              _pathsTextBox.CaretIndex = _pathsTextBox.Text.Length;
            }

            // 只在实际移除了路径时显示提示
            _dialogManager.ShowCustomMessageBox("提示", "已自动移除重复或空白的路径", false);
          }
        });
      }
    }
  }
}