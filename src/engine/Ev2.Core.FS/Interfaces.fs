namespace Dual.Ev2

open Dual.Common.Base.FS
open Dual.Common.Core.FS
open Newtonsoft.Json
open System

[<AutoOpen>]
module Interfaces =
    /// 내부에 IContainee 형식의 다른 요소를 포함할 수 있는 parent 역할을 수행
    type IContainer = interface end

    /// Graph 의 interface.  subclasses: Flow, Work
    type IGraph = interface end

    /// IContainer에 포함될 수 있는 요소의 인터페이스. child 역할을 수행
    type IContainee = interface end

    /// IContainer와 IContainee 역할을 모두 수행하는 인터페이스
    type IContain =
        inherit IContainer
        inherit IContainee


    type INamedVertex =
        inherit IVertex
        inherit INamed


    /// 기본 객체 인터페이스
    type IDsObject = interface end

    /// IDsObject와 INamed를 구현한 인터페이스
    type IDsDsNamedObject =
        inherit IDsObject
        inherit INamed

    /// 시스템 인터페이스
    type ISystem =
        inherit IDsDsNamedObject
        inherit IContainer

    /// 흐름 인터페이스
    type IFlow =
        inherit IDsDsNamedObject
        inherit IContain
        inherit IGraph

    /// 작업 인터페이스
    type IWork =
        inherit IDsDsNamedObject
        inherit IContain
        inherit INamedVertex
        inherit IGraph

    /// 코인 인터페이스
    type ICoin =
        inherit IDsObject
        inherit INamedVertex
        inherit IContainee

    /// 호출 인터페이스
    type ICall =
        inherit ICoin


[<AutoOpen>]
module AbstractClasses =
    /// 이름 속성을 가진 추상 클래스
    [<AbstractClass>]
    type NamedObject(name: string) =
        [<JsonProperty(Order = -100)>] member val Name = name with get, set
        interface INamed with
            member x.Name with get() = x.Name and set(v) = x.Name <- v

    [<AbstractClass>]
    type GuidObject(?guid:Guid) =
        interface IGuid with
            member x.Guid with get () = x.Guid and set v = x.Guid <- v
        [<JsonProperty(Order = -99)>] member val Guid = guid |?? (fun () -> Guid.NewGuid()) with get, set

    [<AbstractClass>]
    type NamedGuidObject(name: string, ?guid:Guid) =
        inherit GuidObject(?guid=guid)
        [<JsonProperty(Order = -100)>] member val Name = name with get, set
        interface INamed with
            member x.Name with get() = x.Name and set(v) = x.Name <- v
