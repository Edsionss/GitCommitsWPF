using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace GitCommitsWPF.Utils
{
    /// <summary>
    /// 提供文件操作的工具类
    /// </summary>
    public static class FileUtility
    {
        /// <summary>
        /// 保存文本内容到文件
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <param name="content">要保存的文本内容</param>
        public static void SaveTextToFile(string path, string content)
        {
            try
            {
                // 确保目录存在
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // 写入文件
                File.WriteAllText(path, content, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                throw new Exception("保存文件时出错: " + ex.Message, ex);
            }
        }

        /// <summary>
        /// 保存字符串列表到文件
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <param name="lines">要保存的字符串列表</param>
        public static void SaveLinesToFile(string path, IEnumerable<string> lines)
        {
            try
            {
                // 确保目录存在
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // 写入文件
                File.WriteAllLines(path, lines, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                throw new Exception("保存文件时出错: " + ex.Message, ex);
            }
        }

        /// <summary>
        /// 从文件读取所有文本
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <returns>文件内容</returns>
        public static string ReadTextFromFile(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    throw new FileNotFoundException("文件不存在", path);
                }

                return File.ReadAllText(path, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                throw new Exception("读取文件时出错: " + ex.Message, ex);
            }
        }

        /// <summary>
        /// 从文件读取所有行
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <returns>文件的所有行</returns>
        public static List<string> ReadLinesFromFile(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    throw new FileNotFoundException("文件不存在", path);
                }

                return new List<string>(File.ReadAllLines(path, Encoding.UTF8));
            }
            catch (Exception ex)
            {
                throw new Exception("读取文件时出错: " + ex.Message, ex);
            }
        }

        /// <summary>
        /// 获取应用程序数据目录
        /// </summary>
        /// <param name="folderName">文件夹名称</param>
        /// <returns>完整的应用数据目录路径</returns>
        public static string GetAppDataDirectory(string folderName = "GitCommitsWPF")
        {
            string appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                folderName);
            
            if (!Directory.Exists(appDataPath))
            {
                Directory.CreateDirectory(appDataPath);
            }
            
            return appDataPath;
        }
    }
} 