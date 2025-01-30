namespace rec Dual.Ev2.Aas

(* Core 를 AAS Json/Xml 로 변환하기 위한 실제 코드 *)

(*
submodels
	submodel
		category..value
		submodelElements
			property
				category..value
			property
				category..value
			submodelElementCollection
				category..value
				value
					submodelElementCollection
						value
*)

open Dual.Common.Core.FS
open Dual.Ev2
open System.Linq

type JObj = System.Text.Json.Nodes.JsonObject
type JArr = System.Text.Json.Nodes.JsonArray
type JNode = System.Text.Json.Nodes.JsonNode

[<AutoOpen>]
module JsonExtensionModule =
    type Category =
        | PARAMETER
        | CONSTANT
        | VARIABLE
    let createKey(key:string, value:string) = JObj() |> tee(fun jo -> jo[key] <- value)
    let createCategory(value:Category) = JObj() |> tee(fun jo -> jo["category"] <- value.ToString())

    type System.Text.Json.Nodes.JsonNode with
        member x.Set(key:string, value:string): JNode =
            x[key] <- value
            x
        member x.Set(key:string, arr:JArr): JNode =
            x[key] <- arr
            x
        member x.Set(key:string, jobj:JNode): JNode =
            x[key] <- jobj
            x
        member x.Set(key:string, arr:JNode[]): JNode =
            x[key] <- JArr arr//(arr.Cast<JNode>().ToArray())
            x
        //member x.SetN(key:string, value:string) = x.Set(key, value) :> JNode
        //member x.SetN(key:string, arr:JArr) = x.Set(key, arr) :> JNode
        //member x.SetN(key:string, arr:JNode) = x.Set(key, arr) :> JNode
        //member x.SetN(key:string, arr:#JNode[]) = x.Set(key, arr) :> JNode

[<AutoOpen>]
module CoreToAas =
    //type DsNamedObject with
    //    member x.NamedToAas():JNode =
    //        let jo = JObj()
    //        jo["name"] <- x.Name
    //        jo

    type EdgeDTO with
        member x.ToSMC():JNode =
            let source =
                JObj()
                    .Set("cat", CONSTANT.ToString())
                    .Set("idShort", "Source")
                    .Set("id", "some-guid")
                    .Set("kind", "some-kind")
                    .Set("keys",
                        JArr [|
                            JObj().Set("key",
                                JObj()
                                    .Set("type", "Submodel")
                                    .Set("value", "0173-1#01-AHF578#001")) |]  )

            let target  =
                JObj()
                    .Set("category", CONSTANT.ToString())
                    .Set("idShort", "Target")
                    .Set("id", "some-guid")
                    .Set("kind", "some-kind")
                    .Set("keys",
                        JArr [|
                            JObj().Set("key",
                                JObj()
                                    .Set("type", "Submodel")
                                    .Set("value", "0173-1#01-AHF578#001")) |]  )

            let smec =
                JObj()
                    .Set("category", CONSTANT.ToString())
                    .Set("idShort", "Edge")
                    .Set("source", source)
                    .Set("target", target)
                    .Set("type", x.EdgeType.ToString())
            smec

    (*
					<category></category>
					<idShort>Document01</idShort>
					<semanticId>
						<type>ExternalReference</type>
						<keys>
							<key>
								<type>ConceptDescription</type>
								<value>0173-1#02-ABI500#001/0173-1#01-AHF579#001*01</value>
							</key>
						</keys>
					</semanticId>
    *)

    type DsSystem with
        member x.ToSubmodel():JNode =
            //let catId:CatIdBuilder = {
            //    Category="CONSTANT";
            //    IdShort="Identification";
            //    SemanticId(*:SemanticIdBuilder*) = {
            //        Type="ExternalReference"
            //        Keys=[| {
            //                Type="ConceptDescription"
            //                Value="0173-1#02-ABI500#001/0173-1#01-AHF579#001*01" }
            //        |]
            //    }
            //}
            let sm = JObj()
            sm["category"] <- "CONSTANT"
            sm["idShort"] <- "Identification"

            let arr = JArr (x.Flows.Map(_.ToSMC()).ToArray())
            sm["submodelElements"] <- arr
            sm


    type DsFlow with
        member x.ToSMC():JNode =
            let sm = JObj()
            sm
            sm["idShort"] <- "Flow"
            let vs = JArr (x.Vertices.Map(_.ToSMC()).ToArray())
            let es = JArr (x.Edges.Map(_.ToSMC()).ToArray())
            sm["vertices"] <- vs
            sm["edges"] <- es
            sm


    type DsWork with
        member x.ToSMC():JNode =
            let jo = JObj()
            jo["idShort"] <- "Work"
            let vs = JArr (x.Vertices.Map(_.ToSMC()).ToArray())
            let es = JArr (x.Edges.Map(_.ToSMC()).ToArray())
            jo["vertices"] <- vs
            jo["edges"] <- es
            jo

    type DsAction with
        member x.ToSMC():JNode =
            let jo = JObj()
            jo["type"] <- "Action"
            jo


    type DsAutoPre with
        member x.ToSMC():JNode =
            let jo = JObj()
            jo["type"] <- "AutoPre"
            jo

    type DsSafety with
        member x.ToSMC():JNode =
            let jo = JObj()
            jo["type"] <- "Safety"
            jo

    type DsCommand with
        member x.ToSMC():JNode =
            let jo = JObj()
            jo["type"] <- "Command"
            jo

    type DsOperator with
        member x.ToSMC():JNode =
            let jo = JObj()
            jo["type"] <- "Operator"
            jo


    type VertexDetail with
        /// VertexDetail to AAS json
        member x.ToSMC() =
            match x with
            | Work     y -> y.ToSMC()
            | Action   y -> y.ToSMC()
            | AutoPre  y -> y.ToSMC()
            | Safety   y -> y.ToSMC()
            | Command  y -> y.ToSMC()
            | Operator y -> y.ToSMC()



