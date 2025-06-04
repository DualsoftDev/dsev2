namespace rec Dual.Ev2.Aas

(* AAS Json/Xml 로부터 Core 를 생성하기 위한 코드 *)

open Dual.Common.Core.FS
open Dual.Ev2
open System.Linq
open System
open Dual.Common.Base
open Ev2.Core.FS



[<AutoOpen>]
module CoreFromAas =
    type Environment = AasCore.Aas3_0.Environment
    type ISubmodel = AasCore.Aas3_0.ISubmodel

    type NjSystem with
        static member FromAasJsonENV(json:string): NjSystem =
            let env = J.CreateIClassFromJson<Environment>(json)
            let sm = env.Submodels.First()
            NjSystem.FromISubmodel(sm)

        static member FromAasXmlENV(xml:string): NjSystem =
            let sm = J.CreateIClassFromXml<Environment>(xml).Submodels.First()
            NjSystem.FromISubmodel(sm)

        [<Obsolete("TODO: ISubmodel 에서 NjSystem 구축 코드 작성")>]
        static member FromISubmodel(submodel:ISubmodel): NjSystem =
            assert(submodel.IdShort = "System")
            //submodel.SubmodelElements[0].
            //let name = submodel.IdShort
            getNull<NjSystem>()
    ()

