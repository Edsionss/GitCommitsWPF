using System;

namespace GitCommitsWPF.Models
{
    /// <summary>
    /// 表示一个Git提交信息的类
    /// </summary>
    public class CommitInfo
    {
        /// <summary>
        /// 仓库名称
        /// </summary>
        public string Repository { get; set; }
        
        /// <summary>
        /// 仓库完整路径
        /// </summary>
        public string RepoPath { get; set; }
        
        /// <summary>
        /// 仓库文件夹名称
        /// </summary>
        public string RepoFolder { get; set; }
        
        /// <summary>
        /// 提交ID（SHA）
        /// </summary>
        public string CommitId { get; set; }
        
        /// <summary>
        /// 作者名称
        /// </summary>
        public string Author { get; set; }
        
        /// <summary>
        /// 提交日期
        /// </summary>
        public string Date { get; set; }
        
        /// <summary>
        /// 提交消息
        /// </summary>
        public string Message { get; set; }
    }
} 