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

    type IPlcTag with
        member x.OptFDA
            with get() =
                let xxx = x
                x.Temporary :?> FDA option
            and set (fda: FDA option) =
                x.Temporary <- fda

module Batch =
    type B() =
        [<Test>]
        member _.``Minimal`` () =
            let inputTags: IPlcTag[] = C.CollectTags([|"BC 로컬 메인제어반.csv"|], addressFilter = fun addr -> addr.StartsWith("%I"))
            let inputTagNames = inputTags |> map _.GetName()


            do
                for tag in inputTags do
                    match tag.TryGetFDA(sm) with
                    | Some { Flow = f; Device = d; Action = a } ->
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
            (*
                "BB 메인제어반.csv" Input 기준 갯수 : errs(1), okFlows(29), okDevices(1318), okActions(404)
                전체 Input 기준 갯수 : errs(11), okFlows(126), okDevices(5196), okActions(755)
                전체 기준 갯수 : errs(15972), okFlows(429), okDevices(48325), okActions(6869)

                see anal.txt
            *)
            //let inputTags: IPlcTag[] = C.CollectTags([|"BB 메인제어반.csv"|], addressFilter = fun addr -> addr.StartsWith("%I"))
            let inputTags: IPlcTag[] = C.CollectTags(csvs(*, addressFilter = fun addr -> addr.StartsWith("%I")*))
            inputTags |> iter (fun t -> t.OptFDA <- t.TryGetFDA(sm))
            let oks, errs = inputTags |> partition _.OptFDA.IsSome

            let okFDAs = oks |> map _.OptFDA.Value
            let errs = errs |> map _.GetName() |> sort |> distinct

            let okFlows   = okFDAs |> map _.Flow   |> sort |> distinct
            let okDevices = okFDAs |> map _.Device |> sort |> distinct
            let okActions = okFDAs |> map _.Action |> sort |> distinct

            let n = 10
            okFlows   |> printN "Flows"   n
            okDevices |> printN "Devices" n
            okActions |> printN "Actions" n
            errs      |> printN "Errors"  n     // BLE_HARTBIT, BLE_HEARTBIT, S508LH_MAT1, S508LH_MAT2, S508RH_MAT1, S508RH_MAT2, S509LH_MAT1, S509LH_MAT2, S509RH_MAT1, S509RH_MAT2

            noop()


    type Filtered() =
        let sm = Semantic.Create()
        do
            sm.DeviceNameErasePatternsDTO <- [| "^([IQMOXYDB]|PS|RS|LS|SOL|[PW]RS)_|_([IQMOXYDB]|PS|RS|LS|SOL|[PW]RS)_" |]
            sm.SpecialActionPatterns <- [|"CARR_NO_\\d+"; "[A-Z]+(_|/)\\d+"; "(1ST|2ND|3RD|[4-9]TH)_IN_OK"|]
            sm.CompileRegexPatterns()

        [<Test>]
        member _.``CollectFDANamesFiltered`` () =
            (*
                "BB 메인제어반.csv" Input 기준 갯수 : errs(1), okFlows(29), okDevices(1305), okActions(83)
                전체 Input 기준 갯수 : errs(11), okFlows(31), okDevices(1973), okActions(109)
                전체 기준 갯수 : errs(), okFlows(), okDevices(), okActions()

                see anal.txt
            *)

            let inputTags: IPlcTag[] = C.CollectTags([|"BB 메인제어반.csv"|], addressFilter = fun addr -> addr.StartsWith("%I") || addr.StartsWith("%Q"))
            //let inputTags: IPlcTag[] = C.CollectTags(csvs(*, addressFilter = fun addr -> addr.StartsWith("%I")*))
            inputTags |> iter (fun t -> t.OptFDA <- t.TryGetFDA(sm))
            let oks, errs = inputTags |> partition _.OptFDA.IsSome

            let okFDAs = oks |> map _.OptFDA.Value
            let errs = errs |> map _.GetName() |> sort |> distinct

            let okFlows   = okFDAs |> map _.Flow   |> sort |> distinct
            let okDevices = okFDAs |> map _.Device |> map (tailNumberUnifier sm sm.DeviceNameErasePatterns) |> sort |> distinct
            let okActions = okFDAs |> map _.Action |> map (tailNumberUnifier sm [||])                       |> sort |> distinct

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
            | Some {Flow=f; Device=d; Action=a} ->
                tracefn $"{tagInfo.GetName()}: {f}, {d}, {a}"
                tailNumberUnifier sm sm.DeviceNameErasePatterns d === "RB4_PLT3_COUNT"        // w/o "Q"
                noop()
            | None ->
                noop()


            let tagInfo = LS.CsvReader.CreatePlcTagInfo($"Tag,GlobalVariable,{dq}DNDL_I_RB1_PROG_ECHO_1{dq},%%QW3345.2,{dq}BOOL{dq},,{ddq}")
            match tagInfo.TryGetFDA(sm) with
            | Some {Flow=f; Device=d; Action=a} ->
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
                sm.CompileRegexPatterns()
                let xxx = sm.SpecialActionPatterns
                let tagInfo = LS.CsvReader.CreatePlcTagInfo($"Tag,GlobalVariable,{dq}S231_M_RBT4_2ND_IN_OK{dq},%%QW3345.2,{dq}BOOL{dq},,{ddq}")
                match tagInfo.TryGetFDA(sm) with
                | Some {Flow=f; Device=d; Action=a} ->
                    tracefn $"{tagInfo.GetName()}: {f}, {d}, {a}"
                    f === "S231"
                    d === "M_RBT4"
                    a === "2ND_IN_OK"
                    noop()
                | None ->
                    noop()
