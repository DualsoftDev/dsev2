namespace rec Dual.Ev2.Aas

(* AAS Json/Xml 로부터 Core 를 생성하기 위한 코드 *)

open Dual.Common.Core.FS
open Dual.Ev2
open System.Linq
open System

[<AutoOpen>]

module CoreFromAas =
    type DsSystem with
        [<Obsolete("TODO")>] static member FromAasJsonENV(json:string): DsSystem = getNull<DsSystem>()
        [<Obsolete("TODO")>]
        static member FromAasXmlENV(xml:string): DsSystem =
            //let xxx = Aas.Xmlization.Deserialize(xml)

            getNull<DsSystem>()
    ()

