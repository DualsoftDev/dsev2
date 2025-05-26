namespace Ev2.Core.FS

open System
open System.IO
open System.Runtime.Serialization
open System
open System.Runtime.CompilerServices

open Dual.Common.Base
open Dual.Common.Core.FS

/// Ds Object 를 JSON 으로 변환하기 위한 모듈
[<AutoOpen>]
module Ds2JsonModule =
    type NjProject with
        /// DsProject 를 JSON 문자열로 변환
        member x.ToJson():string = EmJson.ToJson(x)
        member x.ToJson(jsonFilePath:string) =
            EmJson.ToJson(x)
            |> tee(fun json -> File.WriteAllText(jsonFilePath, json))

        /// JSON 문자열을 DsProject 로 변환
        static member FromJson(json:string): NjProject =
            (* Simple version *)
            //EmJson.FromJson<DsProject>(json)

            (* Withh context version *)
            let settings = EmJson.CreateDefaultSettings()
            // Json deserialize 중에 필요한 담을 그릇 준비
            let ddic = DynamicDictionary() |> tee(fun dic -> ())
            settings.Context <- new StreamingContext(StreamingContextStates.All, ddic)

            EmJson.FromJson<NjProject>(json, settings)


    //type DsSystem with
    //    member x.ToJson():string = EmJson.ToJson(x)
    //    static member FromJson(json:string): DsSystem = EmJson.FromJson<DsSystem>(json)


    type DsProject with
        /// DsProject 를 JSON 문자열로 변환
        member x.ToJson():string = EmJson.ToJson(x)
        member x.ToJson(jsonFilePath:string) =
            NjProject.FromDs(x).ToJson(jsonFilePath)
            //EmJson.ToJson(x)
            //|> tee(fun json -> File.WriteAllText(jsonFilePath, json))

        /// JSON 문자열을 DsProject 로 변환
        static member FromJson(json:string): DsProject = json |> NjProject.FromJson |> _.DsObject :?> DsProject
