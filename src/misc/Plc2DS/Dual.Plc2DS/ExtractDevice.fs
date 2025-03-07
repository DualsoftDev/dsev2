namespace Dual.Plc2DS

open System
open System.Collections.Generic
open System.Text.RegularExpressions

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
            mutable Modifiers: string[]
            mutable InputAuxNumber: int option  // e.g "ADV1" -> 1
            mutable StateName: string       // e.g "ERR"
        } with
            static member Create(name:string, ?semantics:Semantic) =
                // camelCase 분리 : aCamelCase -> [| "a"; "Camel"; "Case" |]
                let splitCamelCase (input: string) =
                    let sep = "<_sep_>"
                    Regex.Replace(input, "(?<!^)([A-Z])", $"{sep}$1") // 첫 글자는 제외하고 대문자 앞에 separator 추가
                        .Split(sep)
                let isSplitOnCamelCase = semantics.Map(_.SplitOnCamelCase) |? false
                let splitter (x:string) = if isSplitOnCamelCase then splitCamelCase x else [|x|]

                let splitNames =
                    let delimiter:string[] = semantics.Map(_.NameSeparators.ToArray()) |? [|"_"|]
                    name.Split(delimiter, StringSplitOptions.RemoveEmptyEntries)
                    |> bind splitter
                    |> map _.ToUpper()

                let baseline =
                    {   FullName = name; SplitNames = splitNames
                        FlowName = ""; ActionName = ""; DeviceName = ""; Modifiers = [||]
                        InputAuxNumber = None; StateName = "" }
                match semantics with
                | Some sm ->
                    let standardPNamesAndNumbers = baseline.SplitNames |> map sm.StandardizePName
                    let standardPNames = standardPNamesAndNumbers |> map fst


                    let flow   = sm.GuessFlowName   standardPNames
                    let action = sm.GuessActionName standardPNames
                    let state  = sm.GuessStateName  standardPNames
                    let device = sm.GuessDeviceName standardPNames
                    let modifiers = sm.GuessModifierNames standardPNames

                    { baseline with
                        FlowName = flow
                        ActionName = action
                        DeviceName = device
                        //InputAuxNumber = splitNames.[1] |> GetAuxNumber
                        StateName = state
                        Modifiers = modifiers
                    }
                | None -> baseline


    type Builder =
        static member ExtractDevices(plcTags:#IPlcTag[], semantics:Semantic): Device[] =
            let anals:AnalyzedNameSemantic[] =
                plcTags
                |> map (fun t ->
                    let name = t.GetName()
                    AnalyzedNameSemantic.Create(name, semantics))
            [||]
