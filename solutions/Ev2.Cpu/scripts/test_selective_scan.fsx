#load "src/cpu/Ev2.Cpu.Core/Core/DataTypes.fs"
#load "src/cpu/Ev2.Cpu.Core/Core/Types.fs"
#load "src/cpu/Ev2.Cpu.Core/Struct/DataTypes.fs"
#load "src/cpu/Ev2.Cpu.Core/Struct/Expression.fs"
#load "src/cpu/Ev2.Cpu.Core/Struct/Statement.fs"
#load "src/cpu/Ev2.Cpu.Runtime/Runtime/Memory.fs"
#load "src/cpu/Ev2.Cpu.Runtime/Runtime/Context.fs"
#load "src/cpu/Ev2.Cpu.Runtime/Runtime/BuiltinFunctions.fs"
#load "src/cpu/Ev2.Cpu.Runtime/Runtime/ExprEvaluator.fs"
#load "src/cpu/Ev2.Cpu.Runtime/Runtime/DependencyAnalyzer.fs"
#load "src/cpu/Ev2.Cpu.Runtime/Runtime/StmtEvaluator.fs"
#load "src/cpu/Ev2.Cpu.Runtime/CpuScan.fs"

open System
open Ev2.Cpu.Core
open Ev2.Cpu.Runtime.DependencyAnalyzer
open Ev2.Cpu.Core.Statement

// 간단한 테스트 시나리오
let testSelectiveScan () =
    // 변수 레지스트리 초기화
    Statement.clearVariableRegistry()
    
    // 테스트 프로그램: A := B + C, D := A * 2
    let varB = DsTag.Int "B"
    let varC = DsTag.Int "C" 
    let varA = DsTag.Int "A"
    let varD = DsTag.Int "D"
    
    let stmt1 = varA := (Terminal varB) + (Terminal varC)
    let stmt2 = varD := (Terminal varA) * (Const (2 :> obj, DataType.TInt))
    
    let program = { Body = [stmt1; stmt2] }
    
    // 컨텍스트 생성
    let ctx = Context.create()
    ctx.Memory.DeclareLocal("A", DataType.TInt)
    ctx.Memory.DeclareLocal("B", DataType.TInt)  
    ctx.Memory.DeclareLocal("C", DataType.TInt)
    ctx.Memory.DeclareLocal("D", DataType.TInt)
    
    // 초기값 설정
    ctx.Memory.Set("B", box 10)
    ctx.Memory.Set("C", box 20)
    
    // 선택적 스캔 설정
    let config = { ScanConfig.Default with SelectiveMode = true }
    let engine = CpuScanEngine(program, ctx, Some config, None, None)
    
    printfn "=== 초기 상태 ==="
    printfn "Memory: %s" (ctx.Memory.SnapshotText())
    
    // 첫 번째 스캔 (모든 문장 실행되어야 함)
    printfn "\n=== 첫 번째 스캔 ==="
    let elapsed1 = engine.ScanOnce()
    printfn "Elapsed: %d ms" elapsed1
    printfn "Memory: %s" (ctx.Memory.SnapshotText())
    printfn "A = %A, D = %A" (ctx.Memory.Get("A")) (ctx.Memory.Get("D"))
    
    // B만 변경
    printfn "\n=== B 변경 후 스캔 ==="
    ctx.Memory.Set("B", box 15)
    let elapsed2 = engine.ScanOnce()
    printfn "Elapsed: %d ms" elapsed2  
    printfn "Memory: %s" (ctx.Memory.SnapshotText())
    printfn "A = %A, D = %A" (ctx.Memory.Get("A")) (ctx.Memory.Get("D"))
    
    // 관련 없는 변수 변경
    printfn "\n=== 관련 없는 변수 변경 후 스캔 ==="
    ctx.Memory.Set("UnrelatedVar", box 999)
    let elapsed3 = engine.ScanOnce()
    printfn "Elapsed: %d ms" elapsed3
    printfn "Memory: %s" (ctx.Memory.SnapshotText())
    printfn "A = %A, D = %A (변경되지 않아야 함)" (ctx.Memory.Get("A")) (ctx.Memory.Get("D"))

// 테스트 실행
printfn "선택적 스캔 테스트 시작..."
try
    testSelectiveScan()
    printfn "\n테스트 완료!"
with ex ->
    printfn "\n오류 발생: %s" ex.Message
    printfn "스택 트레이스: %s" ex.StackTrace
