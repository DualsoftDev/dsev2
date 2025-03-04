namespace PLC.CodeGen.Common

open Dual.Common.Core.FS
open Engine.Core

[<AutoOpen>]

module CollectStoragesModule =

    type TimerStatement with

        member x.CollectStorages() : IStorage list =
            [ yield x.Timer.TimerStruct
              let conditions = [ x.RungInCondition; x.ResetCondition ] |> List.choose id

              for cond in conditions do
                  yield! cond.CollectStorages() ]

    type CounterStatement with

        member x.CollectStorages() : IStorage list =
            [ yield x.Counter.CounterStruct
              let conditions =
                  [ x.UpCondition; x.DownCondition; x.ResetCondition; x.LoadCondition ]
                  |> List.choose id

              for cond in conditions do
                  yield! cond.CollectStorages() ]

    type ActionStatement with

        member x.CollectStorages() : IStorage list =
            [   match x with
                //| DuCopy(cond, src, tgt) ->
                //    yield! cond.CollectStorages()
                //    yield! src.CollectStorages()
                //    yield tgt
                | DuCopyUdt { Condition=cond } ->
                    yield! cond.CollectStorages() ]

    type Statement with

        member x.CollectStorages() : IStorage list =
            [
                match x with
                | DuAssign(condition, exp, tgt) ->
                    match condition with
                    | Some condition ->
                        yield! condition.CollectStorages()
                    | None -> ()
                    yield! exp.CollectStorages()
                    yield tgt

                /// 변수 선언.  PLC rung 생성시에는 관여되지 않는다.
                | DuVarDecl(exp, var) ->
                    yield! exp.CollectStorages()
                    yield var

                | DuTimer stmt -> yield! stmt.CollectStorages()
                | DuCounter stmt -> yield! stmt.CollectStorages()
                | DuAction stmt -> yield! stmt.CollectStorages()

                | DuPLCFunction _functionParameters -> failwithlog "ERROR"
                | DuLambdaDecl _ -> ()
                | (DuUdtDecl _ | DuUdtDef _) -> failwith "Unsupported.  Should not be called for these statements"
                | (DuLambdaDecl _ | DuProcDecl _ | DuProcCall _) ->
                    failwith "ERROR: Not yet implemented"       // 추후 subroutine 사용시, 필요에 따라 세부 구현
            ]
    type CommentedStatement with

        member x.CollectStorages() : IStorage list = x.Statement.CollectStorages()
