

(*
    (FLOW)_(DEVICE)_(확실한 ACTION)$ 패턴을 먼저 찾아서
        - DEVICE 영역을 가변적으로 가져 갈 수 있도록 한다.
        - 확실한 ACTION 이름 : "_ADV$", "_RET$" 등
        - e.g "S303_SOL_SV_RH_B_PNL_CLP_ADV" 를 통해 확실한 ACTION 명인 "_ADV" 앞에서 FLOW 와 DEVICE 명을 구분
            "S303", "SOL_SV_RH_B_PNL_CLP"
            -> ErasePattern 적용: "SOL_SV_RH_B_PNL_CLP" -> "SV_RH_B_PNL_CLP"
            -> 다음과 같은 pattern 에 대해서


                "S303_M_SV_RH_B_PNL_CLP_ADV_END"       =>         "S303", "SV_RH_B_PNL_CLP", "ADV_END"
                "S303_M_SV_RH_B_PNL_CLP_ADV_ERR"       =>         "S303", "SV_RH_B_PNL_CLP", "ADV_ERR"
                "S303_M_SV_RH_B_PNL_CLP_ADV_HMI_LAMP"  =>         "S303", "SV_RH_B_PNL_CLP", "ADV_HMI_LAMP"
                "S303_M_SV_RH_B_PNL_CLP_ADV_LAMP"      =>         "S303", "SV_RH_B_PNL_CLP", "ADV_LAMP"
                "S303_M_SV_RH_B_PNL_CLP_A_ADV_AUX"     =>         "S303", "SV_RH_B_PNL_CLP", "A_ADV_AUX"
                "S303_M_SV_RH_B_PNL_CLP_A_RET_AUX"     =>         "S303", "SV_RH_B_PNL_CLP", "A_RET_AUX"
                "S303_M_SV_RH_B_PNL_CLP_C_ADV_AUX"     =>         "S303", "SV_RH_B_PNL_CLP", "C_ADV_AUX"
                "S303_M_SV_RH_B_PNL_CLP_C_RET_AUX"     =>         "S303", "SV_RH_B_PNL_CLP", "C_RET_AUX"
                "S303_M_SV_RH_B_PNL_CLP_RET_END"       =>         "S303", "SV_RH_B_PNL_CLP", "RET_END"
                "S303_M_SV_RH_B_PNL_CLP_RET_ERR"       =>         "S303", "SV_RH_B_PNL_CLP", "RET_ERR"
                "S303_M_SV_RH_B_PNL_CLP_RET_HMI_LAMP"  =>         "S303", "SV_RH_B_PNL_CLP", "RET_HMI_LAMP"
                "S303_M_SV_RH_B_PNL_CLP_RET_LAMP"      =>         "S303", "SV_RH_B_PNL_CLP", "RET_LAMP"

        - see DefinitelyActionPatterns on Semantic or appsettings.json

*)

namespace T



open NUnit.Framework


open Dual.Plc2DS
open Dual.Common.Core.FS
open System.Text.RegularExpressions
open Dual.Plc2DS.LS
open Dual.Common.Base

module MultiStaging =

    type MS() =
        [<Test>]
        member _.``Minimal`` () =
            let iqmPattern = Regex("^%[IQM]", RegexOptions.Compiled)
            let inputTags: IPlcTag[] =
                C.CollectTags([|"BB 메인제어반.csv"|], addressFilter = fun addr -> iqmPattern.IsMatch(addr) )
                |> filter (fun t -> (t :?> PlcTagInfo).Scope = "GlobalVariable")


            let sm = Semantic.Create()
            sm.DeviceNameErasePatternsDTO <- [| "^([IQMOXYDB]|[PRL]S|SOL|[PW]RS)_|_([IQMOXYDB]|PRL]S|SOL|[PW]RS)_" |]
            sm.SpecialActionPatterns <- [|"CARR_NO_\\d+"; "[A-Z]+(_|/)\\d+"; "(1ST|2ND|3RD|[4-9]TH)_IN_OK"|]
            sm.DefinitelyActionPatternsDTO <- [| "_(ADV|RET|OPEN|SAFETY)$" |]
            sm.FDARegexPatterns <- [| "^(?<flow>[^_]+)_[IQM]_(?<device>RB\\d+)_(?<action>.*)$" |]
            sm.CompileAllRegexPatterns()


            let extractedDeviceNames =
                let definitelyActionTags =
                    inputTags
                    |> filter (fun t -> let n = t.GetName() in sm.DefinitelyActionPatterns |> exists(fun p -> p.IsMatch n))
                definitelyActionTags |> choose _.TryGetFDA(sm) |> map (fun fda -> fda.DeviceName(*.ApplyRemovePatterns(sm.DeviceNameErasePatterns)*)) |> distinct |> sort

                //let xxxx = definitelyActionTags |> map (_.GetName())
            let newSm = sm.DuplicateWithDeviceNames(extractedDeviceNames)
            inputTags |> iter (fun t -> t.SetFDA(t.TryGetFDA(newSm)))

            let oks, errs = inputTags |> partition _.TryGetFDA().IsSome

            let okFDAs = oks |> map (fun t -> t.TryGetFDA() |> Option.get)
            let errs = errs |> map _.GetName() |> sort |> distinct

            let okFlows   = okFDAs |> map _.FlowName   |> sort |> distinct
            let okDevices = okFDAs |> map _.DeviceName |> map (tailNumberUnifier newSm newSm.DeviceNameErasePatterns) |> sort |> distinct
            let okActions = okFDAs |> map _.ActionName |> map (tailNumberUnifier newSm [||])                       |> sort |> distinct

            let n = 10
            okFlows   |> printN "Flows"   n
            okDevices |> printN "Devices" n
            okActions |> printN "Actions" n
            errs      |> printN "Errors"  n     // BLE_HARTBIT, BLE_HEARTBIT, S508LH_MAT1, S508LH_MAT2, S508RH_MAT1, S508RH_MAT2, S509LH_MAT1, S509LH_MAT2, S509RH_MAT1, S509RH_MAT2

            // oks 를 "{flow}::{device}::{action}" key 로 만들어서 grouping
            let grouped: (string * IPlcTag[])[] =
                let getKey (t:IPlcTag) =
                    let f = t.FlowName
                    let d = t.DeviceName.ApplyRemovePatterns(newSm.DeviceNameErasePatterns)
                    $"{f}::{d}"
                oks
                |> groupBy getKey
                |> sortByDescending (snd >> _.Length)
                //|> map (fun (key, tags) -> key, tags |> map snd)

            let nonBT = grouped |> filter (fun (k, ts) -> 2 <= ts.Length && ts.Length <= 10 && not (k.Contains("_BT")))
            let nonBTalpha = nonBT |> sortBy fst

            let advRetPattern = Regex("^(ADV|RET)\d*$", RegexOptions.Compiled)
            let nonBTnonAdvRet = nonBT |> filter (fun (k, ts) -> ts |> exists (fun t -> !! advRetPattern.IsMatch(t.ActionName)))

            let xxx = newSm
            noop()

