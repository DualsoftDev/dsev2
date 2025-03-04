namespace PLC.CodeGen.LS

open System.Xml
open System.Text.RegularExpressions

open Dual.Common.Core.FS
open PLC.CodeGen.LS
open PLC.CodeGen.Common
open Engine.Core
open System.Collections.Generic

[<AutoOpen>]
module XgiXmlProjectAnalyzerModule =
    let xmlSymbolNodeToSymbolInfo (xnSymbol: XmlNode) : SymbolInfo =
        let dic = xnSymbol.GetAttributes()

        {   defaultSymbolInfo with
                Name = dic["Name"]
                Comment = dic["Comment"]
                Address   = dic.TryFindValue("Address")   |> Option.toString
                Device    = dic.TryFindValue("Device")    |> Option.toString
                DevicePos = dic.TryFindValue("DevicePos") |> Option.bind Parse.TryInt |> Option.defaultValue(-1)
                Kind      = dic.TryFindValue("Kind")      |> Option.bind Parse.TryInt |> Option.defaultValue(-1)
        }

    let collectByteIndices target (addresses: string seq) : int list =
        [ for addr in addresses do
              match addr with
              | RegexPattern @"^%M([XBWDL])(\d+)$" [ m; Int32Pattern index ] ->
                  match m with
                  | "X" -> index / 8
                  | ("B" | "W" | "D" | "L") ->
                      let byteSize = getByteSizeFromPrefix m target
                      let s = index * byteSize
                      let e = s + byteSize - 1
                      yield! [ s..e ]
                  | _ -> failwithlog "ERROR"
              | _ -> failwithlog "ERROR" ]
        |> sort
        |> distinct

    let internal collectGlobalSymbols (xdoc: XmlDocument) =
        xdoc.SelectMultipleNodes "//Configurations/Configuration/GlobalVariables/GlobalVariable/Symbols/Symbol"
        |> map xmlSymbolNodeToSymbolInfo
        |> List.ofSeq

    let collectAllSymbols (xdoc: XmlDocument) =
        xdoc.SelectMultipleNodes "//Configurations/Configuration//Symbols/Symbol"
        |> map xmlSymbolNodeToSymbolInfo
        |> List.ofSeq

    let collectGlobalSymbolNames (xdoc: XmlDocument) = collectGlobalSymbols xdoc |> map name

    /// Prefix 로 시작하는 global 변수들의 주소를 반환
    let private collectGlobalVariableAddresses (xdoc: XmlDocument) (prefix:string) : string list =
        collectGlobalSymbols xdoc
        |> map address
        |> filter notNullAny
        |> filter (fun addr -> addr.StartsWith(prefix))

    let collectUsedMermoryByteIndicesInGlobalSymbols (xdoc: XmlDocument) xgx : int list =
        collectGlobalVariableAddresses xdoc "%M"
        |> collectByteIndices xgx


    /// Counter/Timer 등의 주소에서 int 값을 추출
    /// "C0001" -> 1
    /// "T0003" -> 3
    let private extractNumber (address:string) =
        let fail() = failwith $"Failed to parse address: {address}"
        let pattern = @"\d+"
        let regexMatch = Regex.Match(address, pattern)
        if regexMatch.Success then
            regexMatch.Value |> System.Int32.TryParse
            |> function
                | (true, number) -> number
                | _ -> fail()
        else
            fail()

    /// XGK XG5000 XML 문서에서 global 변수 Counter 에 사용된 주소("C0001" -> 1)들을 추출
    let collectCounterAddressesXgk (xdoc: XmlDocument) : int list =
        collectGlobalVariableAddresses xdoc "C" |> map extractNumber

    /// XGK XG5000 XML 문서에서 global 변수 Timer 에 사용된 주소("T0001" -> 1)들을 추출
    let collectTimerAddressesXgk (xdoc: XmlDocument) : int list =
        collectGlobalVariableAddresses xdoc "T" |> map extractNumber

    /// XGK XG5000 XML 문서에서 설정 관련 파라미터들을 추출해서 dictionary 로 반환
    ///
    /// XGK 의 Timer resoultion 관련 설정을 처리하기 위함
    let collectXgkBasicParameters (xdoc: XmlDocument) : Dictionary<string, int> =
        xdoc.GetXmlNode("//Configurations/Configuration/Parameters/Parameter/XGTBasicParam").GetAttributes()
        |> map (fun (KeyValue(k, v)) ->
            match Parse.TryInt v with
            | Some v -> Some (k, v)
            | None -> None)
        |> choose id
        |> Tuple.toDictionary