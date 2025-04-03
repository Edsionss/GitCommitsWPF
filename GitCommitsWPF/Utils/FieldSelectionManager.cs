using System;
using System.Collections.Generic;
using System.Windows.Controls;

namespace GitCommitsWPF.Utils
{
  /// <summary>
  /// 字段选择管理器，用于管理字段选择相关的功能
  /// </summary>
  public class FieldSelectionManager
  {
    private CheckBox _repositoryFieldCheckBox;
    private CheckBox _repoPathFieldCheckBox;
    private CheckBox _repoFolderFieldCheckBox;
    private CheckBox _commitIdFieldCheckBox;
    private CheckBox _authorFieldCheckBox;
    private CheckBox _dateFieldCheckBox;
    private CheckBox _messageFieldCheckBox;

    /// <summary>
    /// 初始化字段选择管理器
    /// </summary>
    /// <param name="repositoryFieldCheckBox">仓库字段复选框</param>
    /// <param name="repoPathFieldCheckBox">仓库路径字段复选框</param>
    /// <param name="repoFolderFieldCheckBox">仓库文件夹字段复选框</param>
    /// <param name="commitIdFieldCheckBox">提交ID字段复选框</param>
    /// <param name="authorFieldCheckBox">作者字段复选框</param>
    /// <param name="dateFieldCheckBox">日期字段复选框</param>
    /// <param name="messageFieldCheckBox">消息字段复选框</param>
    public void Initialize(
        CheckBox repositoryFieldCheckBox,
        CheckBox repoPathFieldCheckBox,
        CheckBox repoFolderFieldCheckBox,
        CheckBox commitIdFieldCheckBox,
        CheckBox authorFieldCheckBox,
        CheckBox dateFieldCheckBox,
        CheckBox messageFieldCheckBox)
    {
      _repositoryFieldCheckBox = repositoryFieldCheckBox;
      _repoPathFieldCheckBox = repoPathFieldCheckBox;
      _repoFolderFieldCheckBox = repoFolderFieldCheckBox;
      _commitIdFieldCheckBox = commitIdFieldCheckBox;
      _authorFieldCheckBox = authorFieldCheckBox;
      _dateFieldCheckBox = dateFieldCheckBox;
      _messageFieldCheckBox = messageFieldCheckBox;
    }

    /// <summary>
    /// 获取选择的字段列表
    /// </summary>
    /// <returns>字段名称列表</returns>
    public List<string> GetSelectedFields()
    {
      List<string> selectedFields = new List<string>();

      if (_repositoryFieldCheckBox.IsChecked == true) selectedFields.Add("Repository");
      if (_repoPathFieldCheckBox.IsChecked == true) selectedFields.Add("RepoPath");
      if (_repoFolderFieldCheckBox.IsChecked == true) selectedFields.Add("RepoFolder");
      if (_commitIdFieldCheckBox.IsChecked == true) selectedFields.Add("CommitId");
      if (_authorFieldCheckBox.IsChecked == true) selectedFields.Add("Author");
      if (_dateFieldCheckBox.IsChecked == true) selectedFields.Add("Date");
      if (_messageFieldCheckBox.IsChecked == true) selectedFields.Add("Message");

      return selectedFields;
    }

    /// <summary>
    /// 检查特定字段是否被选中
    /// </summary>
    /// <param name="fieldName">字段名称</param>
    /// <returns>是否选中</returns>
    public bool IsFieldSelected(string fieldName)
    {
      switch (fieldName)
      {
        case "Repository":
          return _repositoryFieldCheckBox.IsChecked == true;
        case "RepoPath":
          return _repoPathFieldCheckBox.IsChecked == true;
        case "RepoFolder":
          return _repoFolderFieldCheckBox.IsChecked == true;
        case "CommitId":
          return _commitIdFieldCheckBox.IsChecked == true;
        case "Author":
          return _authorFieldCheckBox.IsChecked == true;
        case "Date":
          return _dateFieldCheckBox.IsChecked == true;
        case "Message":
          return _messageFieldCheckBox.IsChecked == true;
        default:
          return false;
      }
    }

    /// <summary>
    /// 设置所有字段的选中状态
    /// </summary>
    /// <param name="isChecked">是否选中</param>
    public void SetAllFields(bool isChecked)
    {
      _repositoryFieldCheckBox.IsChecked = isChecked;
      _repoPathFieldCheckBox.IsChecked = isChecked;
      _repoFolderFieldCheckBox.IsChecked = isChecked;
      _commitIdFieldCheckBox.IsChecked = isChecked;
      _authorFieldCheckBox.IsChecked = isChecked;
      _dateFieldCheckBox.IsChecked = isChecked;
      _messageFieldCheckBox.IsChecked = isChecked;
    }

    /// <summary>
    /// 获取字段选中状态的映射
    /// </summary>
    /// <returns>字段名称到选中状态的映射</returns>
    public Dictionary<string, bool> GetFieldSelectionMap()
    {
      return new Dictionary<string, bool>
      {
        { "Repository", _repositoryFieldCheckBox.IsChecked == true },
        { "RepoPath", _repoPathFieldCheckBox.IsChecked == true },
        { "RepoFolder", _repoFolderFieldCheckBox.IsChecked == true },
        { "CommitId", _commitIdFieldCheckBox.IsChecked == true },
        { "Author", _authorFieldCheckBox.IsChecked == true },
        { "Date", _dateFieldCheckBox.IsChecked == true },
        { "Message", _messageFieldCheckBox.IsChecked == true }
      };
    }
  }
}