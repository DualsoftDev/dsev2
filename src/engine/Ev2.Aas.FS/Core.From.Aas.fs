namespace rec Dual.Ev2.Aas

(* AAS Json/Xml 로부터 Core 를 생성하기 위한 코드 *)

open Dual.Common.Core.FS
open Dual.Ev2
open System.Linq
open System



[<AutoOpen>]
module CoreFromAas =
    type Environment = AasCore.Aas3_0.Environment
    type ISubmodel = AasCore.Aas3_0.ISubmodel

    type DsSystem with
        static member FromAasJsonENV(json:string): DsSystem =
            let env = J.CreateIClassFromJson<Environment>(json)
            let sm = env.Submodels.First()
            DsSystem.FromISubmodel(sm)

        static member FromAasXmlENV(xml:string): DsSystem =
            let sm = J.CreateIClassFromXml<Environment>(xml).Submodels.First()
            DsSystem.FromISubmodel(sm)

        [<Obsolete("TODO: ????")>]
        static member FromISubmodel(submodel:ISubmodel): DsSystem =
            assert(submodel.IdShort = "System")
            //submodel.SubmodelElements[0].
            //let name = submodel.IdShort
            getNull<DsSystem>()
    ()

