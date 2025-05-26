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
        [<Obsolete("Use NjProject.ToJson() instead")>]
        member x.ToJson():string = EmJson.ToJson(x)
        [<Obsolete("Use NjProject.ToJson() instead")>]
        member x.ToJson(jsonFilePath:string) =
            NjProject.FromDs(x).ToJson(jsonFilePath)
            //EmJson.ToJson(x)
            //|> tee(fun json -> File.WriteAllText(jsonFilePath, json))

        /// JSON 문자열을 DsProject 로 변환
        [<Obsolete("Use NjProject.FromJson() instead")>]
        static member FromJson(json:string): DsProject = json |> NjProject.FromJson |> _.DsObject :?> DsProject
            //(* Simple version *)
            ////EmJson.FromJson<DsProject>(json)

            //(* Withh context version *)
            //let settings = EmJson.CreateDefaultSettings()
            //// Json deserialize 중에 필요한 담을 그릇 준비
            //let ddic = DynamicDictionary() |> tee(fun dic -> ())
            //settings.Context <- new StreamingContext(StreamingContextStates.All, ddic)

            //EmJson.FromJson<DsProject>(json, settings)



    /// DsArrow 를 JSON 변환하기 위한 DtoArrow 객체로 변환
    let private arrowToDto (a:IArrow) =
        match a with
        | :? ArrowBetweenCalls as a -> DtoArrow(a.Guid, a.Id, a.Source.Guid, a.Target.Guid, a.DateTime)
        | :? ArrowBetweenWorks as a -> DtoArrow(a.Guid, a.Id, a.Source.Guid, a.Target.Guid, a.DateTime)
        | _ -> failwith $"Unknown type {a.GetType()} in arrowToDto"

    /// JSON 읽어서 생긴 DtoArrow 로부터 DsArrow 객체를 생성하기 위한 정보 수집
    // haystack: Arrow 의 parent 에서 얻은 화살표 src, tgt 이 될수 있는 후보 Ds object 객체.
    //   - needle 이 System 하부의 DtoArrow 인 경우: works
    //   - needle 이 Works 하부의 DtoArrow 인 경우: calls
    let private getArrowInfos (haystack:#Unique seq) (needle:DtoArrow) =
        let source = haystack |> Seq.find (fun w -> w.Guid = needle.Source)
        let target = haystack |> Seq.find (fun w -> w.Guid = needle.Target)
        let id = needle.DbId |> Option.ofNullable
        let guid = needle.Guid
        let dateTime = needle.DateTime
        guid, source, target, dateTime, id

    ///// JSON 쓰기 전에 메모리 구조에 전처리 작업
    //[<Obsolete("Use NjProject version onSerializing() instead")>]
    //let rec internal onSerializingDs (dsObj:IDsObject) =
    //    match dsObj with
    //    | :? Unique as uniq ->
    //        uniq.DbId <- uniq.Id |> Option.toNullable
    //    | _ -> ()

    //    match dsObj with
    //    | :? DsProject as proj ->
    //        proj.SystemPrototypes <-
    //            let originals, copies = proj.ActiveSystems |> partition (fun s -> s.OriginGuid.IsNone)
    //            let distinctCopies = copies |> distinctBy _.Guid
    //            originals @ distinctCopies
    //        proj.ActiveSystemGuids  <- proj.ActiveSystems  |-> (_.Guid >> guid2str)
    //        proj.PassiveSystemGuids <- proj.PassiveSystems |-> (_.Guid >> guid2str)

    //        proj.SystemPrototypes |> iter onSerializingDs

    //    | :? DsSystem as sys ->
    //        sys.DtoArrows <- sys.Arrows |-> arrowToDto
    //        sys.Flows |> iter onSerializingDs
    //        sys.Works |> iter onSerializingDs

    //    | :? DsFlow as flow ->
    //        ()

    //    | :? DsWork as work ->
    //        work.DtoArrows <- work.Arrows |-> arrowToDto |> List.ofSeq
    //        work.Calls |> iter onSerializingDs
    //        work.TryGetFlow() |> iter (fun f -> work.FlowGuid <- guid2str f.Guid)
    //    | :? DsCall as call ->
    //        ()
    //    | _ -> failwith "ERROR.  확장 필요?"

    ///// JSON 읽고 나서 메모리 구조에 후처리 작업
    //[<Obsolete("Use NjProject version onDeserialized() instead")>]
    //let rec internal onDeserializedDs (dsObj:IDsObject) =
    //    match dsObj with
    //    | :? Unique as uniq ->
    //        uniq.Id <- uniq.DbId |> Option.ofNullable
    //    | _ -> ()

    //    match dsObj with
    //    | :? DsProject as proj ->
    //        proj.SystemPrototypes |> iter onDeserializedDs

    //        [
    //            for guid in proj.ActiveSystemGuids |-> (fun g -> Guid.Parse g) do
    //                proj.SystemPrototypes |> find (fun s -> s.Guid = guid)
    //        ] |> proj.forceSetActiveSystems

    //        [
    //            for guid in proj.PassiveSystemGuids |-> (fun g -> Guid.Parse g) do
    //                proj.SystemPrototypes |> find (fun s -> s.Guid = guid)
    //        ] |> proj.forceSetPassiveSystems

    //        proj.Systems |> iter (fun z -> z.RawParent <- Some proj)

    //    | :? DsSystem as sys ->
    //        sys.DtoArrows
    //        |-> getArrowInfos sys.Works
    //        |-> (fun (guid, src, tgt, dateTime, id) -> ArrowBetweenWorks(guid, src, tgt, dateTime, ?id=id))
    //        |> sys.forceSetArrows

    //        // flows, works, arrows 의 Parent 를 this(system) 으로 설정
    //        sys.Arrows |> iter (fun z -> z.RawParent <- Some sys)
    //        sys.Flows  |> iter (fun z -> z.RawParent <- Some sys)
    //        sys.Works  |> iter (fun z -> z.RawParent <- Some sys)

    //        // 하부 구조에 대해서 재귀적으로 호출
    //        sys.Flows |> iter onDeserializedDs
    //        sys.Works |> iter onDeserializedDs

    //    | :? DsFlow as flow ->
    //        ()

    //    | :? DsWork as work ->
    //        work.Calls |> iter (fun z -> z.RawParent <- Some work)
    //        work.Calls |> iter onDeserializedDs
    //        work.DtoArrows
    //        |-> getArrowInfos work.Calls
    //        |-> (fun (guid, src, tgt, dateTime, id) -> ArrowBetweenCalls(guid, src, tgt, dateTime, ?id=id))
    //        |> work.forceSetArrows

    //        work.Arrows |> iter (fun z -> z.RawParent <- Some work)
    //        if work.FlowGuid.NonNullAny() then
    //            work.OptFlowGuid <- Guid.Parse work.FlowGuid |> Some

    //    | :? DsCall as call ->
    //        ()

    //    | _ -> failwith "ERROR.  확장 필요?"


