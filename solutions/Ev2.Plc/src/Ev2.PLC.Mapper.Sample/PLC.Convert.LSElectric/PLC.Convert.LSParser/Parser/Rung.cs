using PLC.Convert.LSCore;
using PLC.Convert.LSCore.Expression;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Linq;
using System.Security.Policy;
using System.Xml.Linq;

/// <summary>
/// Represents a rung in a ladder logic program.
/// </summary>
public class Rung
{
    // Properties to store expressions, terminals, program information, and call parameters.
    public List<Expr> RungExprs { get; private set; }
    public IEnumerable<Terminal> Terminals
    {
        get {
            var outs = RungExprs.OfType<Terminal>();
            var ins = outs.SelectMany(s => s.GetTerminals());

            return outs.Concat(ins).Distinct();
        }
    }
    public string CPUKind { get; set; }
    /// <summary>
    /// Task POU 이름
    /// </summary>
    public string MainProgram { get; set; }
    /// <summary>
    /// 하나의 XG5000에서 멀티 프로젝트중 찾는 기준
    /// </summary>
    public string ConfigName { get; set; }
    public int RungNum { get; set; }
    public int LineNum { get; set; }

    public List<CallPara> CallParas { get; set; }
    public IEnumerable<string> RungLines { get; set; }

    /// <summary>
    /// Converts the rung's expressions and lines into text.
    /// </summary>
    public string ToText()
    {
        return string.Join("\n", RungExprs.Select(s => s.ToText())) + "\n"
                            + string.Join("\n", RungLines) + "\n\n";
    }


    /// <summary>
    /// Constructor that initializes a Rung instance from a sequence of rung lines.
    /// </summary>
    public Rung(IEnumerable<string> rungLines, string mainProgram, string configName, string cpuKind)
    {
        if (!rungLines.Any()) throw new Exception($"configName {configName}, mainProgram {mainProgram} has no lines");
        Tuple<int, int> rungNline = ImportHelper.GetRungNLineNumber(rungLines);
        MainProgram = mainProgram;
        ConfigName = configName;
        CPUKind = cpuKind;
        RungNum = rungNline.Item1;
        LineNum = rungNline.Item2;

        RungLines = rungLines;

        CallParas = ImportHelper.GetCallParas(rungLines, mainProgram, LineNum, configName).ToList();

        if (CallParas.Any())
            RungExprs = ConvertToExpression(rungLines.Where(w => w != ""));
        else
            RungExprs = Enumerable.Empty<Expr>().ToList();
    }


    public Rung(string coil, IEnumerable<string> contactAs, IEnumerable<string> contactBs, Dictionary<string, Terminal> dictRung)
    {
        RungLines = contactAs.Concat(contactBs).Append(coil);
        Terminal getTerminal(string name)
        {
            if (dictRung.ContainsKey(name) && dictRung[name].HasInnerExpr)
            {
                var a = dictRung[name];

            }

            return dictRung.ContainsKey(name) ?
                dictRung[name] 
                : new Terminal(new Symbol(name));
        }       

        var coilExpr = getTerminal(coil);
        Expr baseExpr = new TerminalDummy("");
        if (contactAs.Count() > 0)
        {
            baseExpr = getTerminal (contactAs.First());
            contactAs = contactAs.Skip(1);
        }
        else
        {
            baseExpr = getTerminal (contactBs.First());
            contactBs = contactBs.Skip(1);
        }

        foreach (var item in contactAs)
        {
            var contact = getTerminal(item); 
            if (coilExpr.InnerExpr == null)
                coilExpr.InnerExpr = new And(baseExpr, contact);
            else
                coilExpr.InnerExpr = new And(coilExpr.InnerExpr, contact);

            coilExpr.Type = TerminalType.CONTACT;                      
        }

        foreach (var item in contactBs)
        {
            var contact = getTerminal(item); 
            if (coilExpr.InnerExpr == null)
                coilExpr.InnerExpr = new And(baseExpr, new Not(contact));
            else
                coilExpr.InnerExpr = new And(baseExpr, new Not(contact));

            coilExpr.Type = TerminalType.CONTACT;
        }

        RungExprs = [coilExpr];
    }


    private bool IsAbleTag(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;
        if (Char.IsDigit(name.First()))
            return false; 
        //if (xgk && name.StartsWith("_"))
        //    return false;
        if (name.StartsWith("-") || name.StartsWith("TIME#"))
            return false;
        if (name.StartsWith("^"))
            return false; //^LINEIN ^EMPTY

        if (!CallParaUtil.IsXGIRuntime && name.StartsWith("h"))
            return false;
        if (name.Contains("#")) //t#
            return false; 
        if (name.StartsWith("'") && name.EndsWith("'")) //string type 'AAA'
            return false;

        return true;
    }


    private void DoFunc(string line, List<Expr> exprOuts, Stack<Expr> exprStack)
    {
        string[] tokens = line.Split('\t');
        string fbName = tokens[0];
        var fbType = ImportFB.GetFBType(fbName);
        bool styleFB = fbType == ImportFB.FB_Type.FUNCTION_BLOCK;

        if (ImportFB.IsFuncOrFB(fbName))
        {
            if ("INC" == fbName) { }
            List<ImportFB.FB_INOUT> inouts = ImportFB.GetInOutInfo(fbName, !styleFB);
            if (inouts.Count == 0)
            {//출력 전용 CMD // EX) OUTOFF,..
                HandleOutputOperations(OpType.OUT_FB, fbName, exprStack, exprOuts);
                return;
            }
             

            char charSplit = CallParaUtil.IsXGIRuntime? ',' : ' ';
            string[] args = ImportHelper.SplitIgnoringBrackets(tokens.Last(), charSplit);
            string[] fbArgs = !styleFB ? args : args.SkipWhile(w => w == "^EMPTY").ToArray();


            if (inouts.Count != fbArgs.Length)
            {
                throw new Exception($"fbArgs error {inouts.Count} <> {fbArgs.Length}  : {line}");
            }

            for (int i = 0; i < fbArgs.Length; i++)
            {
                if (!IsAbleTag(fbArgs[i]))
                    continue;

                switch (inouts[i])
                {
                    case ImportFB.FB_INOUT.VAR_IN:
                        if (fbName.StartsWith("AND"))
                        {
                            HandleAndOrOperations(OpType.AND_FB, fbArgs[i], exprStack);
                        }
                        else
                        {
                            if(exprStack.Any() && !fbName.StartsWith("LOAD"))
                                HandleAndOrOperations(OpType.OR_FB, fbArgs[i], exprStack);
                            else 
                                HandleLoadOperations(OpType.LOAD_FB, fbArgs[i], exprStack);
                        }
                        break;

                    case ImportFB.FB_INOUT.VAR_OUT:
                        HandleOutputOperations(OpType.OUT_FB, fbArgs[i], exprStack, exprOuts);
                        break;

                    default:
                        throw new InvalidOperationException($"Unknown IECFunc.FB_INOUT type : {inouts[i]}");
                }
            }
        }
        else
        {
            throw new InvalidOperationException($"Unknown Func: {line}");
        }
    }
    bool _stLine = false;
    private List<Expr> ConvertToExpression(IEnumerable<string> rungLines)
    {
        List<Expr> exprOuts = new();
        Stack<Expr> exprStack = new();
        Stack<Expr> memoryStack = new();
        _stLine = false;
        foreach (string line in rungLines)
        {
            if(line.StartsWith("[Line:")) _stLine = true;
            if (line.StartsWith("[")) continue;     //[Rung:1, Line:2] , [Variable: (AA: 2, 0);(BB: 2, 31);]

            string[] tokens = line.Split('\t');
            OpType opType = OpType.None;
            if (!Enum.TryParse(tokens[0].Replace(' ', '_'), out opType))
            {
                if(line != "ERR")
                    DoFunc(line, exprOuts, exprStack);

                continue;
            }

            OpType operatorType = (OpType)opType;
            string operand = tokens.Length > 1 ? tokens[1] : string.Empty;

            switch (operatorType)
            {
                case OpType.LOAD:
                case OpType.LOADP:
                case OpType.LOADN:
                case OpType.LOAD_BR:
                case OpType.LOAD_ON:
                case OpType.LOAD_NOT:
                case OpType.LOADN_NOT:
                case OpType.LOADP_NOT:
                case OpType.OR_LOAD:
                case OpType.AND_LOAD:
                    HandleLoadOperations(operatorType, operand, exprStack);
                    break;

                case OpType.AND:
                case OpType.ANDP:
                case OpType.ANDN:
                case OpType.OR:
                case OpType.ORP:
                case OpType.ORN:
                case OpType.ORN_NOT:
                case OpType.ORP_NOT:
                case OpType.AND_NOT:
                case OpType.ANDN_NOT:
                case OpType.ANDP_NOT:

                case OpType.OR_NOT:
                    HandleAndOrOperations(operatorType, operand, exprStack);
                    break;

                case OpType.MPUSH:
                case OpType.MLOAD:
                case OpType.MPOP:
                    HandleMemoryOperations(operatorType, exprStack, memoryStack);
                    break;

                case OpType.SET:
                case OpType.RST:
                case OpType.OUT:
                case OpType.OUTP:
                case OpType.OUTN:
                case OpType.OUT_NOT:
                    HandleOutputOperations(operatorType, operand, exprStack, exprOuts);
                    break;

                case OpType.NOT:
                    Expr currentExpr = exprStack.Pop();
                    exprStack.Push(new Not(currentExpr));
                    break;

                case OpType.FF:
                    //if (tokens.Last().Split(',').Last() == "^LINEOUT")
                    //{
                    //    currentExpr = exprStack.Pop();
                    //    exprStack.Push(new Not(currentExpr));
                    //    break;
                    //}
                    //else
                    var operland = tokens.Last().Split(',').Last();
                    if (operland != "^LINEOUT")
                        HandleOutputOperations(operatorType, operland, exprStack, exprOuts);
                    break;

                // Do nothing for these cases
                case OpType.BREAK:
                case OpType.R_EDGE:
                case OpType.F_EDGE:
                    break;

                case OpType.END:
                case OpType.CMT:
                case OpType.OUTCMT:
                case OpType.FOR:
                case OpType.INIT_DONE:
                case OpType.NEXT:
                case OpType.LABEL:
                case OpType.ST_LABEL:
                case OpType.REM:
                case OpType.UDF_RUNG_BEGIN:
                case OpType.JMP:
                case OpType.ST_JMP:
                case OpType.CALL:
                case OpType.SBRT:
                case OpType.RET:
                    if (operatorType == OpType.CMT) { }
                    if (exprStack.Count > 0)
                    {
                        return new List<Expr>() { new TerminalDummy($"{operatorType}") { InnerExpr = exprStack.Peek() } };
                    }
                    else
                    {
                        return new List<Expr>();  // Empty result
                    }

                default:
                    throw new InvalidOperationException($"Unknown operator: {operatorType}");
            }
        }

        if (exprStack.Count == 0 && exprOuts.Count == 0)
        {
            Debug.WriteLine("missing OUTPUT " + string.Join(", ", rungLines));

            //throw new InvalidOperationException("Incomplete expression, missing OUTPUT operation.");
        }

        return exprOuts;
    }



    private void HandleLoadOperations(OpType operation, string operand, Stack<Expr> exprStack)
    {
        switch (operation)
        {
            case OpType.LOAD_ON:
            case OpType.LOAD:
            case OpType.LOADP:
            case OpType.LOADN:
            case OpType.LOAD_BR:
            case OpType.LOADN_NOT:
            case OpType.LOADP_NOT:
            case OpType.LOAD_NOT:
            case OpType.LOAD_FB:

                Expr baseExpr = new TerminalDummy("");
                if(operand != "" )
                {
                    var callPara = CallParaUtil.GetCallPara(CallParas, operand, operation, _stLine);
                    baseExpr = new Terminal(ImportSymbol.GetSymbol(callPara), callPara);
                }

                if (operation is OpType.LOAD_NOT or OpType.LOADN_NOT or OpType.LOADP_NOT)
                {
                    baseExpr = new Not(baseExpr);
                }
                exprStack.Push(baseExpr);
                break;

            case OpType.AND_LOAD:
            case OpType.OR_LOAD:
                if (exprStack.Count < 2)
                {
                    throw new InvalidOperationException("Not enough operands in stack for operation.");
                }

                Expr rightExpr = exprStack.Pop();
                Expr leftExpr = exprStack.Pop();

                Expr combinedExpr = operation == OpType.AND_LOAD ? new And(leftExpr, rightExpr) : new Or(leftExpr, rightExpr);
                exprStack.Push(combinedExpr);
                break;

            default:
                throw new InvalidOperationException($"Unknown load operation: {operation}");
        }
    }

    private void HandleAndOrOperations(OpType operation, string operand, Stack<Expr> exprStack)
    {
        if (exprStack.Count < 1)
        {
            throw new InvalidOperationException("Not enough operands in stack for operation.");
        }
        Expr previousExpr = exprStack.Pop();
        Expr applyNotIfExistNot(Expr t) 
        { 
            return
               operation is OpType.AND_NOT
               or OpType.ANDP_NOT
               or OpType.ANDN_NOT
               or OpType.OR_NOT
               or OpType.ORP_NOT
               or OpType.ORN_NOT
              ? new Not(t) : t;
        }

        if ((CPUKind == "XGI" && operand == "_OFF") || operand.StartsWith("@"))  //임시 자동 생성된거 처리
        {
            TerminalDummy terminalDummy = new(operand);
            Expr applyNot = applyNotIfExistNot(terminalDummy);
            Expr currentExpr = operation is OpType.AND or OpType.ANDP or OpType.ANDN or OpType.AND_NOT or OpType.AND_FB
            ? new And(previousExpr, applyNot) : new Or(previousExpr, applyNot);
            exprStack.Push(currentExpr);
        }
        else
        {
            CallPara callPara = CallParaUtil.GetCallPara(CallParas, operand, operation, _stLine);
            Symbol symbol = ImportSymbol.GetSymbol(callPara);
            Terminal terminal = new(symbol, callPara);
            if (operation is OpType.OR_FB or OpType.AND_FB)
                terminal.Type = TerminalType.FB_IN;

            Expr currentExpr = applyNotIfExistNot(terminal);

             currentExpr = operation is OpType.AND or OpType.ANDP or OpType.ANDN or OpType.AND_NOT or OpType.AND_FB
                ? new And(previousExpr, currentExpr) : new Or(previousExpr, currentExpr);
            exprStack.Push(currentExpr);
        }
    }

    private void HandleMemoryOperations(OpType operation, Stack<Expr> exprStack, Stack<Expr> memoryStack)
    {
        switch (operation)
        {
            case OpType.MPUSH:
                if (exprStack.Count > 0)
                {
                    memoryStack.Push(exprStack.Peek());
                }
                else
                {
                    throw new InvalidOperationException("No expression available to push to memory.");
                }
                break;

            case OpType.MLOAD:
                if (memoryStack.Count > 0)
                {
                    exprStack.Push(memoryStack.Peek());
                }
                else
                {
                    throw new InvalidOperationException("No expression available in memory to load.");
                }
                break;

            case OpType.MPOP:
                if (memoryStack.Count > 0)
                {
                    exprStack.Push(memoryStack.Pop());
                }
                else
                {
                    throw new InvalidOperationException("No expression available in memory to pop.");
                }
                break;
        }
    }

    private void HandleOutputOperations(OpType op, string operand, Stack<Expr> exprStack, List<Expr> exprs)
    {

        if (operand.StartsWith("@"))  //@LDTemp1
        {
            TerminalDummy terminal =
                   exprStack.Count > 0 ?
                   new TerminalDummy(operand) { InnerExpr = getInnerExpr(op, exprStack) }
                   : new TerminalDummy(operand);
            
            exprs.Add(terminal);
        }
        else
        {
            CallPara callPara = CallParaUtil.GetCallPara(CallParas, operand, op, _stLine);
            Terminal terminal =
                    exprStack.Count > 0 ?
                    new Terminal(ImportSymbol.GetSymbol(callPara), callPara) { InnerExpr = getInnerExpr(op, exprStack) }
                    : new Terminal(ImportSymbol.GetSymbol(callPara), callPara);

            if (op == OpType.OUT_FB)
                terminal.Type = TerminalType.FB_OUT;
            else
                terminal.Type = TerminalType.COIL;
          
            exprs.Add(terminal);
        }

        Expr getInnerExpr(OpType operation, Stack<Expr> exprStack)
        {
            Expr currentExpr = exprStack.Peek();
            if (operation is OpType.RST or OpType.OUT_NOT or OpType.OUTN or OpType.FF)
            {
                currentExpr = new Not(currentExpr);
            }

            return currentExpr;
        }
    }
}
