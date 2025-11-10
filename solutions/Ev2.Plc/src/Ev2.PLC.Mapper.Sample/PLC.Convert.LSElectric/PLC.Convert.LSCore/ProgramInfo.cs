using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PLC.Convert.LSCore
{
    /// <summary>
    /// Represents program information parsed from a specified file.
    /// </summary>
    public class ProgramInfo
    {
        /// <summary>
        /// Initializes a new instance of the ProgramInfo class and parses the file at the given filePath.
        /// </summary>
        /// <param name="filePath">The file path of the program information file.</param>
        public ProgramInfo(string filePath)
        {
            // Check if the file exists at the given path.
            if (!File.Exists(filePath))
            {
                throw new Exception($"Could not find a part of the path  {filePath}");
            }

            // Initialize lists and dictionary to store various program details.
            GlobalVariables = new List<string>();
            TASKs = new Dictionary<string, string>(); // scan Order, taskName
            USER_FUN_FBs = new List<string>();
            USER_DATA_TYPEs = new List<string>();
            Dir = Path.GetDirectoryName(filePath);

            // Parse the file to extract program information.
            ParseFile(filePath);
        }

        // Properties to get various details about the program.
        public string Dir { get; private set; }
        public string[] GlobalVariablePaths => GlobalVariables.Select(s => Dir + $"/{s}").ToArray();
        public string[] TASKPaths => TASKs.Values.Select(s => Dir + $"/{s}.nme").ToArray();
        public string[] LocalSymPaths => TASKs.Values.Select(s => Dir + $"/{s}.csv").ToArray();
        public string[] UserDataPaths => USER_DATA_TYPEs.Select(s => Dir + $"/{s}").ToArray();
        public List<string> GlobalVariables { get; private set; }
        public Dictionary<string, string> TASKs { get; private set; } // scan Order, taskName
        public List<string> USER_FUN_FBs { get; private set; }
        public List<string> USER_DATA_TYPEs { get; private set; }
        public List<string> IPs { get; private set; }

        /// <summary>
        /// Parses the file at the given filePath to extract program information.
        /// </summary>
        /// <param name="filePath">The file path to be parsed.</param>
        private void ParseFile(string filePath)
        {
            string[] lines = File.ReadAllLines(filePath);
            List<string> currentList = null;
            List<string> globalvariables = new();
            List<string> tasks = new();
            List<string> user_fun_fbs = new();
            List<string> user_data_types = new();
            List<string> ipList = new();

            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue; // Skip empty lines
                }

                switch (line)
                {
                    case string _ when line.StartsWith("[GlobalVariable]"):
                        currentList = globalvariables;
                        break;
                    case string _ when line.StartsWith("[TASK]"):
                        currentList = tasks;
                        break;
                    case string _ when line.StartsWith("[USER_FUN_FB]"):
                        currentList = user_fun_fbs;
                        break;
                    case string _ when line.StartsWith("[USER_DATA_TYPE]"):
                        currentList = user_data_types;
                        break;
                    case string _ when line.StartsWith("[IP]"):
                        currentList = ipList;
                        break;
                    default:
                        currentList?.Add(line.TrimStart('[').TrimEnd(']'));
                        break;
                }
            }

            static string getdata(string s)
            {
                var trimStart = s.Split(',').First()+",";
                string result = s.Substring(trimStart.Length, s.Length - trimStart.Length).Trim();
                string modifiedString = result.EndsWith(".nme")
                ? result.Replace(".nme","") // 마지막 .nme을 제거
                : result; // .nme이 없으면 원래 문자열 반환

                return modifiedString;
            }


            GlobalVariables = globalvariables.Select(getdata).ToList();
            USER_FUN_FBs = user_fun_fbs.Select(getdata).ToList();
            USER_DATA_TYPEs = user_data_types.Select(getdata).ToList();
            IPs = ipList.Select(s=>s.Split(',').Last().Trim()).ToList();
            
            TASKs = tasks.ToDictionary(s =>
                        s.Split(',')[0].Split(':')[0] +
                        System.Convert.ToInt32(s.Split(',')[0].Split(':')[1]).ToString("000000")
                        , getdata);
        }

    }
}
