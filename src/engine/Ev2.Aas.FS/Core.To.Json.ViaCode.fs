namespace rec Dual.Ev2.Aas

(* Core 를 AAS Json/Xml 로 변환하기 위한 몸풀기용 연습 코드 *)

open Dual.Common.Core.FS
open Dual.Ev2


module CoreToJsonViaCode =
    type NamedGuidObject with
        member x.NamedToJsonViaCode(): JObj =
            let jo = JObj()
            jo["name"] <- x.Name
            jo

    type EdgeDTO with
        member x.ToJsonViaCode(): JObj =
            let jo = JObj()
            jo["source"] <- x.Source
            jo["target"] <- x.Target
            jo["type"] <- x.EdgeType.ToString()
            jo

    type DsSystem with
        member x.ToJsonViaCode(): JObj =
            let jo = x.NamedToJsonViaCode()
            jo["type"] <- "System"
            let arr = J.CreateJArr(x.Flows.Map(_.ToJsonViaCode()))
            jo["flows"] <- arr
            jo

    type DsFlow with
        member x.ToJsonViaCode(): JObj =
            let jo = x.NamedToJsonViaCode()
            jo["type"] <- "Flow"
            let vs = J.CreateJArr(x.Vertices.Map(_.ToJsonViaCode()))
            let es = J.CreateJArr(x.Edges.Map(_.ToJsonViaCode()))
            jo["vertices"] <- vs
            jo["edges"] <- es
            jo


    type DsWork with
        member x.ToJsonViaCode(): JObj =
            let jo = x.NamedToJsonViaCode()
            jo["type"] <- "Work"
            let vs = J.CreateJArr(x.Vertices.Map(_.ToJsonViaCode()))
            let es = J.CreateJArr(x.Edges.Map(_.ToJsonViaCode()))
            jo["vertices"] <- vs
            jo["edges"] <- es
            jo

    type DsAction with
        member x.ToJsonViaCode(): JObj =
            let jo = x.NamedToJsonViaCode()
            jo["type"] <- "Action"
            jo


    type DsAutoPre with
        member x.ToJsonViaCode(): JObj =
            let jo = x.NamedToJsonViaCode()
            jo["type"] <- "AutoPre"
            jo

    type DsSafety with
        member x.ToJsonViaCode(): JObj =
            let jo = x.NamedToJsonViaCode()
            jo["type"] <- "Safety"
            jo

    type DsCommand with
        member x.ToJsonViaCode(): JObj =
            let jo = x.NamedToJsonViaCode()
            jo["type"] <- "Command"
            jo

    type DsOperator with
        member x.ToJsonViaCode(): JObj =
            let jo = x.NamedToJsonViaCode()
            jo["type"] <- "Operator"
            jo


    type VertexDetail with
        /// VertexDetail to AAS json
        member x.ToJsonViaCode() =
            match x with
            | Work     y -> y.ToJsonViaCode()
            | Action   y -> y.ToJsonViaCode()
            | AutoPre  y -> y.ToJsonViaCode()
            | Safety   y -> y.ToJsonViaCode()
            | Command  y -> y.ToJsonViaCode()
            | Operator y -> y.ToJsonViaCode()



