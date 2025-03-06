namespace Dual.Plc2DS

open System
open System.Collections.Generic
open System.Runtime.Serialization
open Newtonsoft.Json

open Dual.Common.Core.FS

[<AutoOpen>]
module AppSettingsModule =

    type WordSet = HashSet<string>
    type Words = string[]

    [<DataContract>]
    type TagSemantics() =
        /// 행위 keyword. e.g "ADV", "RET",
        [<DataMember>] member val Actions = WordSet(StringComparer.OrdinalIgnoreCase) with get, set
        /// 상태 keyword. e.g "ERR"
        [<DataMember>] member val States = WordSet(StringComparer.OrdinalIgnoreCase) with get, set
        /// Mutual Reset Pairs. e.g ["ADV"; "RET"]
        [<DataMember>] member val MutualResetTuples:WordSet[] = [||] with get, set
        /// Alias.  e.g [CLAMP, CLP, CMP].  [][0] 가 표준어, 나머지는 dialects
        [<JsonProperty("Dialects")>] // JSON에서는 "Dialects"라는 이름으로 저장
        [<DataMember>] member val DialectsDTO:Words[] = [||] with get, set

        /// 표준어 사전: Dialect => Standard
        [<JsonIgnore>] member val Dialects = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)

        [<DataMember>] member val FlowNames = WordSet(StringComparer.OrdinalIgnoreCase) with get, set
        [<DataMember>] member val DeviceNames = WordSet(StringComparer.OrdinalIgnoreCase) with get, set

        [<OnDeserialized>]
        member x.OnDeserializedMethod(context: StreamingContext) =
            for ds in x.DialectsDTO do
                let std = ds.[0]
                let dialects = ds[1..]
                dialects |> iter (fun d -> x.Dialects.Add(d, std))

    type AppSettings() =
        inherit TagSemantics()

    let splitTailNumber(pName:string): string * int option = // pName : partial name: '_' 로 분리된 이름 중 하나
        match pName with
        | RegexPattern @"^(\w+)(\d+)$" [name; Int32Pattern number] -> name, Some number
        | _ -> pName, None

    type TagSemantics with
        /// pName 에서 뒤에 붙은 숫자 부분 제거 후, 표준어로 변환
        member x.StandardizePName(pName:string): string * int option =   // pName : partial name: '_' 로 분리된 이름 중 하나
            let name, optNumber = splitTailNumber pName
            match x.Dialects.TryGet(name) with
            | Some standard -> standard, optNumber
            | None -> name, optNumber

        /// 공통 검색 함수: standardPNames 배열에서 targetSet에 있는 첫 번째 단어 반환 (없으면 null)
        [<Obsolete("추후 고려")>]
        member private x.GuessName(targetSet: WordSet, standardPNames: string[]): string =
            // 일단 match 되는 하나라도 있으면 바로 리턴.. 추후에는 갯수와 위치 등을 고려해야 함
            standardPNames |> Array.tryFind targetSet.Contains |? null


        member x.GuessFlowName(standardPNames: Words): string =
            x.GuessName(x.FlowNames, standardPNames)

        member x.GuessDeviceName(standardPNames: Words): string =
            x.GuessName(x.DeviceNames, standardPNames)

        member x.GuessActionName(standardPNames: Words): string =
            x.GuessName(x.Actions, standardPNames)

        member x.GuessStateName(standardPNames: Words): string =
            x.GuessName(x.States, standardPNames)
