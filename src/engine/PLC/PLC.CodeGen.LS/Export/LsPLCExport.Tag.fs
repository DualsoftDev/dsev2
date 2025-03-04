namespace PLC.CodeGen.LS


open Dual.Common.Core.FS
open Engine.Core
open System.Linq

// IEC-61131 Addressing
// http://www.microshadow.com/ladderdip/html/basic_iec_addressing.htm
// https://deltamotion.com/support/webhelp/rmctools/Registers/Address_Formats/Addressing_IEC.htm
(*
% [Q | I ] [X] [file] . [element] . [bit]
% [Q | I ] [B|W|D] [file] . [element]
% MX [file] . [element] . [bit]
% M [B|W|D] [file] . [element]
*)


[<AutoOpen>]
module XGITag = //IEC61131Tag =
    type FE(f, e) =
        member x.File = f
        member x.Element = e

    type FEB(f, e, b) =
        inherit FE(f, e)
        member x.Bit = b

    //let analyzeAddress tag =



    ///// memory type(I/O/M) 에 따른 연속적인  device 를 생성하는 함수를 반환한다.
    //let AddressGenerator
    //    (memType:string)
    //    (nBaseBit:int, nMaxBit:int)
    //    (nBaseByte:int, nMaxByte:int)
    //    (nBaseWord:int, nMaxWord:int) (alreadyAllocatedAddresses:Set<string>)
    //  =
    //    let mutable startBit = nBaseBit
    //    let mutable startByte = nBaseByte
    //    let mutable startWord = nBaseWord
    //    let rec generate() =
    //        //let x = startBit % 16
    //        //let n = startBit / 16
    //        let errMsg = $"Device generator for {memType} bit exceeds max limit!"

    //        if startBit >= nBaseBit + nMaxBit
    //            || startByte >= nBaseByte + nMaxByte
    //            || startWord >= nBaseWord + nMaxWord
    //        then
    //            failwithlog errMsg

    //        /// I,O 주소 생성은 임시적임
    //        let address =
    //            match memType with
    //            | "I" -> sprintf "%%%sX%d.%d.%d" memType (startBit/16/64) (startBit/16) (startBit%16)
    //            | "O" -> sprintf "%%%sX%d.%d.%d" "Q" (startBit/16/64) (startBit/16) (startBit%16)
    //            | "M" -> sprintf "%%%sX%d" memType startBit
    //            | "IB" -> sprintf "%%%s%d.%d" memType (startByte/64) (startByte%64)
    //            | "OB" -> sprintf "%%%s%d.%d" "QB" (startByte/64) (startByte%64)
    //            | "MB" -> sprintf "%%%s%d" memType startByte
    //            | "IW" -> sprintf "%%%s%d.%d" memType (startWord/64) (startWord%64)
    //            | "OW" -> sprintf "%%%s%d.%d" "QW" (startWord/64) (startWord%64)
    //            | "MW" -> sprintf "%%%s%d" memType startWord
    //            | "ID" -> sprintf "%%%s%d.%d" memType (startWord/64) (startWord%64)
    //            | "OD" -> sprintf "%%%s%d.%d" "QD" (startWord/64) (startWord%64)
    //            | "MD" -> sprintf "%%%s%d" memType startWord
    //            | _ ->  failwithlog "Unknown memType:" + memType


    //        match memType with
    //        | "I"  | "O"  | "M"  -> startBit  <- startBit  + 1
    //        | "IB" | "OB" | "MB" -> startByte <- startByte + 1
    //        | "IW" | "OW" | "MW" -> startWord <- startWord + 1
    //        | "ID" | "OD" | "MD" -> startWord <- startWord + 2
    //        | _ ->  failwithlog "Unknown  %s memType:" memType

    //        if (alreadyAllocatedAddresses.Contains(address)) then
    //            Debug.WriteLine $"Adress {address} already in use. Tring to choose other address.."
    //            generate()
    //        else
    //            address

    //    generate


    /// name, comment, plcType, kind 를 받아서 SymbolInfo 를 생성한다.
    let createSymbolInfo name comment plcType kind (initValue: BoxedObjectHolder) =
        {   defaultSymbolInfo with
                Name = name
                Comment = escapeXml comment
                Type = plcType
                Kind = kind
                InitValue = initValue.Object }

    let copyLocal2GlobalSymbol (s: SymbolInfo) =
        {   s with
                Kind = int Variable.Kind.VAR_GLOBAL
                State = 0 }

    type SymbolInfo with

        member private x.ToXgiLiteral() =
            match x.Type with
            | "BOOL" ->
                match x.InitValue :?> bool with
                | true -> "true"
                | false -> "false"
            | _ -> $"{x.InitValue}"

        /// Symbol 관련 XML tag attributes 생성
        member private x.GetXmlArgs (prjParam: XgxProjectParams) =
            let targetType = prjParam.TargetType
            [
                match targetType with
                | XGI ->
                    $"Name=\"{x.Name}\""
                    $"Comment=\"{escapeXml x.Comment}\""
                    $"Device=\"{x.Device}\""
                    $"Kind=\"{x.Kind}\""
                    if x.Kind <> int Variable.Kind.VAR_EXTERNAL then
                        $"Type=\"{x.Type}\""

                        if x.InitValue <> null then
                            $"InitValue=\"{x.ToXgiLiteral()}\""

                        $"Address=\"{x.Address}\""
                    $"State=\"{x.State}\""
                | XGK ->
                    if x.DevicePos = - 1 then
                        failwithf $"Invalid DevicePos for {x.Name}({x.Address})"
                    // <Symbol Name="autoMonitor" Device="P" DevicePos="0" Type="BIT" Comment="" ModuleInfo="" EIP="0" HMI="0"></Symbol>
                    let typ =
                        match x.Type with
                        | ("BIT"|"BOOL") -> "BIT"
                        | ("SINT" | "INT" | "DINT"
                            |"USINT" | "UINT" | "UDINT"
                            |"WORD") -> "WORD"
                        | "TON"| "TOF"| "TMR"  -> "BIT/WORD"
                        | ("CTU"| "CTD"| "CTUD"| "CTR" | "CTD_INT" | "CTU_INT" | "CTUD_INT" ) -> "BIT/WORD"

                        | ("BYTE" | "STRING" | "REAL" | "LREAL" ) -> "WORD"        // xxx 이거 맞나??

                        | ("LINT" | "ULINT" | _ ) ->
                            failwithlog $"Not supported data type {x.Type}"

                    if x.IsDirectAddress then //주소 별칭이 있으면 이름 생성하지 않고 직접변수 스타일로 사용 (실제 이름은 Comment에 저장)
                        $"Name=\"\""
                        let aliasNames = x.AddressAlias.JoinWith(", ")
                        $"Comment=\"{escapeXml x.Comment}//Alias List: {aliasNames}\""
                    else
                        if prjParam.EnableXmlComment then
                            $"Name=\"{x.Name}\"";$"Comment=\"{escapeXml x.Comment}\""
                        else
                            $"Name=\"{x.Name}\""

                    $"Device=\"{x.Device}\""
                    $"DevicePos=\"{x.DevicePos}\""
                    $"Type=\"{typ}\""
                | _ -> failwithlog "Not supported plc type"
            ] |> String.concat " "




        //Address="" Trigger="" InitValue="" Comment="" Device="" DevicePos="-1" TotalSize="0" OrderIndex="0" HMI="0" EIP="0" SturctureArrayOffset="0" ModuleInfo="" ArrayPointer="0"><MemberAddresses></MemberAddresses>
        member x.GenerateXml (prjParam: XgxProjectParams) =
            [
                let xml = x.GetXmlArgs prjParam
                yield $"\t<Symbol {xml}>"

                //// 사용되지 않지만, 필요한 XML children element 생성
                //yield!
                //    [ "Addresses"; "Retains"; "InitValues"; "Comments" ]
                //    |> Seq.map (sprintf "\t\t<Member%s/>")
                yield "\t</Symbol>" ]
            |> String.concat "\r\n"

        member x.GenerateDirectAddressXml () =
            [
                $"\t<DirectVar  "
                $"Device=\"{x.Address}\""
                $"Name=\"\""
                let aliasNames = x.AddressAlias.JoinWith(", ")
                $"Comment=\"{escapeXml x.Comment}//Alias List: {aliasNames}\""
                ">\t</DirectVar>"
            ]|> String.concat " "

    /// Symbol variable 정의 구역 xml 의 string 을 생성
    let private generateSymbolVarDefinitionXml (prjParam: XgxProjectParams) (varType: string) (FList(symbolInfos: SymbolInfo list)) =
        let symbols:SymbolInfo list =
            symbolInfos
            |> List.filter (fun s -> not(s.Name.Contains(xgkTimerCounterContactMarking)))
            |> List.filter (fun s -> not(s.IsDirectAddress))
            |> List.sortBy (fun s -> s.Name)

        let directSymbols =
            symbolInfos
            |> Seq.filter (fun s -> s.IsDirectAddress)
            |> Seq.distinctBy (fun s -> s.Address)
            |> Seq.toList
        let xmls =
            [   yield $"<{varType} Version=\"Ver 1.0\" Count={dq}{symbols.Length}{dq}>"
                yield "<Symbols>"
                let symbols = //xgk는 Symbols 규격에 directSymbols 을 넣는다.  xgi는 DirectVarComment 별도 구역에 생성
                    if prjParam.TargetType = XGK && varType = "GlobalVariable" then  //LocalVar 에는 DirectVarComment 없음
                        symbols@directSymbols
                    else
                        symbols

                yield! symbols |> map (fun s -> s.GenerateXml prjParam)
                yield "</Symbols>"

                yield "<TempVar Count=\"0\"></TempVar>"

                if prjParam.TargetType = XGI && varType = "GlobalVariable" && directSymbols.Length > 0 then
                    yield $"<DirectVarComment Count=\"{directSymbols.Length}\">"
                    yield! directSymbols |> map (fun s -> s.GenerateDirectAddressXml())
                    yield "</DirectVarComment>"

                yield $"</{varType}>"
            ]
        xmls |> String.concat "\r\n"

    let generateLocalSymbolsXml (prjParam: XgxProjectParams) symbols =
        generateSymbolVarDefinitionXml prjParam "LocalVar" symbols

    let generateGlobalSymbolsXml (prjParam: XgxProjectParams) symbols =
        generateSymbolVarDefinitionXml prjParam "GlobalVariable" symbols
