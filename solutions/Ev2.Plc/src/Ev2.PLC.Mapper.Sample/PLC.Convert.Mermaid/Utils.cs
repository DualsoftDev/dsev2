using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Window;

namespace PLC.Convert.Mermaid
{

    public static class FileOpenSave
    {
        /// <summary>
        /// Opens a file dialog to select files.
        /// </summary>
        /// <returns>An array of file paths of the selected files or null if no file is selected.</returns>
        public static string[]? OpenFiles(bool multiselect = false, string fileType = "xgwx")
        {
            using OpenFileDialog openFileDialog = new();
            openFileDialog.Filter =
            $"{fileType} file (*.{fileType})|*.{fileType}|" +
            "All files (*.*)|*.*";
            openFileDialog.Multiselect = multiselect;
            if (openFileDialog.ShowDialog() != DialogResult.OK)
            {
                return null;
            }

            string file = openFileDialog.FileNames.First();

            return openFileDialog.FileNames;
        }

        public static List<string> OpenDirFiles()
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "폴더를 선택하세요.";
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    string selectedPath = dialog.SelectedPath;
                    return GetAllXgwxFiles(selectedPath);

                }

                return null;    
            }

            List<string> GetAllXgwxFiles(string folderPath)
            {
                List<string> files = new List<string>();

                try
                {
                    // 현재 폴더 내 .xgwx 파일 추가
                    files.AddRange(Directory.GetFiles(folderPath, "*.xgwx", SearchOption.AllDirectories));
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"파일 검색 중 오류 발생: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

                return files;
            }
        }
               

        /// <summary>
        /// Sets the last opened file paths in the registry.
        /// </summary>
        /// <param name="filePath">The file paths to set as the last opened files.</param>


        /// <summary>
        /// Generates a new file path based on the given path and the current timestamp.
        /// </summary>
        /// <param name="path">The original file path.</param>
        /// <returns>A new file path with a timestamp.</returns>
        public static string GetNewPath(string path)
        {
            string newPath = Path.Combine(Path.GetDirectoryName(path)
                        , string.Join("_", Path.GetFileNameWithoutExtension(path)));

            string extension = Path.GetExtension(path);
            string excelName = Path.GetFileNameWithoutExtension(newPath) + $"_{DateTime.Now:yyMMdd(HH-mm-ss)}.{extension}";
            string excelDirectory = Path.Combine(Path.GetDirectoryName(newPath), Path.GetFileNameWithoutExtension(excelName));
            _ = Directory.CreateDirectory(excelDirectory);

            return Path.Combine(excelDirectory, excelName);
        }
    }

    public static partial class EmLinq
    {
        /// <summary>
        /// Create IEnumerable from element
        /// http://stackoverflow.com/questions/1577822/passing-a-single-item-as-ienumerablet
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="item"></param>
        /// <returns></returns>
        public static IEnumerable<T> ToEnumerable<T>(this T item) { yield return item; }
    }


}
