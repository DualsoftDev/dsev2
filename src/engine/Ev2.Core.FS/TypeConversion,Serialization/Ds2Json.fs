namespace Ev2.Core.FS

open Dual.Common.Base
open Dual.Common.Core.FS

/// Ds Object 를 JSON 으로 변환하기 위한 모듈
[<AutoOpen>]
module Ds2JsonModule =

    type DsProject with
        /// DsProject 를 JSON 문자열로 변환
        member x.ToJson():string = EmJson.ToJson(x)
        /// JSON 문자열을 DsProject 로 변환
        static member FromJson(json:string): DsProject = EmJson.FromJson<DsProject>(json)


    //type DsSystem with
    //    member x.ToJson():string = EmJson.ToJson(x)
    //    static member FromJson(json:string): DsSystem = EmJson.FromJson<DsSystem>(json)


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
            ()

        | :? DsWork as work ->
            work.DtoArrows <- work.Arrows |-> arrowToDto |> List.ofSeq
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

            // flows, works, arrows 의 Parent 를 this(system) 으로 설정
            sys.Arrows |> iter (fun z -> z.RawParent <- Some sys)
            sys.Flows  |> iter (fun z -> z.RawParent <- Some sys)
            sys.Works  |> iter (fun z -> z.RawParent <- Some sys)

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
            ()

        | :? DsWork as work ->
            work.Calls |> iter (fun z -> z.RawParent <- Some work)
            work.Calls |> iter onDeserialized
            work.Arrows <-
                work.DtoArrows
                |-> getArrowInfos work.Calls
                |-> (fun (guid, src, tgt, dateTime, id) -> ArrowBetweenCalls(guid, src, tgt, dateTime, ?id=id))

            work.Arrows |> iter (fun z -> z.RawParent <- Some work)

        | :? DsCall as call ->
            ()

        | _ -> failwith "ERROR.  확장 필요?"


