namespace Ev2.Core.FS

open Dual.Common.Base
open System.Runtime.Serialization
open Dual.Common.Core.FS

[<AutoOpen>]
module Ds2JsonModule =
    let private createDynamicDictionary() =
        let ddic = DynamicDictionary()
        ddic.Set("systems",    ResizeArray<DsSystem>())
        ddic.Set("flows",      ResizeArray<DsFlow>())
        ddic.Set("flowArrows", ResizeArray<Arrow<DsWork>>())
        ddic.Set("works",      ResizeArray<DsWork>())
        ddic.Set("workArrows", ResizeArray<Arrow<DsCall>>())
        ddic.Set("calls",      ResizeArray<DsCall>())
        ddic

    type DsProject with
        member x.ToJson():string =
            EmJson.ToJson(x)

        static member FromJson(json:string): DsProject =
            let settings = EmJson.CreateDefaultSettings()
            // Json deserialize 중에 필요한 담을 그릇 준비
            let ddic = createDynamicDictionary()
            settings.Context <- new StreamingContext(StreamingContextStates.All, ddic)

            let project = EmJson.FromJson<DsProject>(json, settings)
            project


    //type DsSystem with
    //    member x.ToJson():string =
    //        EmJson.ToJson(x)

    //    static member FromJson(json:string): DsSystem =
    //        let settings = EmJson.CreateDefaultSettings()
    //        let ddic = createDynamicDictionary()
    //        settings.Context <- new StreamingContext(StreamingContextStates.All, ddic)

    //        let system = EmJson.FromJson<DsSystem>(json, settings)
    //        system


    let private arrowToDto (a:IArrow) = DtoArrow(a.Guid, a.Id, a.SourceGuid, a.TargetGuid, a.DateTime)
    let private getArrowInfos (haystack:#Unique seq) (needle:DtoArrow) =
        let source = haystack |> Seq.find (fun w -> w.Guid = needle.Source)
        let target = haystack |> Seq.find (fun w -> w.Guid = needle.Target)
        let id = needle.Id |> Option.ofNullable
        let guid = needle.Guid
        let dateTime = needle.DateTime
        guid, source, target, dateTime, id

    /// JSON 쓰기 전에 메모리 구조에 전처리 작업
    let rec internal onSerializing (dsObj:IDsObject) =
        match dsObj with
        | :? DsProject as proj -> proj.ActiveSystems |> iter onSerializing
        | :? DsSystem as sys ->
            sys.DtoArrows <- sys.Arrows |-> arrowToDto
            sys.Flows |> iter onSerializing
            sys.Works |> iter onSerializing
        | :? DsFlow as flow ->
            flow.DtoArrows <- flow.Arrows |-> arrowToDto
        | :? DsWork as work ->
            work.DtoArrows <- work.Arrows |-> arrowToDto
        | _ -> failwith "ERROR.  확장 필요?"

    /// JSON 읽고 나서 메모리 구조에 후처리 작업
    let rec internal onDeserialized (dsObj:IDsObject) =
        match dsObj with
        | :? DsProject as proj ->
            proj.ActiveSystems |> iter onDeserialized
            proj.Systems |> iter (fun z -> z.RawParent <- Some proj)

        | :? DsSystem as sys ->
            sys.Arrows <-
                sys.DtoArrows
                |-> getArrowInfos sys.Works
                |-> (fun (guid, src, tgt, dateTime, id) -> ArrowBetweenWorks(guid, src, tgt, dateTime, ?id=id))
            sys.Flows |> iter onDeserialized
            sys.Works |> iter onDeserialized

        | :? DsFlow as flow ->
            flow.Arrows <-
                flow.DtoArrows
                |-> getArrowInfos flow.Works
                |-> (fun (guid, src, tgt, dateTime, id) -> ArrowBetweenWorks(guid, src, tgt, dateTime, ?id=id))
        | :? DsWork as work ->
            work.Arrows <-
                work.DtoArrows
                |-> getArrowInfos work.Calls
                |-> (fun (guid, src, tgt, dateTime, id) -> ArrowBetweenCalls(guid, src, tgt, dateTime, ?id=id))
        | _ -> failwith "ERROR.  확장 필요?"


