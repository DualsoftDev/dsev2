namespace Dual.Plc2DS

open System.Collections.Generic
open Dual.Plc2DS.Common.FS
open Dual.Common.Core.FS

[<AutoOpen>]
module ExtractDeviceModule =
    type Call = {
        Name: string    // e.g "ADV"
        Input: HashSet<IPlcTag>
        Output: HashSet<IPlcTag>
    }

    type Device = {
        Name: string    // e.g "Cyl1"
        FlowName: string    // e.g "STN1"
        Calls: Call[]
        MutualResetTuples: Call[][]
    }


    [<AutoOpen>]
    module (*internal*) rec ExtractDeviceImplModule =

        type AnalyzedNameSemantic = {
            FullName: string        // 이름 원본
            SplitNames: string[]    // '_' 기준 분리된 이름
            mutable FlowName: string
            mutable ActionName: string      // e.g "ADV"
            mutable DeviceName: string      // e.g "ADV"
            mutable InputAuxNumber: int option  // e.g "ADV1" -> 1
            mutable StateName: string       // e.g "ERR"
        } with
            static member Create(name:string, ?semantics:TagSemantics) =
                let splitNames = name.Split('_')
                let baseline =
                    {   FullName = name; SplitNames = splitNames
                        FlowName = ""; ActionName = ""; DeviceName = ""
                        InputAuxNumber = None; StateName = "" }
                match semantics with
                | Some sm ->
                    let standardPNamesAndNumbers = baseline.SplitNames |> map sm.StandardizePName
                    let standardPNames = standardPNamesAndNumbers |> map fst


                    let flow, action, state, device =
                        sm.GuessFlowName standardPNames,
                        sm.GuessActionName standardPNames,
                        sm.GuessStateName standardPNames,
                        sm.GuessDeviceName standardPNames

                    { baseline with
                        FlowName = flow
                        ActionName = action
                        DeviceName = device
                        //InputAuxNumber = splitNames.[1] |> GetAuxNumber
                        StateName = state }
                | None -> baseline


    type Builder =
        static member ExtractDevices(plcTags:#IPlcTag[], semantics:TagSemantics): Device[] =
            let anals:AnalyzedNameSemantic[] =
                plcTags
                |> map (fun t ->
                    let name = t.GetName()
                    AnalyzedNameSemantic.Create(name, semantics))
            [||]
