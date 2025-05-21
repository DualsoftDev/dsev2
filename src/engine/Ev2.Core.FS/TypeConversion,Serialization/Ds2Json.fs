namespace Ev2.Core.FS

open Dual.Common.Base
open System.Runtime.Serialization

[<AutoOpen>]
module Ds2JsonModule =
    type DsProject with
        member x.ToJson():string =
            EmJson.ToJson(x)

        static member FromJson(json:string): DsProject =
            let settings = EmJson.CreateDefaultSettings()
            // Json deserialize 중에 필요한 담을 그릇 준비
            let ddic = DynamicDictionary()
            ddic.Set("systems", ResizeArray<DsSystem>())
            ddic.Set("flows", ResizeArray<DsFlow>())
            ddic.Set("works", ResizeArray<DsWork>())
            ddic.Set("calls", ResizeArray<DsCall>())
            settings.Context <- new StreamingContext(StreamingContextStates.All, ddic)

            let project = EmJson.FromJson<DsProject>(json, settings)
            project


    //type DsSystem with
    //    member x.ToJson():string =
    //        EmJson.ToJson(x)

    //    static member FromJson(json:string): DsSystem =
    //        let settings = EmJson.CreateDefaultSettings()
    //        let ddic = DynamicDictionary()
    //        ddic.Set("flows", ResizeArray<DsFlow>())
    //        ddic.Set("works", ResizeArray<DsWork>())
    //        ddic.Set("calls", ResizeArray<DsCall>())
    //        settings.Context <- new StreamingContext(StreamingContextStates.All, ddic)

    //        let system = EmJson.FromJson<DsSystem>(json, settings)
    //        system

