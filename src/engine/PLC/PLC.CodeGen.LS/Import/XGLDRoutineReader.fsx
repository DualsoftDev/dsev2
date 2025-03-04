namespace PLC.CodeGen.LS

open System.Xml
open System.Collections.Generic
open System

open Dual.Common.Core.FS
open AddressConvert

[<AutoOpen>]
module XGLDRoutineReader =

    type XmlRung =
        { RungID: int
          RungLens: seq<string>
          Elements: seq<XmlRungElement>
          Program: string }

    and XmlRungElement =
        { ElementType: int
          Coordinate: int
          Param: string
          Tag: string
          Address: string
          AddressType: string }

    /// XML에 존재하는 모든 Rung의 정보를 Element 구조 포함하여 추출한다.
    let getRungs (xdoc: XmlDocument, cpuSerise: CpuType, dicMaxDevice: IDictionary<string, int>) =

        let getElements (xmlNode: XmlNode) =
            xmlNode.SelectNodes("Element")
            |> XmlExt.ToEnumerables
            |> Seq.map (fun e ->
                let na =
                    if (e.FirstChild <> null) then
                        e.FirstChild.InnerText
                    else
                        ""

                let el = e.Attributes.["ElementType"].InnerText |> int
                let co = e.Attributes.["Coordinate"].InnerText |> int

                let pa =
                    if (e.Attributes.ItemOf("Param") <> null) then
                        e.Attributes.["Param"].InnerText
                    else
                        ""

                let iec, addressType =
                    if (na = "" || na.StartsWith("_") || (Char.IsNumber(na.ToCharArray().[0]))) then
                        "", ""
                    else
                        match AddressConvert.tryParseTag (cpuSerise) na with
                        | Some v -> v.Tag, v.DataType.Totext()
                        | None -> "", ""


                { ElementType = el
                  Coordinate = co
                  Param = pa
                  Tag = na
                  Address = iec
                  AddressType = addressType })

        let getLens (elements: seq<XmlRungElement>) =
            let startY = elements |> Seq.head |> (fun f -> (f.Coordinate - 1) / 1024)

            elements
            |> Seq.map (fun e ->
                let tagMarking = if (e.Tag <> "") then "0T0" else "0N0" //Tag 위치 규격은 존재하면 0T0, 없으면 0N0 숫자문자조합으로 마킹
                let posMarking = (e.Coordinate - 1) - (1024 * startY)
                (sprintf "%s‡%d‡%d" tagMarking posMarking e.ElementType))

        let mutable cntRung = -1

        let rungs =
            xdoc.SelectNodes(xmlCnfPath + "/POU/Programs/Program")
            |> XmlExt.ToEnumerables
            |> Seq.collect (fun xmlProgram ->
                let nameProgram = xmlProgram.FirstChild.InnerText

                xmlProgram.SelectNodes("Body/LDRoutine/Rung")
                |> XmlExt.ToEnumerables
                |> Seq.filter (fun f -> f.ChildNodes.Count <> 0)
                |> Seq.map (fun xmlRung ->
                    cntRung <- cntRung + 1
                    let elements = getElements (xmlRung)
                    let rungLens = getLens (elements)

                    { RungID = cntRung
                      RungLens = rungLens
                      Program = nameProgram
                      Elements = elements }))


        rungs
