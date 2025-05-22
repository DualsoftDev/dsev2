namespace Ev2.Core.FS

open Dual.Common.Base
open Dual.Common.Core.FS

[<AutoOpen>]
module Ds2JsonModule =

    type DsProject with
        member x.ToJson():string = EmJson.ToJson(x)
        static member FromJson(json:string): DsProject = EmJson.FromJson<DsProject>(json)


    //type DsSystem with
    //    member x.ToJson():string = EmJson.ToJson(x)
    //    static member FromJson(json:string): DsSystem = EmJson.FromJson<DsSystem>(json)



    let private arrowToDto (a:IArrow) =
        match a with
        | :? ArrowBetweenCalls as a -> DtoArrow(a.Guid, a.Id, a.Source.Guid, a.Target.Guid, a.DateTime)
        | :? ArrowBetweenWorks as a -> DtoArrow(a.Guid, a.Id, a.Source.Guid, a.Target.Guid, a.DateTime)

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
        | :? Unique as uniq ->
            uniq.DbId <- uniq.Id |> Option.toNullable
        | _ -> ()

        match dsObj with
        | :? DsProject as proj -> proj.Systems |> iter onSerializing
        | :? DsSystem as sys ->
            sys.DtoArrows <- sys.Arrows |-> arrowToDto
            sys.Flows |> iter onSerializing
            sys.Works |> iter onSerializing
        | :? DsFlow as flow ->
            //flow.DtoArrows <- flow.Arrows |-> arrowToDto
            ()
        | :? DsWork as work ->
            work.DtoArrows <- work.Arrows |-> arrowToDto
            work.Calls |> iter onSerializing
        | :? DsCall as call ->
            ()
        | _ -> failwith "ERROR.  확장 필요?"

    /// JSON 읽고 나서 메모리 구조에 후처리 작업
    let rec internal onDeserialized (dsObj:IDsObject) =
        match dsObj with
        | :? Unique as uniq ->
            uniq.Id <- uniq.DbId |> Option.ofNullable
        | _ -> ()

        match dsObj with
        | :? DsProject as proj ->
            proj.ActiveSystems |> iter onDeserialized
            proj.Systems |> iter (fun z -> z.RawParent <- Some proj)

        | :? DsSystem as sys ->
            sys.Arrows <-
                sys.DtoArrows
                |-> getArrowInfos sys.Works
                |-> (fun (guid, src, tgt, dateTime, id) -> ArrowBetweenWorks(guid, src, tgt, dateTime, ?id=id))

            // flows, works 의 Parent 를 this(system) 으로 설정
            sys.Flows |> iter (fun z -> z.RawParent <- Some sys)
            sys.Works |> iter (fun z -> z.RawParent <- Some sys)

            // flow 가 가진 WorksGuids 에 해당하는 work 들을 모아서 flow.Works 에 instance collection 으로 저장
            for f in sys.Flows do
                let fWorks = sys.Works |> filter (fun w -> f.WorksGuids |> Seq.contains w.Guid) |> toArray
                for w in fWorks do
                    w.OptFlowGuid <- Some f.Guid

                f.forceSetWorks fWorks

            // 하부 구조에 대해서 재귀적으로 호출
            sys.Flows |> iter onDeserialized
            sys.Works |> iter onDeserialized

        | :? DsFlow as flow ->
            //flow.Arrows <-
            //    flow.DtoArrows
            //    |-> getArrowInfos flow.Works
            //    |-> (fun (guid, src, tgt, dateTime, id) -> ArrowBetweenWorks(guid, src, tgt, dateTime, ?id=id))
            ()
        | :? DsWork as work ->
            work.Calls |> iter (fun z -> z.RawParent <- Some work)
            work.Arrows <-
                work.DtoArrows
                |-> getArrowInfos work.Calls
                |-> (fun (guid, src, tgt, dateTime, id) -> ArrowBetweenCalls(guid, src, tgt, dateTime, ?id=id))
        | _ -> failwith "ERROR.  확장 필요?"


