using PLC.Convert.LSCore;
using PLC.Convert.LSCore.Expression;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reactive.Subjects;
using System.Text.RegularExpressions;
using System.Xml.Linq;

/// <summary>
/// A helper class providing methods for processing specific formats of string data.
/// </summary>
public static class ImportHelper
{

    public static class ImportEvent
    {
        public static Subject<string> Notify = new Subject<string>();
    }

    /// <summary>
    /// Groups a sequence of strings based on specified delimiters.
    /// </summary>
    /// <param name="source">The source sequence of strings.</param>
    /// <param name="delimiters">A list of strings that are used as delimiters for grouping.</param>
    /// <returns>An enumerable of string groups, each represented as an enumerable of strings.</returns>
    public static IEnumerable<IEnumerable<string>> GroupByDelimiter(this IEnumerable<string> source, List<string> delimiters)
    {
        List<string> group = new();
        foreach (string item in source)
        {
            if (delimiters.Any(w => item.StartsWith(w)))
            {
                if (group.Any()) // Ensures skipping of empty groups
                {
                    yield return new List<string>(group);
                    group.Clear();
                }
                group.Add(item);
                continue; // Skips the delimiter itself
            }
            group.Add(item);
        }

        if (group.Any())
        {
            yield return group; // Returns the final group if it's not empty
        }
    }

    /// <summary>
    /// Extracts the rung and line number from a sequence of strings representing rung lines.
    /// </summary>
    /// <param name="rungLines">The sequence of strings representing rung lines.</param>
    /// <returns>A tuple containing the rung number and line number.</returns>
    public static Tuple<int, int> GetRungNLineNumber(IEnumerable<string> rungLines)
    {
        IEnumerable<string> lines = rungLines.Where(s => s.StartsWith("[Rung:"));

        if (lines.Any())
        {
            string line = lines.First();
            int rungNum = Convert.ToInt32(line.Split(',')[0].Split(':')[1]);
            int lineNum = Convert.ToInt32(line.Split(',')[1].Split(':')[1].TrimEnd(']'));

            return Tuple.Create(rungNum, lineNum);
        }
        else
        {
            return Tuple.Create(0, 0); // Returns (0, 0) if no matching lines are found
        }
    }

    /// <summary>
    /// Extracts call parameters from a sequence of strings representing rung lines.
    /// </summary>
    /// <param name="rungLines">The sequence of strings representing rung lines.</param>
    /// <param name="mainProgram">The name of the main program.</param>
    /// <param name="scanName">The name of the scan.</param>
    /// <param name="rungNum">The rung number.</param>
    /// <returns>An enumerable of CallPara objects representing the call parameters.</returns>
    public static IEnumerable<CallPara> GetCallParas(IEnumerable<string> rungLines, string taskName, int line, string configName)
    {
        string filterText = "[Variable:";
        IEnumerable<string> varis = rungLines.Where(s => s.StartsWith(filterText)).Select(s => s.TrimStart('[').TrimEnd(']').Trim());

        //ex [Variable: tagName: address: type: posXY: 상시 ON]
        //ex [Variable: _ON: %FX153: 1: 4,0: 상시 ON]
        return varis.Select(s =>
        {
            var items = s.Split(':').Select(s => s.Trim()).ToArray();
            string tagName = items[1];
            string address = items[2];
            string dataType = items[3];
            string posXY = items[4];
            string comment = items[5];

            int xgiOffset = CallParaUtil.IsXGIRuntime ? 100 : 0;        
            SymbolDataType sdt = SymbolUtil.GetDataTypeFromIndex(xgiOffset + Convert.ToInt32(dataType));
            bool isGlobal = false;
            //이름이 없으면 isGlobal
            if (tagName == "")
            {
                isGlobal = true;
            }
            else
            {
                var globalKey = SymbolUtil.GetKey(taskName, tagName, sdt, true);
                isGlobal = ImportSymbol.Symbols.ContainsKey(globalKey) ? true : false;
            }


            int posX = Convert.ToInt32(posXY.Split(',')[1]);
            int posY = Convert.ToInt32(posXY.Split(',')[0]);

            return new CallPara(taskName, tagName, address, line, posX, posY,  configName, comment, sdt, CallParaUtil.IsXGIRuntime, isGlobal);
        });
    }

 

    /// <summary>
    /// Splits a string by a specified delimiter, ignoring delimiters that are inside brackets.
    /// </summary>
    /// <param name="input">The input string to be split.</param>
    /// <param name="delimiter">The character used as a delimiter for splitting.</param>
    /// <returns>An array of strings resulting from the split operation.</returns>
    public static string[] SplitIgnoringBrackets(string input, char delimiter)
    {
        List<string> result = new();
        int start = 0;
        bool inBracket = false;

        for (int i = 0; i < input.Length; i++)
        {
            if (input[i] == '[')
            {
                inBracket = true;
            }
            else if (input[i] == ']')
            {
                inBracket = false;
            }
            else if (!inBracket && input[i] == delimiter)
            {
                result.Add(input.Substring(start,  i-start));
                start = i + 1;
            }
        }

        if (start < input.Length)
        {
            result.Add(input.Substring(start, input.Length - start));
        }

        return result.ToArray();
    }
}
