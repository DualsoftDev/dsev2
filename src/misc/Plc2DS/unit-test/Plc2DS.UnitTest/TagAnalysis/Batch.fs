namespace T


open System.IO

open NUnit.Framework

open Dual.Common.UnitTest.FS

open Dual.Plc2DS
open Dual.Common.Core.FS
open Dual.Common.Base.FS

module Batch =
    let dataDir = "Z:/dsev2/src/misc/Plc2DS/unit-test/Plc2DS.UnitTest/Samples/LS/Autoland광명2"
    let sm = EmJson.FromJson<SemanticSettings>(File.ReadAllText("Z:/dsev2/src/misc/Plc2DS/ConsoleTestApp/appsettings.json"))

    let csvs = [
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
    ]

    type C =
        static member CollectTags (csvs:string[], ?addressFilter:string -> bool): IPlcTag[] =
            csvs
            |> map (fun csv -> Path.Combine(dataDir, csv))
            |> map (fun csv -> CsvReader.Read(Vendor.LS, csv, ?addressFilter=addressFilter))
            |> Array.concat

    //let rtryAnalyze (tag:IPlcTag): Result< =
    //    match tag.TryGetFDA(sm) with
    //    | Some (f, d, a) -> tracefn $"{tag.GetName()}: {f}, {d}, {a}"
    //    | None -> logWarn $"------------ {tag.GetName()}: Failed to match"

    type B() =
        [<Test>]
        member _.``Minimal`` () =
            let inputTags: IPlcTag[] = C.CollectTags([|"BC 로컬 메인제어반.csv"|], addressFilter = fun addr -> addr.StartsWith("%I"))
            let inputTagNames = inputTags |> map _.GetName()


            do
                for tag in inputTags do
                    match tag.TryGetFDA(sm) with
                    | Some (f, d, a) ->
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




