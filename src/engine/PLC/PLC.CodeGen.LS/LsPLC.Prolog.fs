namespace PLC.CodeGen.LS

open System.Text.RegularExpressions
open System.Security
open System.Linq
open Engine.Core
open Dual.Common.Core.FS
open PLC.CodeGen.Common

[<AutoOpen>]
module XgiPrologModule =
    /// XML 특수 문자 escape.  '&' 등
    let escapeXml (xml:string) =
        let removeXmlSpecialChars (input: string) : string =
            let pattern = "&amp;|&lt;|&gt;|&quot;|&apos;"
            Regex.Replace(input, pattern, "")
        let containsXmlSpecialChars (input: string) : bool =
            let removed = removeXmlSpecialChars input
            //Regex.IsMatch(removed, @"[]<>&'")
            Regex.IsMatch(removed, "[<>&\"']")

        if (xml.NonNullAny() && containsXmlSpecialChars xml) then
            SecurityElement.Escape xml
        else
            xml

    let validateVariableName (name:string) (targetType:PlatformTarget) =
        let n = name.ToUpper()
        if targetType = XGI && n[0].IsOneOf('N', 'M', 'R') && Regex.IsMatch(n.Substring(1), @"^([XBWDL]?)(\d+)$") then
            // @"^([NMR][XBWDL]?)(\d+)$"
            Error $"'{n}' is not valid symbol name.  (Can't use direct variable name)"
        elif Regex.IsMatch(n, @"[\s]+") then
            Error $"'{n}' contains white space char"
        else
            Ok true


    let validateAddress name (address:string) targetType =
        match targetType with
        | XGI -> if address.IsXGIAddress() then Ok true else Error $"Invalid address: '{name} ({address})'"
        | XGK -> if address.IsXGKAddress() then Ok true else Error $"Invalid address: '{name} ({address})'"
        | _ -> Error $"Invalid targetType: '{targetType}'"


    /// Xml Symbol tag 가 가지는 속성
    type SymbolInfo = {
        Name:string
        /// "BOOL"
        Type:string
        InitValue:obj
        Comment:string
        /// "M"
        Device:string
        /// "%MX1"
        Address:string
        DevicePos:int //XGK 일경우 DevicePos 정보 필요
        Kind:int
        State:int
        AddressIEC : string //XGK 일경우 IEC 주소로 변환해서 가지고 있음
        AddressAlias : ResizeArray<string> //동일주소를 갖는 Tag를 별칭으로 이름을 저장 (AB PLC tag alias 유사)
    } with
        member x.Validate(targetType) =
            result {
                let! _ = validateVariableName x.Name targetType
                let! _ =
                    if x.Address.IsNullOrEmpty() && x.Device = "" then   //빈주소 자동 변수로 허용
                         Ok true
                    else
                        validateAddress x.Name x.Address targetType
                return! Ok()
            }
        member x.IsDirectAddress = x.AddressAlias.Any()

    let defaultSymbolInfo = {
        Name       = ""
        Type       = ""
        InitValue  = null
        Comment    = ""
        Device     = ""
        Address    = ""
        DevicePos  = -1
        Kind       = int Variable.Kind.VAR
        State      = 0
        AddressIEC = ""
        AddressAlias = ResizeArray<string>()
    }


    /// name -> comment -> plcType -> kind -> SymbolInfo
    let mutable fwdCreateSymbolInfo =
        let dummy (_name:string) (_comment:string) (_plcType:string) (_kind:int) (_initValue:BoxedObjectHolder) : SymbolInfo =
            failwithlog "Should be reimplemented."
        dummy

