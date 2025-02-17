open Microsoft.CodeAnalysis.CSharp.Scripting
open Microsoft.CodeAnalysis.Scripting

open Dual.Common.Core.FS

type ScriptEngine((*sources:string seq, options:ScriptOptions*)) =
    static member AsyncRun(code: string, options:ScriptOptions) = CSharpScript.EvaluateAsync<int>(code, options) |> Async.AwaitTask

    static member AsyncRun(code: string, ?dlls:string seq) =
        let dlls = dlls |? ScriptEngine.DefaultDlls
        let options = ScriptOptions.Default.WithReferences(dlls)
        ScriptEngine.AsyncRun(code, options)


    static member Run(code: string, options:ScriptOptions) = CSharpScript.EvaluateAsync<int>(code, options).Result

    static member Run(code: string, ?dlls:string seq, ?imports:string seq) =
        let dlls = dlls |? ScriptEngine.DefaultDlls
        let imports = imports |? ScriptEngine.DefaultImports
        let options =
            ScriptOptions.Default
                .WithReferences(dlls)
                .WithImports(imports)
        ScriptEngine.Run(code, options)

    static member DefaultDlls = ["System.Linq.dll"; "System.Collection.Generic.dll"; "Ev2.Core.FS.dll"]
    static member DefaultImports = ["System"; "System.Linq"; "System.Collections.Generic"; "Dual.Ev2"]


[<AutoOpen>]
module ScriptModule =
    let codeAddLists = """
        //using System;
        //using System.Linq;
        //using System.Collections.Generic;

        List<int> numbers = new List<int> { 1, 2, 3, 4, 5 };
        return numbers.Sum();
    """
    let codeSimpleAdd = "int x = 10; int y = 20; return x + y;"


[<EntryPoint>]
let main _ =
    ScriptEngine.Run(codeSimpleAdd) |> printfn "결과: %d"
    ScriptEngine.Run(codeAddLists) |> printfn "결과: %d"
    0
