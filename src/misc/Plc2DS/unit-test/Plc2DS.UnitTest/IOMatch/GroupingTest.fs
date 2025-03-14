namespace T


open System.IO

open NUnit.Framework

open Dual.Common.UnitTest.FS

open Dual.Plc2DS
open Dual.Common.Core.FS
open Dual.Common.Base.FS
open System.Text.RegularExpressions
open Dual.Plc2DS.LS

module GroupingTest =

    type G() =
        [<Test>]
        member _.``Minimal`` () =
            let inputTags: IPlcTag[] =
                C.CollectTags([|"BB 메인제어반.csv"|], addressFilter = fun addr -> addr.StartsWith("%I") || addr.StartsWith("%Q"))
                |> filter (fun t -> (t :?> PlcTagInfo).Scope = "GlobalVariable")

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


            // oks 를 "{flow}::{device}::{action}" key 로 만들어서 grouping
            let grouped: (string * IPlcTag[])[] =
                let getKey (t:IPlcTag) =
                    let f = t.FlowName
                    let d = t.DeviceName.ApplyRemovePatterns(sm.DeviceNameErasePatterns)
                    $"{f}::{d}"
                oks
                |> groupBy getKey
                |> sortByDescending (snd >> _.Length)
                //|> map (fun (key, tags) -> key, tags |> map snd)

            let nonBT = grouped |> filter (fun (k, ts) -> 2 <= ts.Length && ts.Length <= 10 && not (k.Contains("_BT")))

            let advRetPattern = Regex("^(ADV|RET)\d*$", RegexOptions.Compiled)
            let nonBTnonAdvRet = nonBT |> filter (fun (k, ts) -> ts |> exists (fun t -> !! advRetPattern.IsMatch(t.ActionName)))

            let xxx = sm
            noop()



