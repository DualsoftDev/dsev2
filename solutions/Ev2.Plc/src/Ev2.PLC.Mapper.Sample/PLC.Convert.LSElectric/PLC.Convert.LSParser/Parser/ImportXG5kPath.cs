using PLC.Convert.LSCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static ImportHelper;

/// <summary>
/// A class providing methods to load and process data from an XG5k project path.
/// </summary>
public static class ImportXG5kPath
{
    /// <summary>
    /// Loads rungs from a given XG5k project path.
    /// </summary>
    /// <param name="pathXG5k">The path to the XG5k project.</param>
    /// <returns>A list of Rung objects representing the loaded rung data.</returns>
    public static async Task<Tuple<List<Rung>, ProgramInfo>> LoadAsync(string pathXG5k)
    {
        string programInfoPath = Path.GetDirectoryName(pathXG5k) + "/AbnormalContact/ProgramInfo.txt";
        ProgramInfo info = new ProgramInfo(programInfoPath);
        if (!info.TASKPaths.Any())
            return Tuple.Create(new List<Rung>(), info);
        else
            CallParaUtil.IsXGIRuntime = ImportFile.ReadFileByLine(info.TASKPaths[0]).First().Contains("XGI");



        // Assuming CreateReaderAsync is an async version of CreateReader
        ImportFB.IECFuncs = ImportFB.CreateReader(info.Dir + "/Standard_Function_List.txt");
        ImportFB.UserFuncs = info.USER_FUN_FBs.Any() ?  ImportFB.CreateReader(info.Dir + "/User_Function_List.txt") : new();

        ImportSymbol.UserDataSymbols =  ImportSymbol.LoadUserDataSymbols(info.UserDataPaths);
        var globalSyms =  ImportSymbol.LoadGlobalSymbols(info.GlobalVariablePaths);
        ImportSymbol.Symbols =  ImportSymbol.LoadAllSymbols(info.LocalSymPaths, globalSyms);

        ImportSymbol.FindAutoDeviceSymbols = ImportSymbol.Symbols.Values
                                                .ToDictionary(s => SymbolUtil.GetAutoDeviceKey(s.TaskName, s.Name), s => s);

        List<Rung> rungs = await ImportNME.LoadAsync(info.TASKPaths, ImportSymbol.Symbols);

        return Tuple.Create(rungs, info);
    }

}
