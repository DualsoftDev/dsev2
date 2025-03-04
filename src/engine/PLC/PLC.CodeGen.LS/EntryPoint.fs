namespace PLC.CodeGen.LS

open Engine.Core
open PLC.CodeGen.Common.FlatExpressionModule2
open PLC.CodeGen.Common.FlatExpressionModule3


module ModuleInitializer =
    let Initialize() =
        printfn "PLC.CodeGen.LS Module is being initialized..."

        fwdCreateSymbolInfo <- XGITag.createSymbolInfo
        fakeAlwaysOnFlatExpression  <- fakeAlwaysOnExpression |> flattenExpression
        fakeAlwaysOffFlatExpression <- fakeAlwaysOffExpression |> flattenExpression