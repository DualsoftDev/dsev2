namespace Dual.Plc2DS.LS

open Dual.Common.Core.FS
open Dual.Plc2DS
open System

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

type PlcTagInfo = {
    Type    : string
    Scope   : string
    Variable: string
    Address : string
    DataType: string
    Property: string
    Comment : string

    mutable FlowName  :string
    mutable DeviceName:string
    mutable ActionName:string

    mutable Temporary :obj
} with
    interface IPlcTag

/// LS.CsvReader
type CsvReader =
    static member CreatePlcTagInfo(line: string) : PlcTagInfo =
        let cols = Csv.ParseLine line
        assert(cols.Length = 7)
        {   Type = cols[0]; Scope = cols[1]; Variable = cols[2]; Address = cols[3]
            DataType = cols[4]; Property = cols[5]; Comment = cols[6]
            FlowName = null; DeviceName = null; ActionName = null; Temporary = null
        }

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


