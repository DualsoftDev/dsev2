namespace T


open System.IO

open NUnit.Framework

open Dual.Common.UnitTest.FS

open Dual.Plc2DS
open Dual.Common.Core.FS
open Dual.Common.Base.FS
open System.Text.RegularExpressions

[<AutoOpen>]
module BatchCommon =
    let dataDir = "Z:/dsev2/src/misc/Plc2DS/unit-test/Plc2DS.UnitTest/Samples/LS/Autoland광명2"
    let sm = EmJson.FromJson<SemanticSettings>(File.ReadAllText("Z:/dsev2/src/misc/Plc2DS/ConsoleTestApp/appsettings.json"))

    let csvs = [|
        "BB 메인제어반.csv"
        "BC 로컬 메인제어반.csv"
        "BC1 메인제어반.csv"
        "BR 메인제어반.csv"
        "CRP 메인제어반.csv"
        "F_COMPL 메인제어반.csv"
        "FLR RESPOT 메인제어반.csv"
        "FRT FLR 메인제어반.csv"
        "ROOF 메인제어반.csv"
        "S_COMPL LH 메인제어반.csv"
        "S_COMPL RH 메인제어반.csv"
    |]

    type C =
        static member CollectTags (csvs:string[], ?addressFilter:string -> bool): IPlcTag[] =
            csvs
            |> map (fun csv -> Path.Combine(dataDir, csv))
            |> map (fun csv -> CsvReader.Read(Vendor.LS, csv, ?addressFilter=addressFilter))
            |> Array.concat


    let sortResults (results: Result<(IPlcTag * FDA), IPlcTag>[]) =
        results
        |> Array.sortWith (fun a b ->
            match a, b with
            | Ok (s1, _), Ok (s2, _) -> compare (s1.GetName()) (s2.GetName())  // Ok 내부 알파벳 정렬
            | Error e1, Error e2 -> compare (e1.GetName()) (e2.GetName())      // Error 내부 알파벳 정렬
            | Ok _, Error _ -> -1  // Ok를 Error보다 먼저 배치
            | Error _, Ok _ -> 1   // Error를 Ok보다 뒤에 배치
        )

    let printN (header:string) (size:int) (items:string[]) =
        tracefn $":::: {header} {items.Length}"
        for s10 in items |> chunkBySize size do
            s10 |> String.concat ", " |> tracefn "\t%s"


    /// 단어 뒤에 숫자(혹은 array index) 형식의 이름 match.  성능 향상을 위해 local 변수로 만들 수 없음.
    let tailNumberPattern = Regex(@"^([a-zA-Z가-힣_\.]+)_?(\d+|\[[\d,\.]+\])$", RegexOptions.Compiled)
    let tailNumberUnifier (sm:Semantic) (erasePatterns:Regex[]) name =
        let discardedName =
            TagString.SplitName(name, erasePatterns=erasePatterns)
            |> String.concat "_"
        let m =
            discardedName |> tailNumberPattern.Match
        if m.Success then
            $"{m.Groups[1].Value}$" // pattern 에서 단어 부분만 추출하고 나머지는 "$" 로 대체
        else
            discardedName


module Batch =
    type B() =
        [<Test>]
        member _.``Minimal`` () =
            let inputTags: IPlcTag[] = C.CollectTags([|"BC 로컬 메인제어반.csv"|], addressFilter = fun addr -> addr.StartsWith("%I"))
            let inputTagNames = inputTags |> map _.GetName()


            do
                for tag in inputTags do
                    match tag.TryGetFDA(sm) with
                    | Some fda ->
                        let f, d, a = fda.GetTuples()
                        tracefn $"{tag.GetName()}: {f}, {d}, {a}"
                    | None ->
                        logWarn $"------------ {tag.GetName()}: Failed to match"
            noop()


            (*
                이상한 태그들
            *)
            [
                "MES_POS[1].BODY_NO_2"
                "MES_POS[1].공통버퍼_SPARE_2"
                "POINT_4.지시_DATA.공통버퍼_BODY_TYPE_1"
                "S510_RR_RH_SERVO.MANUAL_SEL_A"
                "S510_RR_RH_SERVO.CTYPE_A"
                "S510_M_RR_LH_SRV_SET_POSI_E"
                "S510_M_RR_LH_SRV_MOVE_START_T.ET"
                "S510_FRT_LH_SRV_EIP1.AxisPosOk"
                "S510_CTYPE.M_CType_R_CAM_O"
            ] |> ignore


        [<Test>]
        member _.``CollectFDANames`` () =
            //let inputTags: IPlcTag[] = C.CollectTags([|"BB 메인제어반.csv"|], addressFilter = fun addr -> addr.StartsWith("%I"))
            let inputTags: IPlcTag[] = C.CollectTags(csvs(*, addressFilter = fun addr -> addr.StartsWith("%I")*))
            inputTags |> iter (fun t -> t.SetFDA(t.TryGetFDA(sm)))
            let oks, errs = inputTags |> partition _.TryGetFDA().IsSome

            let okFDAs = oks |> map (fun t -> t.TryGetFDA() |> Option.get)
            let errs = errs |> map _.GetName() |> sort |> distinct

            let okFlows   = okFDAs |> map _.FlowName   |> sort |> distinct
            let okDevices = okFDAs |> map _.DeviceName |> sort |> distinct
            let okActions = okFDAs |> map _.ActionName |> sort |> distinct

            let n = 10
            okFlows   |> printN "Flows"   n
            okDevices |> printN "Devices" n
            okActions |> printN "Actions" n
            errs      |> printN "Errors"  n     // BLE_HARTBIT, BLE_HEARTBIT, S508LH_MAT1, S508LH_MAT2, S508RH_MAT1, S508RH_MAT2, S509LH_MAT1, S509LH_MAT2, S509RH_MAT1, S509RH_MAT2

            noop()


    type Filtered() =
        let sm = Semantic.Create()
        do
            sm.DeviceNameErasePatternsDTO <- [| "^([IQMOXYDB]|[PRL]S|SOL|[PW]RS)_|_([IQMOXYDB]|PRL]S|SOL|[PW]RS)_" |]
            sm.SpecialActionPatterns <- [|"CARR_NO_\\d+"; "[A-Z]+(_|/)\\d+"; "(1ST|2ND|3RD|[4-9]TH)_IN_OK"|]
            sm.CompileAllRegexPatterns()

        [<Test>]
        member _.``CollectFDANamesFiltered`` () =
            let inputTags: IPlcTag[] = C.CollectTags([|"BB 메인제어반.csv"|], addressFilter = fun addr -> addr.StartsWith("%I") || addr.StartsWith("%Q"))
            //let inputTags: IPlcTag[] = C.CollectTags(csvs(*, addressFilter = fun addr -> addr.StartsWith("%I")*))
            inputTags |> iter (fun t -> t.SetFDA(t.TryGetFDA(sm)))
            let oks, errs = inputTags |> partition _.TryGetFDA().IsSome

            let okFDAs = oks |> map (fun t -> t.TryGetFDA() |> Option.get)
            let errs = errs |> map _.GetName() |> sort |> distinct

            let okFlows   = okFDAs |> map _.FlowName   |> sort |> distinct
            let okDevices = okFDAs |> map _.DeviceName |> map (tailNumberUnifier sm sm.DeviceNameErasePatterns) |> sort |> distinct
            let okActions = okFDAs |> map _.ActionName |> map (tailNumberUnifier sm [||])                       |> sort |> distinct

            let n = 10
            okFlows   |> printN "Flows"   n
            okDevices |> printN "Devices" n
            okActions |> printN "Actions" n
            errs      |> printN "Errors"  n     // BLE_HARTBIT, BLE_HEARTBIT, S508LH_MAT1, S508LH_MAT2, S508RH_MAT1, S508RH_MAT2, S509LH_MAT1, S509LH_MAT2, S509RH_MAT1, S509RH_MAT2

            noop()

        [<Test>]
        member _.``CollectFDANames개별Tag분석`` () =
            let dq = "\""
            let ddq = "\"\""
            let tagInfo = LS.CsvReader.CreatePlcTagInfo($"Tag,GlobalVariable,{dq}S305_Q_RB4_PLT3_COUNT_RST{dq},%%QW3345.2,{dq}BOOL{dq},,{ddq}")
            match tagInfo.TryGetFDA(sm) with
            | Some fda ->
                let f, d, a = fda.GetTuples()
                tracefn $"{tagInfo.GetName()}: {f}, {d}, {a}"
                tailNumberUnifier sm sm.DeviceNameErasePatterns d === "RB4_PLT3_COUNT"        // w/o "Q"
                noop()
            | None ->
                noop()


            let tagInfo = LS.CsvReader.CreatePlcTagInfo($"Tag,GlobalVariable,{dq}DNDL_I_RB1_PROG_ECHO_1{dq},%%QW3345.2,{dq}BOOL{dq},,{ddq}")
            match tagInfo.TryGetFDA(sm) with
            | Some fda ->
                let f, d, a = fda.GetTuples()
                tracefn $"{tagInfo.GetName()}: {f}, {d}, {a}"
                f === "DNDL"
                d === "I_RB1_PROG"
                a === "ECHO_1"
                tailNumberUnifier sm sm.DeviceNameErasePatterns d === "RB1_PROG"        // w/o "Q"
                noop()
            | None ->
                noop()


            do
                let sm = sm.Duplicate()
                sm.SpecialActionPatterns <- [| "(1ST|2ND|3RD|[4-9]TH)_IN_OK" |]
                sm.CompileAllRegexPatterns()
                let xxx = sm.SpecialActionPatterns
                let tagInfo = LS.CsvReader.CreatePlcTagInfo($"Tag,GlobalVariable,{dq}S231_M_RBT4_2ND_IN_OK{dq},%%QW3345.2,{dq}BOOL{dq},,{ddq}")
                match tagInfo.TryGetFDA(sm) with
                | Some fda ->
                    let f, d, a = fda.GetTuples()
                    f === "S231"
                    d === "M_RBT4"
                    a === "2ND_IN_OK"
                    noop()
                | None ->
                    noop()
