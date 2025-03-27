(*
S7 *.SDF 파일 한계
- 내용에 "," 가 포함된 경우, encoding 처리가 안되어서 분석 불가: 자체 simatic manager에서도 export 후 import 시 제대로 읽어 들이지 못함
 *)

namespace Dual.Plc2DS.S7

open System.Runtime.Serialization

open Dual.Plc2DS
open Dual.Common.Core.FS
open Dual.Common.Base


[<DataContract>]
type PlcTagInfo(?name, ?address, ?dataType, ?comment) =
    inherit PlcTagBaseFDA()

    let name     = name     |? ""
    let address  = address  |? ""
    let dataType = dataType |? ""
    let comment  = comment  |? ""

    new() = PlcTagInfo(null, null, null, null)    // for JSON parameterless constructor
    [<DataMember>] member val Name     = name     with get, set
    [<DataMember>] member val Comment  = comment  with get, set
    [<DataMember>] member val Address  = address  with get, set
    [<DataMember>] member val DataType = dataType with get, set



/// S7.SdfReader (Csv 와 동일 형식)
type SdfReader =
    static member CreatePlcTagInfo(line: string) : PlcTagInfo =
        let cols = Csv.ParseLine line
        let name = cols[0]
        let dataType = cols[2]
        match cols.Length with
        | 4 ->  // name * address * data type * comment
            let address =
                match cols[1] with       // "I  200.2 ", "T 99 "  등의 불균일 요소 => "I 202.2", "T 99" 로 정리
                | RegexPattern @"^\s*(\w+)\s+(\d+(?:\.\d+)*)\s*$" [typ; addr] ->
                    $"{typ} {addr}"
                | _ -> cols[1]
            let comment = cols[3]
            PlcTagInfo(name, address, dataType, comment)
        | 9 ->  // name * address * data type * bool * bool * bool * comment * ? * bool
            let address = cols[1]
            let comment = cols[6]
            PlcTagInfo(name, address, dataType, comment)
        | _ ->
            failwith "Invalid file format"

    /// read .SDF comment file
    static member Read(sdfPath: string) : PlcTagInfo[] =
        File.PeekLines(sdfPath, 0)
        |> toArray
        |> map SdfReader.CreatePlcTagInfo

    static member ReadCommentCSV(sdfPath: string) = SdfReader.Read(sdfPath)


