namespace Dual.Ev2

open Dual.Common.Base.FS
open Dual.Common.Core.FS
open Newtonsoft.Json
open System

[<AutoOpen>]
module Interfaces =
    /// Graph 의 interface.  subclasses: Flow, Work
    type IGraph = interface end

    type INamedVertex =
        inherit IVertex
        inherit IWithName


    /// 기본 객체 인터페이스
    type IDsObject = interface end

    /// IDsObject와 INamed 를 구현한 인터페이스
    type IDsDsNamedObject =
        inherit IDsObject
        inherit IWithName

    /// 시스템 인터페이스
    type ISystem =
        inherit IDsDsNamedObject
        inherit IGraph

    /// 흐름 인터페이스
    type IFlow =
        inherit IDsDsNamedObject

    /// 작업 인터페이스
    type IWork =
        inherit IDsDsNamedObject
        inherit INamedVertex
        inherit IGraph

    /// 코인 인터페이스
    type ICoin =
        inherit IDsObject
        inherit INamedVertex

    /// 호출 인터페이스
    type ICall =
        inherit ICoin

    /// script 샘플 interface
    type ICalculator =
        abstract member Add: int * int -> int
        abstract member Multiply: int * int -> int

[<AutoOpen>]
module AbstractClasses =
    /// 이름 속성을 가진 추상 클래스
    [<AbstractClass>]
    type NamedObject(name: string) =
        [<JsonProperty(Order = -100)>] member val Name = name with get, set
        interface IWithName

    [<AbstractClass>]
    type GuidObject(?guid:Guid) =
        interface IGuid with
            member x.Guid with get () = x.Guid and set v = x.Guid <- v
        [<JsonProperty(Order = -99)>] member val Guid = guid |?? (fun () -> Guid.NewGuid()) with get, set

    [<AbstractClass>]
    type NamedGuidObject(name: string, ?guid:Guid) =
        inherit GuidObject(?guid=guid)
        [<JsonProperty(Order = -100)>] member val Name = name with get, set
        interface IWithName
