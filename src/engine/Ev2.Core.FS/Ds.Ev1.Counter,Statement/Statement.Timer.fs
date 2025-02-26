namespace Dual.Ev2

open Dual.Common.Core.FS
open Dual.Common.Base.FS

[<AutoOpen>]
module TimerStatementModule =
    type internal TimerCreateParams = {
        Type: TimerType
        Name: string
        Preset: CountUnitType
        RungConditionIn: IExpression<bool> option
        ResetCondition: IExpression<bool> option
        FunctionName:string
    }


    let private generateTimerStatement (ts :TimerStruct, tParams:TimerCreateParams) =

        if ts.PRE.TValue < MinTickInterval then
            failwith <| $"Timer Resolution Error: Preset value should be larger than %A{MinTickInterval}"

        let timer = new Timer(ts.Type, ts)
        let statements = StatementContainer()
        tParams.RungConditionIn
        |> iter(fun cond ->
            let rungInStatement = DuAssign (None, cond, ts.EN)
            rungInStatement.Do()
            statements.Add rungInStatement)

        tParams.ResetCondition
        |> iter(fun cond ->
            if not (isInUnitTest()) then
                // unit test 에 한해, reset condition 허용
                failwith <| "Reset condition is not supported for XGK compatibility"

            (*
             * XGK 에서도 reset condition 을 사용할 수 있도록 하려면 아래 코드 사용하고,
             * PLC 생성 부분에서 reset condition rising 시에 Timer reset 하도록 작성해야 함.
             * 현재 구현은 상위 로직에서 reset condition 을 사용하지 않도록 하고 있음.
             *)

            let resetStatement = DuAssign (None, cond, ts.RES)
            resetStatement.Do()
            statements.Add resetStatement)

        timer.InputEvaluateStatements <- statements |> Seq.cast<IStatement> |> List.ofSeq
        DuTimer ({ Timer=timer; RungInCondition = tParams.RungConditionIn; ResetCondition = tParams.ResetCondition; FunctionName=tParams.FunctionName }:TimerStatement)


    let private createTONStatement (ts :TimerStruct, rungInCondition, resetCondition)  : Statement =

        let tParams ={ Type=ts.Type; Name=ts.Name; Preset=ts.PRE.TValue;
                       RungConditionIn=rungInCondition; ResetCondition=resetCondition; FunctionName="createXgiTON"}     // createWinTON

        generateTimerStatement (ts, tParams)

    let private createTimerStatement (storages:Storages) (target:PlatformTarget)  (tParams:TimerCreateParams)  : Statement =
        let ts = TimerStruct.Create(tParams.Type, storages, tParams.Name, tParams.Preset, 0u, RuntimeDS.System.Value, target)
        generateTimerStatement (ts, tParams)


    /// Timer & Counter construction parameters
    type TCConstructionParams = {
        Storages:Storages
        /// timer/counter structure name
        Name: string
        Preset: CountUnitType
        RungInCondition:IExpression<bool>
        /// e.g 'createXgiCTU'
        FunctionName:string
    }

    type TimerStatement =
        static member CreateTON(tcParams:TCConstructionParams) (target:PlatformTarget)=
            let {Storages=storages; Name=name; Preset=preset; RungInCondition=rungInCondition; FunctionName=functionName} = tcParams
            ({  Type=TON; Name=name; Preset=preset;
                RungConditionIn=Some rungInCondition;
                ResetCondition=None; FunctionName=functionName
             } :TimerCreateParams
            )|> createTimerStatement storages target

        static member CreateTONUsingStructure(ts: TimerStruct, rungInCondition, resetCondition) =
            createTONStatement (ts, rungInCondition, resetCondition)

        static member CreateTOF(tcParams:TCConstructionParams) (target:PlatformTarget)=
            let {Storages=storages; Name=name; Preset=preset; RungInCondition=rungInCondition; FunctionName=functionName} = tcParams
            ({  Type=TOF; Name=name; Preset=preset;
                RungConditionIn=Some rungInCondition;
                ResetCondition=None; FunctionName=functionName
             }: TimerCreateParams)
            |> createTimerStatement storages target

        static member CreateAbRTO(tcParams:TCConstructionParams) (target:PlatformTarget)=
            let {Storages=storages; Name=name; Preset=preset; RungInCondition=rungInCondition; FunctionName=functionName} = tcParams
            ({  Type=TMR; Name=name; Preset=preset;
                RungConditionIn=Some rungInCondition;
                ResetCondition=None; FunctionName=functionName
             }:TimerCreateParams)
            |> createTimerStatement storages target

        //static member CreateTON(tcParams:TCConstructionParams, resetCondition) =
        //    let {Storages=storages; Name=name; Preset=preset; RungInCondition=rungInCondition} = tcParams
        //    {   Type=TON; Name=name; Preset=preset;
        //        RungConditionIn=Some rungInCondition;
        //        ResetCondition=Some resetCondition; }
        //    |> createTimerStatement storages

        //static member CreateTOF(tcParams:TCConstructionParams, resetCondition) =
        //    let {Storages=storages; Name=name; Preset=preset; RungInCondition=rungInCondition} = tcParams
        //    {   Type=TOF; Name=name; Preset=preset;
        //        RungConditionIn=Some rungInCondition;
        //        ResetCondition=Some resetCondition; }
        //    |> createTimerStatement storages

        static member CreateTMR(tcParams:TCConstructionParams, resetCondition) (target:PlatformTarget)=
            let {Storages=storages; Name=name; Preset=preset; RungInCondition=rungInCondition; FunctionName=functionName} = tcParams
            ({  Type=TMR; Name=name; Preset=preset;
                RungConditionIn=Some rungInCondition;
                ResetCondition=Some resetCondition; FunctionName=functionName
             }: TimerCreateParams)
            |> createTimerStatement storages target



