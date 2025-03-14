namespace Dual.Plc2DS.LS

open Dual.Common.Core.FS
open Dual.Plc2DS
open System
open System.Diagnostics

(*
Remark,Title=CSV File
Remark,Date=2025-3-6-13:41:54.459
Remark,Version=Ver3.1
Remark,PLC Name=LSPLC
Remark,CPU Type=XGI-CPUUN
Remark,EIP GUID=65a0ca83-53ad-48ee-9da6-f3dd04e15349
Remark,EIP OnlineWrite=0
Type,Scope,Variable,Address,DataType,Property,Comment
Tag,GlobalVariable,"AGV_M_I_AUTO_MODE",%MW24000.0,"BOOL",,"AGV 자동모드"
*)

[<DebuggerDisplay("{Stringify()}")>]
type PlcTagInfo(?typ, ?scope, ?variable, ?address, ?dataType, ?property, ?comment) =
    inherit FDA()

    let typ      = typ      |? ""
    let scope    = scope    |? ""
    let variable = variable |? ""
    let address  = address  |? ""
    let dataType = dataType |? ""
    let property = property |? ""
    let comment  = comment  |? ""

    interface IPlcTag
    member val Type    = typ      with get, set
    member val Scope   = scope    with get, set
    member val Variable= variable with get, set
    member val Address = address  with get, set
    member val DataType= dataType with get, set
    member val Property= property with get, set
    member val Comment = comment  with get, set

    member x.Stringify() = $"{x.Variable} = {base.Stringify()}, {x.Address}, {x.Type}, {x.DataType}, {x.Comment}"



/// LS.CsvReader
type CsvReader =
    static member CreatePlcTagInfo(line: string) : PlcTagInfo =
        let cols = Csv.ParseLine line
        assert(cols.Length = 7)
        PlcTagInfo(typ = cols[0], scope = cols[1], variable = cols[2], address = cols[3],
            dataType = cols[4], Property = cols[5], Comment = cols[6])

    static member ReadCommentCSV(filePath: string): PlcTagInfo[] =
        let header = "Type,Scope,Variable,Address,DataType,Property,Comment"
        match File.TryReadUntilHeader(filePath, header) with
        | Some headers ->
            File.PeekLines(filePath, headers.Length)
            |> Seq.filter (fun l -> not (String.IsNullOrEmpty l) && l.[0] <> '\u0000')  // 파일 맨 마지막 라인 NULL 라인
            |> map CsvReader.CreatePlcTagInfo
            |> toArray
        | None ->
            failwith $"ERROR: failed to find header {header}"


