namespace rec Dual.Script


open System
open System.IO
open Microsoft.CodeAnalysis
open Microsoft.CodeAnalysis.CSharp
open System.Reflection

open Microsoft.CodeAnalysis.CSharp.Scripting
open Microsoft.CodeAnalysis.Scripting

open Dual.Common.Core.FS
open Dual.Ev2

[<AutoOpen>]
module ScriptModule =
    type private Compiler =
        static member Compile(code:string, ?dllPath:string, ?compilation:CSharpCompilation): Assembly =
            let dllPath = dllPath |? "DynamicLibrary.dll"
            let dllName = Path.GetFileNameWithoutExtension(dllPath)
            // C# 코드 파싱
            let syntaxTree = CSharpSyntaxTree.ParseText(code)
            // 컴파일러 옵션 설정
            let compilation = compilation |?? (fun () ->
                    // 어셈블리 참조 추가
                    let references: MetadataReference[] = [|
                        MetadataReference.CreateFromFile(typeof<obj>.Assembly.Location)
                        MetadataReference.CreateFromFile(typeof<ICalculator>.Assembly.Location)
                    |]
                    CSharpCompilation.Create(dllName)
                        .AddReferences(references)
            )

            let compilation =
                compilation
                    .WithOptions(CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                    .AddSyntaxTrees(syntaxTree)

            // DLL 파일 저장
            let dllPath = $"{dllName}.dll"
            use stream = File.Create(dllPath)
            let emitResult = compilation.Emit(stream)
            stream.Close()
            if emitResult.Success then
                printfn "✅ DLL 생성 완료: %s" dllPath
                Assembly.LoadFile(Path.GetFullPath(dllPath))
            else
                printfn "❌ 컴파일 실패!"
                null

    type ScriptEngine(code:string, ?options:ScriptOptions) =
        member val Assembly:Assembly = Compiler.Compile(code)
        member val Options:ScriptOptions = options |? ScriptOptions.Default

        member x.RunMethod(typeName:string, methodName:string) =
            let typ = x.Assembly.GetType(typeName)
            let method = typ.GetMethod(methodName)
            method.Invoke(null, [||])
        member x.CreateInstance<'T>(typeName:string) =
            let typ = x.Assembly.GetType(typeName)
            Activator.CreateInstance(typ) :?> 'T

    // static methods
    type ScriptEngine with
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
                    .WithReferences(dlls)       // 기존 override.  기존 것에 추가하려면 AddReferences 를 사용
                    .WithImports(imports)       // 기존 override.  기존 것에 추가하려면 AddImports 를 사용
            ScriptEngine.Run(code, options)

        static member DefaultDlls = ["System.Linq.dll"; "System.Collection.Generic.dll"; "Ev2.Core.FS.dll"]
        static member DefaultImports = ["System"; "System.Linq"; "System.Collections.Generic"; "Dual.Ev2"]

        (*
         typeof<obj>.Assembly.Location : System.Private.CoreLib.dll
         typeof<ScriptEngine>.Assembly.Location : C:\Users\user\source\repos\ScriptingTest\ScriptingTest\bin\Debug\net5.0\ScriptingTest.dll
         typeof<System.Linq.Enumerable>.Assembly.Location : System.Linq.dll or System.Core.dll
        *)


[<AutoOpen>]
module ScriptTestModule =
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
        Console.OutputEncoding <- System.Text.Encoding.UTF8
        ScriptEngine.Run(codeSimpleAdd) |> printfn "AddSimple 결과: %d"
        ScriptEngine.Run(codeAddLists) |> printfn "AddLists 결과: %d"

        let se =
            let code = File.ReadAllText("Z:\dsev2\TestApps\Scripting\ScriptingTest\ScriptSource.cs")
            ScriptEngine(code)

        se.RunMethod("DynamicClass", "Hello") |> printfn "DynamicClass.Hello 결과: %A"

        let calc = se.CreateInstance<ICalculator>("Calculator")
        printfn $"🔢 Add(10, 5) = {calc.Add(10, 5)}"
        printfn $"🔢 Multiply(4, 3) = {calc.Multiply(4, 3)}"

        0

