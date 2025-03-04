namespace PLC.CodeGen.LS

open Dual.Common.Core.FS
open PLC.CodeGen.LS.Config.POU.Program.LDRoutine
open FB
open Engine.Core

[<AutoOpen>]
module internal Common =
    let systemOnRising = fRising([Expression.True]) :?> IExpression<bool>

    let bxiRungXmlInfosToBlockXmlInfo (rungXmlInfos: RungXmlInfo list) : BlockXmlInfo =
        let xs   = rungXmlInfos
        let xys  = xs  |> List.map (fun e -> xyOfCoord e.Coordinate |> fst)
        let minX = xys |> List.map fst |> List.min
        let minY = xys |> List.map snd |> List.min
        let maxX = xs  |> List.map (fun e -> xOfCoord e.Coordinate + e.SpanX) |> List.max
        let maxY = xs  |> List.map (fun e -> yOfCoord e.Coordinate + e.SpanY) |> List.max

        let totalSpanX = maxX - minX
        let totalSpanY = maxY - minY

        {   Xy = (minX, minY)
            TotalSpanXy = (totalSpanX, totalSpanY)
            RungXmlInfos = xs }

    let dq = "\""



    /// 산전 limit : contact 기준 가로로 최대 31개[0..30] + coil 1개[31]
    [<Literal>]
    let maxNumHorizontalContact = 31
    /// 산전 limit : contact 기준 가로로 최대 31개[0..30] + coil 1개[31]
    [<Literal>]
    let coilCellX = maxNumHorizontalContact
    /// 최소기본 FB 위치 : 가로로  9 포인트
    [<Literal>]
    let minFBCellX = 9

    /// rung 을 구성하는 element (접점)의 XML 표현 문자열 반환
    let elementFull (elementType: int) (coordi: int) (param: string) (tag: string) : XmlOutput =
        $"\t\t<Element ElementType={dq}{elementType}{dq} Coordinate={dq}{coordi}{dq} {param}>{tag}</Element>"

    /// rung 을 구성하는 element (접점)의 XML 표현 문자열 반환
    let elementBody elementType coordi tag = elementFull elementType coordi "" tag
    /// rung 을 구성하는 element (접점)의 XML 표현 문자열 반환
    let element elementType coordi = elementBody elementType coordi ""

    /// 좌표 c 에서 시작하는 수직 line
    let vline c =
        element (int ElementType.VertLineMode) c

    let pointAt (elementType: ElementType) (tag: string) (x: int, y: int) : XmlOutput =
        let xx = x * 3 + 1
        let yy = y * 1024
        let coordi = xx + yy
        assert (coordi = coord (x, y))

        let nElementType = int elementType

        (* see elementFull
            /// rung 을 구성하는 element (접점)의 XML 표현 문자열 반환
            let elementFull elementType coordi param tag : XmlOutput =
                $"\t\t<Element ElementType={dq}{elementType}{dq} Coordinate={dq}{coordi}{dq} {param}>{tag}</Element>"
        *)

        elementFull nElementType coordi "" tag

    /// x, y 위치에 contact 생성하기 위한 xml 문자열 반환
    let contactAt (tag: string) (x: int, y: int) = pointAt ElementType.ContactMode tag (x, y)

    /// y line 에 coil 생성하기 위한 xml 문자열 반환
    let coilAt (tag: string) (y: int) =
        pointAt ElementType.CoilMode tag (coilCellX, y) // coilCellX = 31

    let xgkFBAt (fbParam:string) (x: int, y: int) =
        let c = coord (x, y)
        elementFull (int ElementType.FBMode) c fbParam ""

    module Unused =
        /// 조건이 9 이상이면 뒤로 증가
        let getFBCellX x : int =
            if minFBCellX <= x + 3 then (x + 4) else minFBCellX

        /// 좌표 c 에서 시작하는 양 방향 검출 line
        let risingline c =
            elementFull (int ElementType.RisingContact) (c) "" ""

        /// 좌표 c 에서 시작하는 음 방향 검출 line
        let fallingline c =
            elementFull (int ElementType.FallingContact) (c) "" ""

        let hlineEmpty c =
            element (int ElementType.HorzLineMode) c

        let hline c =
            element (int ElementType.MultiHorzLineMode) c


    let hlineStartMarkAt (x, y) =
        elementFull (int ElementType.HorzLineMode) (coord (x, y)) "" ""

    /// [rxi] debugging 용 xml comment 생성
    let rxiCommentAtCoordinate (c: EncodedXYCoordinate) (comment: string) =
        { Coordinate = c
          Xml = $"<!-- {comment} -->"
          SpanXy = (maxNumHorizontalContact, 1) }

    /// [rxi] debugging 용 xml comment 생성
    let rxiCommentAt (x, y) comment =
        rxiCommentAtCoordinate (coord (x, y)) comment


    /// 마지막 수평으로 연결 정보: 그릴 수 없으면 [], 그릴 수 있으면 [singleton]
    let tryHlineTo (x, y) endX : XmlOutput list =
        if endX < x then
            []
        else
            let lengthParam = $"Param={dq}{3 * (endX - x)}{dq}"
            let c = coord (x, y)
            [ elementFull (int ElementType.MultiHorzLineMode) c lengthParam "" ]

    let hlineTo (x, y) endX : XmlOutput =
        if endX < x then
            failwithlog $"endX startX [{endX} > {x}]"

        tryHlineTo (x, y) endX |> List.exactlyOne

    /// [rxi] x y 위치에서 수직선 한개를 긋는다
    let rxiVLineAt (x, y) : RungXmlInfo =
        verify (x >= 0)
        let c = coord (x, y) + 2
        { Coordinate = c; Xml = vline c; SpanXy = (0, 1) }

    let mutable EnableXmlComment = false

    /// [rxi] x y 위치에서 수직으로 n 개의 line 을 긋는다
    let rxisVLineDownN (x, y) n : RungXmlInfo list =
        [ if EnableXmlComment then
              rxiCommentAt (x, y) $"vlineDownN ({x}, {y}) {n}"

          if n > 0 then
              for i in [ 0 .. n - 1 ] do
                  rxiVLineAt (x, y + i) ]

    let rxisVLineUpN (x, y) n = rxisVLineDownN (x, y - n) n

    /// [rxi] x y 위치에서 수직으로 endY 까지 line 을 긋는다
    let rxisVLineDownTo (x, y) endY = rxisVLineDownN (x, y) (endY - y)
    let rxisVLineUpTo (x, y) endY = rxisVLineUpN (x, y) (y - endY)

    /// xml 문자열을 <Rung> 으로 감싸기
    let wrapWithRung xml = $"\t<Rung BlockMask={dq}0{dq}>\r\n{xml}\t</Rung>"

    /// 함수 그리기 (detailedFunctionName = 'ADD2_INT', briefFunctionName = 'ADD')
    let rxiFunctionAt (detailedFunctionName, briefFunctionName) (inst: string) (x, y) : RungXmlInfo =
        let tag = briefFunctionName
        let instFB = if inst = "" then "," else (inst + ",VAR")
        let c = coord (x, y)

        let param =
            FB.getFBXmlParam (detailedFunctionName, briefFunctionName) instFB (FB.getFBIndex tag)

        let fbBody = $"Param={dq}{param}{dq}"
        let xml = elementFull (int ElementType.VertFBMode) c fbBody inst

        { Coordinate = c
          Xml = xml
          SpanXy = (3, getFunctionHeight detailedFunctionName) }

    /// 함수 파라메터 그리기
    let rxiFBParameter (x, y) tag : RungXmlInfo =
        let c = coord (x, y)
        let xml = elementFull (int ElementType.VariableMode) c "" tag

        { Coordinate = c
          Xml = xml
          SpanXy = (1, 1) }

//let drawRising (x, y) =
//    let cellX = getFBCellX x
//    let c = coord (cellX, y)
//    [   { Coordinate = c; Xml = risingline c; SpanX = 0; SpanY = 0 }
//        { Coordinate = c; Xml = hLineTo (x, y) (cellX-1); SpanX = (cellX-1); SpanY = 0 }
//    ]

//let drawPulseCoil (x, y) (tagCoil:INamedExpressionizableTerminal) (funSize:int) =
//    let newX = getFBCellX (x-1)
//    let newY = y + funSize
//    [
//        { Coordinate = coord(x, y); Xml = risingline (coord(x, y)); SpanX = 0; SpanY = 1 }
//        let xml = hLineTo (x, newY) (newX - 1)
//        { Coordinate = coord(newX, newY); Xml = xml; SpanX = (newX - 1); SpanY = 1 }

//        let xml = elementBody (int ElementType.CoilMode) (coord(coilCellX, newY)) (tagCoil.StorageName)
//        { Coordinate = coord(coilCellX, newY); Xml = xml; SpanX = 0; SpanY = 0 }
//        yield! vlineDownTo (x-1, y) funSize
//    ]
