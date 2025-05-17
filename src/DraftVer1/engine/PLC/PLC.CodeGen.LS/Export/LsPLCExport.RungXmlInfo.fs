namespace PLC.CodeGen.LS

open System.Runtime.CompilerServices
open Dual.Common.Core.FS

[<AutoOpen>]
module internal RungXmlInfoModule =
    /// XmlOutput = string
    type XmlOutput = string

    /// 좌표.  상호 변환: coord, xyOfCoord, xOfCoord, yOfCoord
    type EncodedXYCoordinate = int

    /// [rxi] Rung 구성 기본 요소.  하나의 Rung 자체의 정보
    type RungXmlInfo =
        {
            /// Xgi 출력시 순서 결정하기 위한 coordinate.
            // 대부분 X, Y 로 결정되지만, 세로선은 coord(x, y) + 2 의 좌표를 가져서 (X, Y) tuple 로 구성할 수 없음.
            Coordinate: EncodedXYCoordinate // int
            /// Xml element 문자열
            Xml: XmlOutput // string
            SpanXy: int * int
        } with
            member rxi.SpanX = rxi.SpanXy |> fst
            member rxi.SpanY = rxi.SpanXy |> snd

    /// [bxi] Rung 구성 요소 조합.  하나의 Rung 내의 block 정보
    type BlockXmlInfo =
        {
            /// Block 시작 좌상단 (x, y) 좌표
            Xy: int * int
            /// Block 이 사용하는 가로, 세로 span
            TotalSpanXy: int * int
            /// Block 을 구성하는 element 들의 xml 정보
            RungXmlInfos: RungXmlInfo list
        } with
            member rxi.X = rxi.Xy |> fst
            member rxi.Y = rxi.Xy |> snd
            member rxi.TotalSpanX = rxi.TotalSpanXy |> fst
            member rxi.TotalSpanY = rxi.TotalSpanXy |> snd

    /// [rgi] Rung 을 생성하기 위한 정보
    ///
    /// - Xmls: 생성된 xml string 의 list
    type RungGenerationInfo =
        { Xmls: XmlOutput list // Rung 별 누적 xml.  역순으로 추가.  꺼낼 때 뒤집어야..
          NextRungY: int }

        member me.AddSingleLineXml(xml) = { Xmls = xml :: me.Xmls; NextRungY = me.NextRungY + 1 }

    type XmlSnippet =
    | DuRungXmlInfo of RungXmlInfo
    | DuBlockXmlInfo of BlockXmlInfo
    //| DuRungGenerationInfo of RungGenerationInfo

    /// 좌표 반환 : 1, 4, 7, 11, ...
    /// 논리 좌표 x y 를 LS 산전 XGI 수치 좌표계로 반환
    let coord (x, y) : EncodedXYCoordinate = x * 3 + y * 1024 + 1

    /// coord(x, y) 에서 x, y 좌표 반환
    let xyOfCoord coord =
        let y = (coord - 1) / 1024
        let xx = ((coord - 1) % 1024)
        let x = xx / 3
        let r = xx % 3
        (x, y), r

    /// coord(x, y) 에서 x 좌표 반환
    let xOfCoord : (EncodedXYCoordinate -> int) = xyOfCoord >> fst >> fst
    /// coord(x, y) 에서 y 좌표 반환
    let yOfCoord : (EncodedXYCoordinate -> int) = xyOfCoord >> fst >> snd

    /// RungXmlInfo list 로부터 coordinate 순으로 최종 xml 문자열 생성
    let mergeXmls(xmls:RungXmlInfo seq) : string =
        xmls
        |> Seq.sortBy (fun ri -> ri.Coordinate) // fst
        |> Seq.map (fun ri -> ri.Xml) //snd
        |> String.concat "\r\n"

    /// [rxi]
    let rxiBlockXmlInfoToRungXmlInfo (block:BlockXmlInfo) : RungXmlInfo =
        let bx, by = block.X, block.Y
        let c = coord (bx, by)
        let tx, ty = block.TotalSpanX, block.TotalSpanY
        let xml = block.RungXmlInfos |> mergeXmls
        { Coordinate = c; Xml = xml; SpanXy = (tx, ty) }

    type BlockXmlInfo with
        member x.GetXml():string = mergeXmls x.RungXmlInfos


type internal RungXmlExtension =
    [<Extension>]
    static member MergeXmls(xmls:RungXmlInfo seq) : string = mergeXmls xmls

    /// RungXmlInfo list 로부터 최종 xml 문자열 생성 및 다음 Y 좌표계산을 위한 spanY 반환
    [<Extension>]
    static member MergeXmls(xmls:XmlSnippet seq) : XmlOutput * int =
        let rxis:RungXmlInfo seq =
            xmls
            |> map (function
                | DuRungXmlInfo rxi -> rxi
                | DuBlockXmlInfo bxi -> rxiBlockXmlInfoToRungXmlInfo bxi)

        let spanY = rxis |> Seq.map(fun rxi -> rxi.SpanY) |> Seq.max
        let xml = mergeXmls rxis
        xml, spanY



