namespace Dual.Plc2DS.LS

open System
open System.Diagnostics
open System.Runtime.Serialization
open Newtonsoft.Json

open Dual.Common.Core.FS
open Dual.Common.Base
open Dual.Plc2DS

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
[<DataContract>]
type PlcTagInfo(?typ, ?scope, ?variable, ?address, ?dataType, ?property, ?comment) =
    inherit PlcTagBaseFDA()

    let typ      = typ      |? ""
    let scope    = scope    |? ""
    let variable = variable |? ""
    let address  = address  |? ""
    let dataType = dataType |? ""
    let property = property |? ""
    let comment  = comment  |? ""

    new() = PlcTagInfo(null, null, null, null, null, null, null)    // for JSON parameterless constructor
    [<DataMember>] member val Type    = typ      with get, set
    [<DataMember>] member val Scope   = scope    with get, set
    [<DataMember>] member val Variable= variable with get, set
    [<DataMember>] member val Address = address  with get, set
    [<DataMember>] member val DataType= dataType with get, set
    [<DataMember>] member val Property= property with get, set
    [<DataMember>] member val Comment = comment  with get, set

    [<DataMember>] member val VariableProcessing = variable with get, set
    [<JsonIgnore>] member val VariableOriginal = variable with get, set

    override x.Stringify() = $"{x.Variable} = {base.Stringify()}, {x.Address}, {x.Type}, {x.DataType}, {x.Comment}"
    override x.Csvify() = $"{x.Type},{x.Scope},{x.Variable},{x.Address},{x.DataType},{x.Property}, {x.Comment},{x.VariableOriginal},{base.Csvify()}"

    override x.OnDeserialized() =
        x.VariableOriginal <- x.Variable
        x.Variable <- x.VariableProcessing
    override x.OnSerializing() =
        x.VariableProcessing <- x.Variable
        x.Variable <- x.VariableOriginal


/// LS.CsvReader
type CsvReader =
    static member CreatePlcTagInfo(line: string) : PlcTagInfo =
        let cols = Csv.ParseLine line
        assert(cols.Length = 7)
        PlcTagInfo(typ = cols[0], scope = cols[1], variable = cols[2], address = cols[3],
            dataType = cols[4], Property = cols[5], Comment = cols[6])

    static member Read(filePath: string): PlcTagInfo[] =
        let header = "Type,Scope,Variable,Address,DataType,Property,Comment"
        match File.TryReadUntilHeader(filePath, header) with
        | Some headers ->
            File.PeekLines(filePath, headers.Length)
            |> Seq.filter (fun l -> not (String.IsNullOrEmpty l) && l.[0] <> '\u0000')  // 파일 맨 마지막 라인 NULL 라인
            |> map CsvReader.CreatePlcTagInfo
            |> toArray
        | None ->
            failwith $"ERROR: failed to find header {header}"


