namespace PLC.CodeGen.LS

open Engine.Core
open PLC.CodeGen.Common
open Dual.Common.Core.FS
open Dual.Common.Base.FS
open System.Collections.Generic

[<AutoOpen>]
module POUParametersModule =

    type XgxPOUParams =
        {
            /// POU name.  "DsLogic"
            POUName: string
            ///// POU container task name
            //TaskName: string
            /// POU ladder 최상단의 comment
            Comment: string
            LocalStorages: Storages
            /// 참조용 global storages
            GlobalStorages: Storages
            CommentedStatements: CommentedStatement list
        }

    /// (unit -> int) 함수를 반환하는 type
    type counterGeneratorType = Seq.counterGeneratorType

    /// 이미 존재하는 counterGenerator 함수를 사용하되, excludeList 에 있는 값은 제외하고 반환
    let counterGeneratorOverrideWithExclusionList (existingCounterGenerator: counterGeneratorType) (excludes: int seq) : counterGeneratorType =
        let excludes = excludes |> HashSet
        fun () ->
            let mutable n = existingCounterGenerator()
            while excludes.Contains n  do
                n <- existingCounterGenerator()
            n

    /// start 값부터 출력하는 counterGenerator 함수를 반환하되, excludeList 에 있는 값은 제외하고 반환
    let counterGeneratorWithExclusionList start (excludes: int list) : counterGeneratorType =
        counterGeneratorOverrideWithExclusionList (counterGenerator start) excludes


    type XgkTimerResolutionSpec(resolution:float, range:IntRange) =
        member x.Resolution = resolution
        member x.Range = range

    type XgxProjectParamsProperties() =
        member val XgxTimerResolutionSpec:XgkTimerResolutionSpec list = [] with get, set

    type XgxProjectParams = {
        TargetType         : PlatformTarget
        ProjectName        : string
        ProjectComment     : string
        ScanProgramName    : string         // "Scan Program" or "스캔 프로그램".  Damn it!
        GlobalStorages     : Storages
        ExistingLSISprj    : string
        POUs               : XgxPOUParams list
        MemoryAllocatorSpec: PLCMemoryAllocatorSpec
        EnableXmlComment   : bool
        TimerCounterGenerator  : counterGeneratorType
        CounterCounterGenerator: counterGeneratorType
        RungCounter            : counterGeneratorType
        /// Auto 변수의 이름을 uniq 하게 짓기 위한 용도 "_tmp_temp_internal{n}
        AutoVariableCounter    : counterGeneratorType
        AppendDebugInfoToRungComment: bool

        /// Read/Write 가능한 속성들 집합
        Properties: XgxProjectParamsProperties
    }
    and XgxProjectParams with
        /// XGK timer 변수의 resolution 값을 반환.  e.g "T0001" 이면 n 의 값은 1
        member x.GetXgkTimerResolution(n:int) =
            assert (x.TargetType = XGK)
            let contains (range:int*int) needle = fst range <= needle && needle <= snd range
            let rangeSpec =
                x.Properties.XgxTimerResolutionSpec
                |> filter (fun spec -> contains spec.Range n)
                |> exactlyOne
            rangeSpec.Resolution

    let createDefaultProjectParams targetType memorySize =
        let voidCounterGenerator : counterGeneratorType =
            fun () -> failwith "Should be assigned with valid counter generator"

        {
            TargetType = targetType
            ProjectName = ""
            ProjectComment = ""
            ScanProgramName = "Scan Program"
            GlobalStorages = Storages()
            ExistingLSISprj = null
            POUs = []
            MemoryAllocatorSpec = AllocatorFunctions(createMemoryAllocator "M" (0, memorySize) [] targetType)
            EnableXmlComment = false
            AppendDebugInfoToRungComment = IsDebugVersion || isInUnitTest()
            TimerCounterGenerator   = voidCounterGenerator
            CounterCounterGenerator = voidCounterGenerator
            RungCounter             = voidCounterGenerator
            AutoVariableCounter     = voidCounterGenerator
            Properties = XgxProjectParamsProperties()
        }

    let defaultMemorySize = 640 * 1024
    let defaultXGIProjectParams = createDefaultProjectParams XGI defaultMemorySize   // 640K "M" memory 영역
    let defaultXGKProjectParams = createDefaultProjectParams XGK defaultMemorySize

    let getXgxProjectParams (targetType:PlatformTarget) (projectName:string) =
        assert(isInUnitTest())
        let getProjectParams =
            match targetType with
            | XGI -> defaultXGIProjectParams
            | XGK -> defaultXGKProjectParams
            | _ -> failwithf "Invalid target type: %A" targetType
        {   getProjectParams with
                ProjectName = projectName; TargetType = targetType;
                MemoryAllocatorSpec = AllocatorFunctions(createMemoryAllocator "M" (0, defaultMemorySize) [] targetType)
                TimerCounterGenerator   = counterGeneratorWithExclusionList 0 []
                CounterCounterGenerator = counterGeneratorWithExclusionList 0 []
                AutoVariableCounter     = counterGenerator 1
                RungCounter             = counterGenerator 0
        }


    type Statement with
        member x.SanityCheck(prjParam: XgxProjectParams) =
            match x with
            | DuAssign(_, _expr, _target) -> ()
            | DuVarDecl(_expr, _variable) -> ()
            | DuTimer(_t:TimerStatement) -> ()
            | DuCounter(c:CounterStatement) ->
                let ctr = c.Counter
                let up, down = c.UpCondition, c.DownCondition
                let ld, rst = c.LoadCondition, c.ResetCondition
                let cs, typ = ctr.CounterStruct, ctr.Type
                let name = $"counter: {cs.Name}"

                let isUpCounter, isDownCounter =
                    match typ with
                    | CTU -> true, false
                    | CTD | CTR -> false, true
                    | CTUD -> true, true

                match typ with
                | CTUD ->
                    verifyM $"No up/down condition for {name}" (up.IsSome && down.IsSome)
                    verifyM $"No ld/rst condition for {name}" (ld.IsSome && rst.IsSome)
                | CTD ->
                    verifyM $"No load condition for {name}" (down.IsSome && ld.IsSome)
                    verifyM $"Invalid up/reset condition for {name}" (up.IsNone && rst.IsNone)
                | CTR ->
                    verifyM $"No load condition for {name}" (down.IsSome && rst.IsSome)
                    verifyM $"Invalid up/reset condition for {name}" (up.IsNone && ld.IsNone)
                | CTU ->
                    verifyM $"No up/reset condition for {name}" (up.IsSome && rst.IsSome)
                    verifyM $"Invalid down/load condition for {name}" (down.IsNone && ld.IsNone)

                (*
                 * XGK CTUD 에서 load
                 * - 별도의 statement 로 분리해서 구현 가능함: ldcondition --- MOV PV C0001
                 * - statement2statements 에서 이미 새로운 Rung 으로 분리되어 있어야 한다.
                 *)

                if isUpCounter then
                    verifyM $"No reset condition for {name}" rst.IsSome

                verifyM $"No up/down condition for {name}" (up.IsSome || down.IsSome)
                verifyM $"No up condition for {name}" (isDownCounter || up.IsSome)
                verifyM $"No down condition for {name}" (isUpCounter || down.IsSome)
                verifyM $"No down condition for {name}" (typ <> CTUD || (up.IsSome && down.IsSome))


                match rst, ld with
                | Some _, Some _ when ctr.Type <> CTUD  -> failwith $"Both reset and load condition specified for {name}."
                | None, None -> failwith $"No reset/load condition specified for {name}."
                | _ -> ()


                match typ with
                | CTU ->
                    verifyM "CTU condition error" (up.IsSome && down.IsNone)
                | CTD ->
                    verifyM "CTD condition error" (up.IsNone && down.IsSome)
                | CTUD ->
                    verifyM "CTUD condition error" (up.IsSome && down.IsSome)
                | CTR ->
                    verifyM "CTR condition error" (up.IsNone && down.IsSome)

            | (DuUdtDecl _ | DuUdtDef _) ->
                if prjParam.TargetType <> XGI then
                    failwith "UDT declaration is not supported in XGK"
            | DuAction(_a:ActionStatement) -> ()
            | DuPLCFunction(_fbParam) -> ()

            | (DuLambdaDecl _ | DuProcDecl _ | DuProcCall _) ->
                failwith "ERROR: Not yet implemented"       // 추후 subroutine 사용시, 필요에 따라 세부 구현

    type XgxPOUParams with
        member x.SanityCheck(prjParam: XgxProjectParams) =
            for CommentedStatement(_comment, stmt) in x.CommentedStatements do
                stmt.SanityCheck prjParam

            // todo: POU 내에서의 double coil (이중 코일) 체크
