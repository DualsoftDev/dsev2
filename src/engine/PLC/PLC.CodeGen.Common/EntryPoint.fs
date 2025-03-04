namespace PLC.CodeGen.Common

open Engine.Core

module ModuleInitializer =
    let Initialize () =
        printfn "PLC.CodeGen.Common Module is being initialized..."
        fwdFlattenExpression <- flattenExpression
