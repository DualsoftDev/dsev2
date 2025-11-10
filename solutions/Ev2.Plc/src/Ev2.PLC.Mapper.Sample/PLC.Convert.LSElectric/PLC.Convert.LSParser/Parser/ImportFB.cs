using PLC.Convert.LSCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>
/// A class for importing and processing function blocks (FB) and functions for automation systems,
/// particularly for handling IEC standard and user-defined functionalities.
/// </summary>
public static class ImportFB
{
    /// <summary>
    /// Enumeration for input, output, and input-output variable types in a function block.
    /// </summary>
    public enum FB_INOUT
    {
        VAR_IN,
        VAR_OUT,
        VAR_IN_OUT
    }
    /// <summary>
    /// Enumeration for different types of function blocks.
    /// </summary>
    public enum FB_Type
    {
        FUNCTION_BLOCK,
        FUNCTION,
        FUNCTION_BLOCK_USER,
        FUNCTION_USER,
    }

    /// <summary>
    /// Stores IEC standard function definitions.
    /// </summary>
    public static Dictionary<string, string[]> IECFuncs = new();

    /// <summary>
    /// Stores user-defined function definitions.
    /// </summary>
    public static Dictionary<string, string[]> UserFuncs = new();

    /// <summary>
    /// Reads and parses a file to create a dictionary of function block information.
    /// </summary>
    /// <param name="path">File path of the function block definitions.</param>
    /// <returns>A dictionary with function block names as keys and their definition lines as values.</returns>
    /// <exception cref="Exception">Thrown when the file at the specified path is not found.</exception>
    public static Dictionary<string, string[]> CreateReader(string path)
    {
        if (!File.Exists(path))
        {
            throw new Exception($"Could not find a part of the path  {path}");
        }

        List<string> textlines = File.ReadAllLines(path).ToList();

        List<string> lines = textlines
                            .Where(s => !s.StartsWith("#"))  //#BEGIN_FUNC: ARY_ASC_TO_BCD 제거
                            .Select(s => s.Trim())
                            .ToList();

        List<List<string>> groups = new();
        List<string> currentGroup = new();

        foreach (string line in lines)
        {
            if (!string.IsNullOrEmpty(line))
            {
                currentGroup.Add(line);
            }
            else if (currentGroup.Count > 0)
            {
                groups.Add(new List<string>(currentGroup));
                currentGroup.Clear();
            }
        }

        if (currentGroup.Count > 0)
        {
            groups.Add(currentGroup);
        }

        Dictionary<string, string[]> dictionary = new();

        foreach (List<string> group in groups)
        {
            string fName = null;

            string fNameLine = group.Find(l => l.StartsWith("FNAME:"));

            Match match = Regex.Match(fNameLine, @"FNAME: (.*)$");
            fName = match.Success ? match.Groups[1].Value : throw new InvalidOperationException("XG5000_IEC_FUNC read ERROR");

            if (fName.Contains("***"))  //풀어서 제공받음
            {
                continue;
            }

            if (!dictionary.ContainsKey(fName))
            {
                dictionary.Add(fName, group.ToArray());
            }
            else
            {
                //내용 같으면 넘어감 
                if (!dictionary[fName].SequenceEqual(group.ToArray()))
                {
                    throw new InvalidOperationException($"LD, ST 동일한 함수가 존재 하지만 [{fName}] 내용이 다름니다. ERROR");
                }
            }
        }

        return dictionary;
    }
    /// <summary>
    /// Generates an XML representation of a function block.
    /// </summary>
    /// <param name="info">Tuple containing source and target function block names, instance name, and index.</param>
    /// <returns>A string with the XML representation of the function block.</returns>
    public static string GetFBXML((string fnameSource, string fnameTarget, string instance, int index) info)
    {
        Dictionary<string, string[]> reader = ImportFB.IECFuncs;
        string[] lines = reader[info.fnameSource];

        IEnumerable<string> result = lines.Select(line =>
            {
                return line.StartsWith("FNAME: ")
                    ? $"FNAME: {info.fnameTarget}"
                    : line.StartsWith("INSTANCE: ") ? $"INSTANCE: {info.instance}" : line.StartsWith("INDEX: ") ? $"INDEX: {info.index}" : line;
            });

        return string.Join("&#xA;", result) + " &#xA;";
    }

    /// <summary>
    /// Determines the type of a function block based on its name.
    /// </summary>
    /// <param name="fnameSource">The name of the function block.</param>
    /// <returns>The type of the function block.</returns>
    public static FB_Type GetFBType(string fnameSource)
    {
        string[] lines = GetFB(fnameSource);

        FB_Type fbType = FB_Type.FUNCTION;
        string line = lines.First(f => f.StartsWith("TYPE:"));
        fbType = line switch
        {
            "TYPE: function_block" => FB_Type.FUNCTION_BLOCK,
            "TYPE: function" => FB_Type.FUNCTION,
            "TYPE: function_block, user" => FB_Type.FUNCTION_BLOCK_USER,
            "TYPE: function, user" => FB_Type.FUNCTION_USER,
            _ => throw new Exception($"does not have type line : {fnameSource}"),
        };
        return fbType;
    }

    /// <summary>
    /// Gets input/output information for a function block.
    /// </summary>
    /// <param name="fnameSource">The name of the function block.</param>
    /// <param name="xgiStyleFB">Boolean indicating if it is an XGI style function block.</param>
    /// <returns>List of input/output types.</returns>
    public static List<FB_INOUT> GetInOutInfo(string fnameSource, bool styleNotFB)
    {
        string[] lines = GetFB(fnameSource);

        if (styleNotFB && CallParaUtil.IsXGIRuntime)
        {
            return getXgiStyleFC(lines);
        }
        else
        {
            List<FB_INOUT> xgkInOuts = new();
            List<string> inoutLines = lines.Where(w => w.StartsWith("VAR")).ToList();
            int inoutCnt = 0;
            for (int i = 0; i < inoutLines.Count(); i++)
            {
                if (inoutLines[i].StartsWith("VAR_IN:"))
                {
                    xgkInOuts.Add(FB_INOUT.VAR_IN);
                }
                else if (inoutLines[i].StartsWith("VAR_IN_OUT:"))
                {
                    xgkInOuts.Add(FB_INOUT.VAR_IN);
                    inoutCnt++;
                }

                if (inoutLines[i].StartsWith("VAR_OUT:"))
                {
                    xgkInOuts.Add(FB_INOUT.VAR_OUT);
                }
            }
            for (int i = 0; i < inoutCnt; i++)
                    xgkInOuts.Add(FB_INOUT.VAR_OUT);

            return xgkInOuts;
        }
    }

    /// <summary>
    /// Helper method to get input/output information for XGI style function blocks.
    /// </summary>
    /// <param name="lines">The lines defining the function block.</param>
    /// <returns>A list of <see cref="FB_INOUT"/> indicating the input/output configuration.</returns>
    private static List<FB_INOUT> getXgiStyleFC(string[] lines)
    {
        int inCnt = lines.Where(w => w.StartsWith("VAR_IN:")).Count();
        int outCnt = lines.Where(w => w.StartsWith("VAR_OUT:")).Count();
        int inOutCnt = lines.Where(w => w.StartsWith("VAR_IN_OUT:")).Count();
        int totalCnt = inCnt + outCnt + (inOutCnt * 2);


        Dictionary<int, FB_INOUT> directions = new();
        for (int i = 0; i < totalCnt; i++)
        {
            directions.Add(i, FB_INOUT.VAR_OUT); //임시로 OUT 배정, IN 뒤에서 처리
        }

        List<int> varInOrder = new();
        List<int> varOutOrder = new();
        List<int> varInOutOrder = new();

        bool firstOut = true;
        List<string> inoutLines = lines.Where(w => w.StartsWith("VAR")).ToList();
        for (int i = 1; i < inoutLines.Count(); i++) //처음은 무조건 VAR_IN 입력 미리함
        {
            string line = inoutLines[i];
            if (line.StartsWith("VAR_IN:"))
            {
                varInOrder.Add(i + 1);  //2번째 VAR_OUT 처리
            }
            else if (line.StartsWith("VAR_IN_OUT:"))
            {
                varInOutOrder.Add(i + 1);  //2번째 VAR_OUT 처리
            }
            else if (line.StartsWith("VAR_OUT:"))
            {
                if (firstOut) //처음은 나오는  VAR_OUT 입력 미리함
                {
                    firstOut = false;
                    continue;
                }
                varOutOrder.Add(i);
            }
        }

        directions[0] = FB_INOUT.VAR_IN;
        directions[1] = FB_INOUT.VAR_OUT;

        foreach (int index in varInOrder)
        {
            directions[index] = FB_INOUT.VAR_IN;
        }

        foreach (int index in varInOutOrder)
        {
            directions[index] = FB_INOUT.VAR_IN;
            directions[index] = FB_INOUT.VAR_OUT;
        }
        foreach (int index in varOutOrder)
        {
            directions[index] = FB_INOUT.VAR_OUT;
        }

        return directions.Values.ToList();
    }

    /// <summary>
    /// Retrieves the definition lines of a function block.
    /// </summary>
    /// <param name="fnameSource">The name of the function block.</param>
    /// <returns>An array of strings containing the definition lines of the function block.</returns>
    /// <exception cref="Exception">Thrown if the function block is not found.</exception>
    private static string[] GetFB(string fnameSource)
    {
        string[] lines = IECFuncs.ContainsKey(fnameSource)
            ? IECFuncs[fnameSource]
            : UserFuncs.ContainsKey(fnameSource) ? UserFuncs[fnameSource] : throw new Exception($"not found : {fnameSource}");
        return lines;
    }

    /// <summary>
    /// Checks if a given function or function block is defined.
    /// </summary>
    /// <param name="fnameSource">The name of the function or function block.</param>
    /// <returns>True if the function or function block exists; otherwise, false.</returns>
    public static bool IsFuncOrFB(string fnameSource)
    {
        return IECFuncs.ContainsKey(fnameSource) || UserFuncs.ContainsKey(fnameSource);
    }

}
