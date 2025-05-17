namespace Dual.Ev2

open Dual.Common.Core.FS
open Dual.Common.Base

[<AutoOpen>]
module CounterStatementModule =

    let generateCounterStatement (cs, cParams:CounterCreateParams) =
        let counter = new Counter   (cParams.Type, cs, cParams.DsRuntimeEnvironment)

        let statements = StatementContainer()
        cParams.CountUpCondition
        |> iter (fun up ->
            let statement = DuAssign (None, up, cs.CU)
            statement.Do()
            statements.Add statement)

        cParams.CountDownCondition
        |> iter (fun down ->
            let statement = DuAssign (None, down, cs.CD)
            statement.Do()
            statements.Add statement)

        if not <| isItNull cs.RES then
            cParams.ResetCondition
            |> iter(fun reset ->
                let statement = DuAssign (None, reset, cs.RES)
                statement.Do()
                statements.Add statement)

        cParams.LoadCondition
        |> iter(fun load ->
            if not (isInUnitTest()) then
                // unit test 에 한해, reset condition 허용
                failwith <| "Load condition is not supported for XGK compatibility"

            let statement = DuAssign (None, load, cs.LD)
            statement.Do()
            statements.Add statement)

        counter.InputEvaluateStatements <- statements |> Seq.cast<IStatement> |> List.ofSeq
        let counterStatement:CounterStatement =
            {   Counter=counter; FunctionName=cParams.FunctionName;
                UpCondition=cParams.CountUpCondition; DownCondition=cParams.CountDownCondition;
                ResetCondition=cParams.ResetCondition; LoadCondition=cParams.LoadCondition;  }
        DuCounter counterStatement

    let (*private*) createCounterStatement (storages:Storages) (cParams:CounterCreateParams): Statement =
        let accum = 0u
        let cs =    // counter structure
            match cParams.Type with
            | CTU  -> CTUStruct.Create  cParams storages accum :> CounterBaseStruct
            | CTD  -> CTDStruct.Create  cParams storages accum
            | CTUD -> CTUDStruct.Create cParams storages accum
            | CTR  -> CTRStruct.Create  cParams storages accum

        generateCounterStatement (cs, cParams)

    let defaultCounterCreateParam = {
        DsRuntimeEnvironment = getNull<DsRuntimeEnvironment>()
        Type              = CTU
        Name              = ""
        Preset            = 0u
        CountUpCondition  = None
        CountDownCondition= None
        ResetCondition    = None
        LoadCondition     = None
        FunctionName      = ""
    }

    let private createCTRStatement (cs :CTRStruct, rungInCondition, dsRte:DsRuntimeEnvironment)  : Statement =
        let cParams = {
            defaultCounterCreateParam with
                DsRuntimeEnvironment = dsRte
                Type = cs.Type
                Name = cs.Name
                Preset= cs.PRE.TValue
                CountUpCondition =  rungInCondition
                FunctionName = "createWinCTR" }

        generateCounterStatement (cs, cParams)


    type CounterStatement =
        static member CreateAbCTU(tcParams:TCConstructionParams) =
            let { DsRuntimeEnvironment=dsRte; Storages=storages; Name=name; Preset=preset; RungInCondition=rungInCondition; FunctionName=functionName} = tcParams

            ({ defaultCounterCreateParam with
                DsRuntimeEnvironment = dsRte
                Type=CTU; Name=name; Preset=preset; FunctionName=functionName
                CountUpCondition=Some rungInCondition; } :CounterCreateParams)
            |> createCounterStatement storages

        static member CreateAbCTD(tcParams:TCConstructionParams) =
            let { DsRuntimeEnvironment=dsRte; Storages=storages; Name=name; Preset=preset; RungInCondition=rungInCondition; FunctionName=functionName} = tcParams
            { defaultCounterCreateParam with
                DsRuntimeEnvironment = dsRte
                Type=CTD; Name=name; Preset=preset; FunctionName=functionName
                CountDownCondition=Some rungInCondition; }
            |> createCounterStatement storages

        static member CreateAbCTUD(tcParams:TCConstructionParams, countDownCondition, reset) =
            let { DsRuntimeEnvironment=dsRte; Storages=storages; Name=name; Preset=preset; RungInCondition=countUpCondition; FunctionName=functionName} = tcParams
            { defaultCounterCreateParam with
                DsRuntimeEnvironment = dsRte
                Type=CTUD; Name=name; Preset=preset; FunctionName=functionName
                CountUpCondition   = Some countUpCondition;
                CountDownCondition = Some countDownCondition;
                ResetCondition     = Some reset;  }
            |> createCounterStatement storages

        // ldCondition (load) 는 XGK 에서는 사용할 수 없음.
        static member CreateCTUD(tcParams:TCConstructionParams, countDownCondition:IExpression<bool>, reset:IExpression<bool>, ldCondition:IExpression<bool> option) =
            let { DsRuntimeEnvironment=dsRte; Storages=storages; Name=name; Preset=preset; RungInCondition=countUpCondition; FunctionName=functionName} = tcParams
            { defaultCounterCreateParam with
                DsRuntimeEnvironment = dsRte
                Type=CTUD; Name=name; Preset=preset; FunctionName=functionName
                CountUpCondition   = Some countUpCondition
                CountDownCondition = Some countDownCondition
                LoadCondition      = ldCondition
                ResetCondition     = Some reset  }
            |> createCounterStatement storages

        static member CreateCTRUsingStructure(cs: CTRStruct, rungInCondition, dsRte:DsRuntimeEnvironment) =
            createCTRStatement (cs, rungInCondition, dsRte)


        static member CreateCTU(tcParams:TCConstructionParams, reset) =
            let { DsRuntimeEnvironment=dsRte; Storages=storages; Name=name; Preset=preset; RungInCondition=rungInCondition; FunctionName=functionName} = tcParams
            { defaultCounterCreateParam with
                DsRuntimeEnvironment = dsRte
                Type=CTU; Name=name; Preset=preset; FunctionName=functionName
                CountUpCondition = Some rungInCondition;
                ResetCondition   = Some reset; }
            |> createCounterStatement storages

        static member CreateXgiCTD(tcParams:TCConstructionParams, load) =
            let { DsRuntimeEnvironment=dsRte; Storages=storages; Name=name; Preset=preset; RungInCondition=rungInCondition; FunctionName=functionName} = tcParams
            { defaultCounterCreateParam with
                DsRuntimeEnvironment = dsRte
                Type=CTD; Name=name; Preset=preset; FunctionName=functionName
                CountDownCondition = Some rungInCondition;
                LoadCondition     = Some load; }
            |> createCounterStatement storages

        static member CreateXgiCTR(tcParams:TCConstructionParams, reset) =
            let { DsRuntimeEnvironment=dsRte; Storages=storages; Name=name; Preset=preset; RungInCondition=rungInCondition; FunctionName=functionName} = tcParams
            { defaultCounterCreateParam with
                DsRuntimeEnvironment = dsRte
                Type=CTR; Name=name; Preset=preset; FunctionName=functionName
                CountDownCondition = Some rungInCondition;
                ResetCondition   = Some reset; }
            |> createCounterStatement storages

