using Microsoft.VisualBasic.FileIO;
using PLC.Convert.LSCore;
using PLC.Convert.LSCore.XGTTag;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using System.Threading.Tasks.Sources;
using System.Xml.Linq;
using static PLC.Convert.LSCore.XGTTag.XGTDevice;
using static System.Windows.Forms.LinkLabel;

/// <summary>
/// A class providing methods for importing, storing, and managing symbols used in a program.
/// </summary>
public static class ImportSymbol
{
    /// <summary>
    /// Stores all symbols, both global and local.
    /// </summary>
    public static Dictionary<string, Symbol> Symbols = new();
    public static Dictionary<string, Symbol> UserDataSymbols = new();
    public static Dictionary<string, Symbol> FBDataSymbols = new();
    /// <summary>
    /// Stores all symbols, both global and local.
    /// </summary>
    public static Dictionary<string, Symbol> FindAutoDeviceSymbols = new();
    /// <summary>
    /// Array of symbols used as contacts.
    /// </summary>
    public static Dictionary<string, Symbol> OnlyContacts = new();



    /// <summary>
    /// Retrieves a symbol by its name, considering global and local scope.
    /// </summary>
    /// <param name="name">The name of the symbol to retrieve.</param>
    /// <param name="pouName">The name of the Program Organization Unit (POU) for local symbols.</param>
    /// <returns>The corresponding Symbol object.</returns>
    public static Symbol GetSymbol(CallPara cp)
    {
        //global 영역에서 먼저 찾는다.
        var findName = cp.TagName == "" ? cp.Address : cp.TagName;
        var key = SymbolUtil.GetKey("", findName, cp.SymbolDataType, true);
        if (Symbols.ContainsKey(key))
            return Symbols[key];
        else if (cp.TagName == "" && cp.Address != "")
            return getDirectSymbol(cp);
        else if (cp.TagName != "" && cp.Address != "" && !cp.Address.ToUpper().StartsWith("A"))
        {
            if (cp.IsXgi)
                return getSystemSymbol(cp); 
            else
                return getXgkSymbol(cp);  //xgi는 Symbols 이미 있음
        }
        else
            return getAutoSymbol(cp);
    }

    private static Symbol getXgkSymbol(CallPara cp)
    {
        Symbol xgkSym = new Symbol("", cp.TagName);
        xgkSym.Address = cp.Address;
        xgkSym.IsGlobal = true;
        var t = getTagInfo(cp, xgkSym);

        var bitEnd = t.Item3 + SymbolUtil.GetSizeType(cp.SymbolDataType);
        xgkSym.UpdateParameter(cp.SymbolDataType, true, cp.Address, t.Item1, t.Item3, bitEnd);

        return getSymbolFromDic(xgkSym);
    }

    private static Symbol getSystemSymbol(CallPara cp)
    {
        var key = SymbolUtil.GetAutoDeviceKey("", cp.TagName);
        if (Symbols.ContainsKey(key))
            return Symbols[key];
        else
        {
            Symbol sysSym = new Symbol("", cp.TagName);
            sysSym.Address = cp.Address;
            sysSym.IsGlobal = true;

            Tuple<string, XGTDeviceSize, int> t = getTagInfo(cp, sysSym);

            if (t == null) { }

            sysSym.SymbolDataType = SymbolUtil.GetDataType(t.Item2);
            sysSym.Comment = cp.Comment;
            var bitEnd = t.Item3 + SymbolUtil.GetSizeType(sysSym.SymbolDataType);
            sysSym.UpdateParameter(sysSym.SymbolDataType, true, sysSym.Address, t.Item1, t.Item3, bitEnd);

            return getSymbolFromDic(sysSym);
        }
    }

    private static Tuple<string, XGTDeviceSize, int> getTagInfo(CallPara cp, Symbol sym)
    {
        return cp.IsXgi ? XGTParserXGI.LsTagXGIPattern(sym.Address)
                        : XGTParserXGK.LsTagXGKPattern(sym.Address, cp.SymbolDataType == SymbolDataType.BIT);
    }

    private static Symbol getAutoSymbol(CallPara cp)
    {
        var key = SymbolUtil.GetAutoDeviceKey(cp.MainProgram, cp.TagName);

        if (FindAutoDeviceSymbols.ContainsKey(key))
        {
            Symbol refSym  = FindAutoDeviceSymbols[key];
            Symbol autoSym = new Symbol(cp.MainProgram, cp.TagName);
            autoSym.Address = refSym.Address;
            autoSym.BitSize = SymbolUtil.GetSizeType(cp.SymbolDataType);

            Tuple<string, XGTDeviceSize, int> t = getTagInfo(cp, autoSym);

            if (t == null) { }

            var bitEnd = t.Item3 + autoSym.BitSize;
            autoSym.UpdateParameter(cp.SymbolDataType, false, autoSym.Address, t.Item1, t.Item3, bitEnd);

            return getSymbolFromDic(autoSym);
        }
        else  //user data autoSymbol or fb_instance
        {
            var userTypeName = cp.TagName.Split('.').First();
            var userVariable = cp.TagName.Split('.').Last().Split('[').First();
            var userkey = SymbolUtil.GetAutoDeviceKey(cp.MainProgram, userTypeName);
            var userSym = FindAutoDeviceSymbols[userkey];

            if(userSym.SymbolDataType == SymbolDataType.FB_INST)
            {
                var variKey = SymbolUtil.GetUserOrFBDataKey(userSym.TaskName, userTypeName);
                Symbol refSym = FBDataSymbols[variKey];

                Symbol fbInstSym = new Symbol(cp.MainProgram, cp.TagName);
                fbInstSym.Address = userSym.Address;

                Tuple<string, XGTDeviceSize, int> t = getTagInfo(cp, fbInstSym);
                if (t == null) { }
                var bitEnd = t.Item3 + refSym.BitStartOffset + SymbolUtil.GetSizeType(refSym.SymbolDataType);
                fbInstSym.UpdateParameter(cp.SymbolDataType, false, fbInstSym.Address, t.Item1, t.Item3, bitEnd);

                return getSymbolFromDic(fbInstSym);
            }
            else
            {
             
                Symbol autoSym = new Symbol(cp.MainProgram, cp.TagName);
                autoSym.Address = userSym.Address;

                Symbol refSym = autoSym;
                var variKey = SymbolUtil.GetUserOrFBDataKey(userSym.UserDataName, userVariable);
                if (UserDataSymbols.ContainsKey(key))
                    refSym = UserDataSymbols[variKey];


                Tuple<string, XGTDeviceSize, int> t = getTagInfo(cp, autoSym);
                if (t == null) { }
                var bitEnd = t.Item3 + refSym.BitStartOffset + SymbolUtil.GetSizeType(refSym.SymbolDataType);
                autoSym.UpdateParameter(cp.SymbolDataType, false, autoSym.Address, t.Item1, t.Item3, bitEnd);

                return getSymbolFromDic(autoSym);
            }
        }
    }

  
    private static Symbol getDirectSymbol(CallPara cp)
    {
        Symbol directSym = new Symbol("", cp.Address);
        directSym.Address = cp.Address;
        Tuple<string, XGTDeviceSize, int> t = getTagInfo(cp, directSym);

        if (t == null) { }
        directSym.SymbolDataType = SymbolUtil.GetDataType(t.Item2); 
        var bitEnd = t.Item3 + SymbolUtil.GetSizeType(directSym.SymbolDataType);
        directSym.UpdateParameter(directSym.SymbolDataType, true, cp.Address, t.Item1, t.Item3, bitEnd);

        return getSymbolFromDic(directSym);
    }
    private static Symbol getSymbolFromDic(Symbol symbol)
    {
        if (Symbols.ContainsKey(symbol.KeyName))
            return Symbols[symbol.KeyName];
        else
        {
            Symbols.Add(symbol.KeyName, symbol);
            return symbol;
        }
    }

    /// <summary>
    /// Loads symbols from an array of file paths.
    /// </summary>
    /// <param name="paths">An array of file paths containing symbol definitions.</param>
    /// <returns>A dictionary of symbols parsed from the files.</returns>
    public static Dictionary<string, Symbol> LoadGlobalSymbols(string[] paths)
    {
        FBDataSymbols = new();
        Dictionary<string, Symbol> symbols = new();
        try
        {
            foreach (var file in paths)
            {
                var data = ImportFile.ReadFileByLine(file).Skip(1);
                foreach (string line in data)
                {

                    Symbol symbol = ParseLineToSymbol(line, "");
                    if (symbol == null) continue;

                    symbol.IsGlobal = true;
                    symbol.UpdateKeyName(symbol.SymbolDataType, true);

                    if (!symbols.ContainsKey(symbol.KeyName))
                        symbols.Add(symbol.KeyName, symbol);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading file: {ex.Message}");
        }
        return symbols;
    }

    /// <summary>
    /// Loads symbols from an array of file paths.
    /// </summary>
    /// <param name="paths">An array of file paths containing symbol definitions.</param>
    /// <param name="globalSyms">An array of file paths containing symbol definitions.</param>
    /// <returns>A dictionary of symbols parsed from the files.</returns>
    public static Dictionary<string, Symbol> LoadUserDataSymbols(string[] paths)
    {
        Dictionary<string, Symbol> symbols = new();
        try
        {
            foreach (var file in paths)
            {
                if (!File.Exists(file)) continue; //xgk 는 없다.

                var taskName = Path.GetFileNameWithoutExtension(file);
                foreach (string line in ImportFile.ReadFileByLine(file).Skip(1))
                {
                    Symbol symbol = ParseLineToSymbol(line, taskName);
                    if (symbol == null) continue;

                    var key = SymbolUtil.GetUserOrFBDataKey(taskName, symbol.Name);

                    if (!symbols.ContainsKey(key))
                    {
                        symbol.UpdateKeyName(symbol.SymbolDataType, symbol.IsGlobal);
                        symbols.Add(key, symbol);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading file: {ex.Message}");
        }

        return symbols;
    }

    /// <summary>
    /// Loads symbols from an array of file paths.
    /// </summary>
    /// <param name="paths">An array of file paths containing symbol definitions.</param>
    /// <param name="globalSyms">An array of file paths containing symbol definitions.</param>
    /// <returns>A dictionary of symbols parsed from the files.</returns>
    public static Dictionary<string, Symbol> LoadAllSymbols(string[] paths, Dictionary<string, Symbol> globalSyms)
    {
        Dictionary<string, Symbol> symbols = globalSyms;
        try
        {
            foreach (var file in paths)
            {
                if (!File.Exists(file)) continue; //xgk 는 없다.

                var taskName = Path.GetFileNameWithoutExtension(file);
                foreach (string line in ImportFile.ReadFileByLine(file).Skip(1))
                {
                    Symbol symbol = ParseLineToSymbol(line, taskName);
                    if (symbol == null) continue;

                    var key = SymbolUtil.GetKey(taskName, symbol.Name, symbol.SymbolDataType, symbol.IsGlobal);

                    if (!symbols.ContainsKey(key))
                    {
                        symbol.UpdateKeyName(symbol.SymbolDataType, symbol.IsGlobal);
                        symbols.Add(key, symbol);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading file: {ex.Message}");
        }

        return symbols;
    }
    /// <summary>
    /// Parses a line of text into a Symbol object.
    /// </summary>
    /// <param name="line">The line of text representing a symbol.</param>
    /// <returns>The parsed Symbol object.</returns>
    public enum symColumn
    {
        Device = 0,
        BitStartOffset = 1,
        BitEndOffset = 2,
        BitSize = 3,
        Address = 4,
        VarKind = 5,
        VarName = 6,
        Type = 7,
        Comment = 8,
    }
    //Device BitStartOffset  BitEndOffset BitSize Address VarKind VarName Type    Comment
    //M	    100	            100	            1	    %MX100 VAR_GLOBAL  A  BOOL    TEST1
    //M	    101	            101	            1	    %MX101 VAR_GLOBAL  A0 BOOL    TEST2
    //M,00601408,00601423,16,%MW37588,VAR_GLOBAL,ZR37588,"WORD","#303.   RB01사양"3803"  용접기준"
    private static Symbol ParseLineToSymbol(string line, string taskName)
    {
        string[] split;
        var fixLine = FixMalformedQuotes(line);
        static string FixMalformedQuotes(string input)
        {
            // 1. 필드 내부의 잘못된 이중 큰따옴표를 제거 ("" → ")
            string result = Regex.Replace(input, "\"\"([^\"]+?)\"", "\"$1\"");

            // 2. CSV 필드 개수 확인 (9개 필드인지 확인)
            string pattern = "^(.*?,.*?,.*?,.*?,.*?,.*?,.*?,.*?,)(\".*\")$"; // 9번째 열을 정확히 추출하는 정규식
            Match match = Regex.Match(result, pattern);

            if (match.Success)
            {
                string firstPart = match.Groups[1].Value; // 앞의 8개 필드
                string lastField = match.Groups[2].Value; // 9번째 필드 (따옴표 포함)

                // 내부 큰따옴표만 공백으로 변환하고, 맨 끝의 큰따옴표는 유지
                lastField = "\"" + lastField.Substring(1, lastField.Length - 2).Replace("\"", " ") + "\"";

                // 다시 합쳐서 반환
                result = firstPart + lastField;
            }

            return result;
        }
        using (TextFieldParser parser = new TextFieldParser(new StringReader(fixLine)))
        {
             try
            {
                parser.TextFieldType = FieldType.Delimited;
                parser.SetDelimiters(",");
                parser.HasFieldsEnclosedInQuotes = true; // 큰따옴표 포함된 필드 처리

                split = parser.ReadFields();

                if (split == null || split.Length == 0)
                {
                    throw new Exception($"line is empty {line}");
                }

              
            }
            catch (Exception ex)
            {
                throw new Exception($"Error parsing line: {ex.Message}");   
            }
           
        }


        var tempName = split[(int)symColumn.VarName];
        var name = tempName == "" ? split[(int)symColumn.Address] 
                 : tempName;

        var type = split[(int)symColumn.Type];
        var varType = SymbolUtil.GetDataTypeFromString(type);

        if (varType == SymbolDataType.NONE)
        {
            if (UserDataSymbols.Select(s=>s.Key.Split('|').First()).Contains(type))
                varType = SymbolDataType.USERDATA;
            else
                varType = SymbolDataType.FB_INST;
        }



        Symbol sym = new Symbol(taskName, name)
        {
            Device = split[(int)symColumn.Device],
            BitStartOffset = Convert.ToInt32(split[(int)symColumn.BitStartOffset]),
            BitEndOffset = Convert.ToInt32(split[(int)symColumn.BitEndOffset]),
            BitSize = Convert.ToInt32(split[(int)symColumn.BitSize]),
            Address = split[(int)symColumn.Address],
            VarKind = split[(int)symColumn.VarKind],
            SymbolDataType = varType,
            Comment = split[(int)symColumn.Comment],
            IsGlobal = split[(int)symColumn.VarKind] is "VAR_EXTERNAL" or "VAR_GLOBAL",
            SymbolArray = varType == SymbolDataType.ARRAY ? SymbolArrayUtil.CreateSymbolArray(type) : null,
            UserDataName = varType == SymbolDataType.USERDATA ? type : "",
        };


        if (varType == SymbolDataType.FB_INST)
        {
            var fbInsKey = SymbolUtil.GetUserOrFBDataKey(taskName, name);
            if(!FBDataSymbols.ContainsKey(fbInsKey))
                FBDataSymbols.Add(fbInsKey, sym);
        }

        return sym;
    }

  
}
