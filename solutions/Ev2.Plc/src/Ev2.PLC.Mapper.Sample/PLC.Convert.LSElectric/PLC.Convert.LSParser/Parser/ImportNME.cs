using PLC.Convert.LSCore;
using PLC.Convert.LSCore.Expression;
using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms.VisualStyles;
using static ImportHelper;

/// <summary>
/// The ImportNME class provides functionality to import and process NME files, extracting rung information and symbols.
/// </summary>
public static class ImportNME
{
    public const string XGRUNGSTART = "[Rung:;[Line:";
	/// <summary>
	/// Retrieves a specific line item from a collection of lines based on a specified kind.
	/// </summary>
	/// <param name="lines">The collection of lines to search through.</param>
	/// <param name="kind">The kind of line item to retrieve.</param>
	/// <returns>The line item as a string.</returns>
	private static string getLineItem(IEnumerable<string> lines, string kind)
    {
        return lines.First(f => f.StartsWith($"[{kind}:")).Split(':')[1].TrimEnd(']').Trim();
    }

	/// <summary>
	/// Asynchronously loads rungs from NME files.
	/// </summary>
	/// <param name="files">An array of file paths to load.</param>
	/// <param name="symbols">A dictionary of symbols to be used in the processing of rungs.</param>
	/// <returns>A list of Rung objects representing the rungs in the NME files.</returns>
	/// <exception cref="Exception">Thrown when duplicate symbols are detected.</exception>
	internal static async Task<List<Rung>> LoadAsync(string[] files, Dictionary<string, Symbol> symbols)
    {
        List<Rung> rungs = new();
        foreach (string file in files)
        {
            IEnumerable<string> lines = ImportFile.ReadFileByLine(file);
            string programKind = getLineItem(lines, "ProgramKind");
            string configName = getLineItem(lines, "ConfigName");
            string mainProgram = getLineItem(lines, "MainProgram");
            string variableFile = getLineItem(lines, "VariableFile");
            string cpuKind = getLineItem(lines, "CPUKind");
            

            List<IEnumerable<string>> groupedLines = lines.GroupByDelimiter(XGRUNGSTART.Split(';').ToList()).ToList();

            StringBuilder stringBuilder = new();
            foreach (IEnumerable<string> rungLine in groupedLines)
            {
                if (rungLine.First().StartsWith("[ProgramKind:"))
                    continue;

                try
                {
                    Rung rung = new(rungLine, mainProgram, configName, cpuKind);
                    if (rung.RungExprs.Any())
                        rungs.Add(rung);
                }

                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            }
        }
        var exprs = rungs.SelectMany(r => r.RungExprs);
        var contacts = exprs.Cast<Expr>().Concat(exprs.OfType<TerminalDummy>())
                                         .SelectMany(s => s.GetTerminals())
                                         .Where(w => !w.IsOutputType)
                                         .ToList();

        var dicExprs = exprs.OfType<Terminal>()
                            .GroupBy(s => s.Symbol.KeyName)
                            .ToDictionary(g => g.Key, g => g.ToList());

        int totalCnt = contacts.Count;
        int currCnt = 0;
        foreach (var contact in contacts)
        {
            ImportEvent.Notify.OnNext($"ImportNME {currCnt++}/{totalCnt}");
            if (currCnt % 256 == 0) await Task.Delay(1);
            if (dicExprs.TryGetValue(contact.Symbol.KeyName, out var coilInners))
            {
                var innerExprs = coilInners.Where(w => w != contact).Where(w => w.IsOutputType);
                if (innerExprs.Count() > 1)
                {
                    contact.InnerExpr =
                        innerExprs.Cast<Expr>()
                                  .Aggregate((left, right) => new Or(left, right));
                }
                else if (innerExprs.Count() == 1 && innerExprs.First() != contact) //FB INOUT 타입 제외
                {
                    contact.InnerExpr = coilInners[0];
                }
            }
        }


        HashSet<string> contactSymbols = contacts.Select(s => s.Symbol.KeyName).Distinct().ToHashSet();
        var onlyContacts = symbols.Values.Where(w => contactSymbols.Contains(w.KeyName)).Distinct();
        var errors = onlyContacts.GroupBy(g => g.KeyName).Where(s => s.Count() > 1);
        if (errors.Any())
            throw new Exception($"Duplicate symbols exist.");

        ImportSymbol.OnlyContacts = onlyContacts.ToDictionary(s => s.KeyName, s => s);

        return rungs;
    }
}
