// Copyright (c) Dualsoft  All Rights Reserved.
namespace Dual.Ev2

open Dual.Common.Core.FS
open System
open System.Collections.Generic


[<AutoOpen>]
module DsDataType =

    let [<Literal>] PLCBOOL    = "bit"
    let [<Literal>] PLCUINT8   = "byte"
    let [<Literal>] PLCUINT16  = "word"
    let [<Literal>] PLCUINT32  = "dword"
    let [<Literal>] PLCUINT64  = "lword"


    /// DefaultValue * MaxValue * DefaultToString
    let private typeInfo = dict [
        BOOL,     (box false, box true, "false")
        CHAR,     (box ' ', box '\uFFFF', "' '")  // Unicode 최대값
        FLOAT32,  (box 0.0f, box Single.MaxValue, "0.0f")
        FLOAT64,  (box 0.0, box Double.MaxValue, "0.0")
        INT8,     (box 0y, box SByte.MaxValue, "0y")
        INT16,    (box 0s, box Int16.MaxValue, "0s")
        INT32,    (box 0, box Int32.MaxValue, "0")
        INT64,    (box 0L, box Int64.MaxValue, "0L")
        STRING,   (box "", box "", "\"\"") // 문자열은 기본값과 최대값이 동일
        UINT8,    (box 0uy, box Byte.MaxValue, "0uy")
        UINT16,   (box 0us, box UInt16.MaxValue, "0us")
        UINT32,   (box 0u, box UInt32.MaxValue, "0u")
        UINT64,   (box 0UL, box UInt64.MaxValue, "0UL")
    ]

    let private getTypeInfo (typ: System.Type) =
        match typeInfo.TryGetValue typ.Name with
        | true, v -> v
        | _ -> failwithlog "ERROR"

    let typeDefaultValue    typ = getTypeInfo typ |> Tuple.item 0
    let typeMaxValue        typ = getTypeInfo typ |> Tuple.item 1
    let typeDefaultToString typ = getTypeInfo typ |> Tuple.item 2

(*
    /// DataType Enum
    type DataType =
        | DuBOOL | DuCHAR | DuFLOAT32 | DuFLOAT64 | DuINT16 | DuINT32
        | DuINT64 | DuINT8 | DuSTRING | DuUINT16 | DuUINT32 | DuUINT64 | DuUINT8

    /// DataType => DefaultValue *  MaxValue     *      DefaultToString * SystemType * PLCType * Type *               BitSize
    let dataTypeInfo = dict [
        DuBOOL,    (box false,      box true,            "false",        BOOL,       PLCBOOL,    typedefof<bool>,    1)
        DuCHAR,    (box ' ',        box '\uFFFF',        "' '",          CHAR,       CHAR,       typedefof<char>,    8)
        DuFLOAT32, (box 0.0f,       box Single.MaxValue, "0.0f",         FLOAT32,    FLOAT32,    typedefof<single>, 32)
        DuFLOAT64, (box 0.0,        box Double.MaxValue, "0.0",          FLOAT64,    FLOAT64,    typedefof<double>, 64)
        DuINT16,   (box 0s,         box Int16.MaxValue,  "0s",           INT16,      INT16,      typedefof<int16>,  16)
        DuINT32,   (box 0,          box Int32.MaxValue,  "0",            INT32,      INT32,      typedefof<int32>,  32)
        DuINT64,   (box 0L,         box Int64.MaxValue,  "0L",           INT64,      INT64,      typedefof<int64>,  64)
        DuINT8,    (box 0y,         box SByte.MaxValue,  "0y",           INT8,       INT8,       typedefof<sbyte>,   8)
        DuSTRING,  (box "",         box "",              "\"\"",         STRING,     STRING,     typedefof<string>, 32 * 8)
        DuUINT16,  (box 0us,        box UInt16.MaxValue, "0us",          UINT16,     PLCUINT16,  typedefof<uint16>, 16)
        DuUINT32,  (box 0u,         box UInt32.MaxValue, "0u",           UINT32,     PLCUINT32,  typedefof<uint32>, 32)
        DuUINT64,  (box 0UL,        box UInt64.MaxValue, "0UL",          UINT64,     PLCUINT64,  typedefof<uint64>, 64)
        DuUINT8,   (box 0uy,        box Byte.MaxValue,   "0uy",          UINT8,      PLCUINT8,   typedefof<byte>,    8)
    ]

    /// 문자열을 DataType으로 변환하는 매핑
    let stringToDataType = dict [
        BOOL,    DuBOOL
        CHAR,    DuCHAR
        FLOAT32, DuFLOAT32
        FLOAT64, DuFLOAT64
        INT16,   DuINT16
        INT32,   DuINT32
        INT64,   DuINT64
        INT8,    DuINT8
        STRING,  DuSTRING
        UINT16,  DuUINT16
        UINT32,  DuUINT32
        UINT64,  DuUINT64
        UINT8,   DuUINT8
    ]
*)

    type DataType =
        | DuBOOL
        | DuCHAR
        | DuFLOAT32
        | DuFLOAT64
        | DuINT16
        | DuINT32
        | DuINT64
        | DuINT8
        | DuSTRING
        | DuUINT16
        | DuUINT32
        /// Obsolete: XGK PLC 에서 지원되지 않는 type.  모듈 공용화를 위해서 가급적 사용하지 않는 걸로..
        | DuUINT64
        | DuUINT8

    let typeMappings = dict [
        DuBOOL,    (BOOL, PLCBOOL, "BOOL", typedefof<bool>, 1)
        DuCHAR,    (CHAR, CHAR, "CHAR", typedefof<char>, 8)
        DuFLOAT32, (FLOAT32, FLOAT32, "REAL", typedefof<single>, 32)
        DuFLOAT64, (FLOAT64, FLOAT64, "LREAL", typedefof<double>, 64)
        DuINT16,   (INT16, INT16, "INT", typedefof<int16>, 16)
        DuINT32,   (INT32, INT32, "DINT", typedefof<int32>, 32)
        DuINT64,   (INT64, INT64, "LINT", typedefof<int64>, 64)
        DuINT8,    (INT8, INT8, "SINT", typedefof<sbyte>, 8)
        DuSTRING,  (STRING, STRING, "STRING", typedefof<string>, 32 * 8)
        DuUINT16,  (UINT16, PLCUINT16, "UINT", typedefof<uint16>, 16)
        DuUINT32,  (UINT32, PLCUINT32, "UDINT", typedefof<uint32>, 32)
        DuUINT64,  (UINT64, PLCUINT64, "ULINT", typedefof<uint64>, 64)
        DuUINT8,   (UINT8, PLCUINT8, "BYTE", typedefof<byte>, 8)
    ]

    let blockSizeMappings = dict [
        DuUINT16, (16, PLCUINT16)
        DuUINT32, (32, PLCUINT32)
        DuUINT64, (64, PLCUINT64)
        DuUINT8, (8, PLCUINT8)
    ]

    type DataType with
        member x.ToText() = typeMappings.[x] |> fun (text, _, _, _, _) -> text
        member x.ToPLCText() = typeMappings.[x] |> fun (_, plcText, _, _, _) -> plcText
        member x.ToPLCType() = typeMappings.[x] |> fun (_, _, plcType, _, _) -> plcType
        member x.ToType() = typeMappings.[x] |> fun (_, _, _, typ, _) -> typ
        member x.ToBitSize() = typeMappings.[x] |> fun (_, _, _, _, size) -> size
        member x.ToTextLower() = x.ToText().ToLower()

        member x.ToBlockSizeNText() =
            match blockSizeMappings.TryGetValue x with
            | true, v -> v
            | _ -> failwithf $"'{x}' not support ToBlockSize"

            member x.ToValue(valueText: string) =
                let valueText =
                    if x = DuCHAR || x = DuSTRING || x = DuBOOL then valueText
                    else valueText.TrimEnd([|'f'; 's'; 'L'; 'u'; 'y'|])

                match x.ToType().Name with
                | "Boolean" -> Convert.ToBoolean valueText |> box
                | "Char"    -> Convert.ToChar valueText |> box
                | "Single"  -> Convert.ToSingle valueText |> box
                | "Double"  -> Convert.ToDouble valueText |> box
                | "Int16"   -> Convert.ToInt16 valueText |> box
                | "Int32"   -> Convert.ToInt32 valueText |> box
                | "Int64"   -> Convert.ToInt64 valueText |> box
                | "SByte"   -> Convert.ToSByte valueText |> box
                | "String"  -> box valueText
                | "UInt16"  -> Convert.ToUInt16 valueText |> box
                | "UInt32"  -> Convert.ToUInt32 valueText |> box
                | "UInt64"  -> Convert.ToUInt64 valueText |> box
                | "Byte"    -> Convert.ToByte valueText |> box
                | _ -> failwith $"Unsupported type {x}"

        member x.DefaultValue() = typeDefaultValue (x.ToType())







    let getDataType (typ:System.Type) =
        match typ.Name with
        | BOOL      -> DuBOOL
        | CHAR      -> DuCHAR
        | FLOAT32   -> DuFLOAT32
        | FLOAT64   -> DuFLOAT64
        | INT16     -> DuINT16
        | INT32     -> DuINT32
        | INT64     -> DuINT64
        | INT8      -> DuINT8
        | STRING    -> DuSTRING
        | UINT16    -> DuUINT16
        | UINT32    -> DuUINT32
        | UINT64    -> DuUINT64
        | UINT8     -> DuUINT8
        | _  -> failwithlog "ERROR"

    let getDataTypeFromName (textTypeName:string) =
        getDataType(Type.GetType(textTypeName))

    let ToStringValue (value: obj) =
        match getDataType(value.GetType()), value  with
        | DuBOOL     , _ -> value.ToString()
        | DuCHAR     , _ -> sprintf "'%c'" (Convert.ToChar(value))
        | DuFLOAT32  , (:? float32 as v) -> sprintf "%gf" v
        | DuFLOAT64  , (:? float   as v) -> sprintf "%g" v
        | DuINT16    , (:? int16   as v) -> sprintf "%ds" v
        | DuINT32    , (:? int     as v) -> sprintf "%d" v
        | DuINT64    , (:? int64   as v) -> sprintf "%dL" v
        | DuINT8     , (:? sbyte   as v) -> sprintf "%dy" v
        | DuSTRING   , (:? string  as v) -> sprintf "\"%s\"" v
        | DuUINT16   , (:? uint16  as v) -> sprintf "%dus" v
        | DuUINT32   , (:? uint32  as v) -> sprintf "%du" v
        | DuUINT64   , (:? uint64  as v) -> sprintf "%dUL" v
        | DuUINT8    , (:? byte    as v) -> sprintf "%duy" v
        | _  -> failwithf $"ERROR: Unsupported type {value.GetType()} for value {value}"

    let getTextValueNType (x: string) =
        match x with
        | _ when x.StartsWith("\"") && x.EndsWith("\"") && x.Length > 1 ->
            Some (x.[1..x.Length-2], DuSTRING)
        | _ when x.StartsWith("'") && x.EndsWith("'") && x.Length = 3 ->
            Some (x.[1].ToString(), DuCHAR)
        | _ when x.EndsWith("f") && x |> Seq.forall (fun c -> Char.IsDigit(c) || c = '.' || c = 'f') ->
            Some (x.TrimEnd('f'), DuFLOAT32)
        | _ when x.Contains(".") && x |> Seq.forall (fun c -> Char.IsDigit(c) || c = '.') ->
            Some (x, DuFLOAT64)
        | _ when x.EndsWith("uy") && Byte.TryParse(x.TrimEnd([|'u';'y'|]))|> fst  ->
            Some (x.TrimEnd([|'u';'y'|]), DuUINT8)
        | _ when x.EndsWith("us") && UInt16.TryParse(x.TrimEnd([|'u';'s'|]))|> fst  ->
            Some (x.TrimEnd([|'u';'s'|]), DuUINT16)
        | _ when x.EndsWith("u") && UInt32.TryParse(x.TrimEnd('u'))|> fst  ->
            Some (x.TrimEnd('u'), DuUINT32)
        | _ when x.EndsWith("UL") && UInt64.TryParse(x.TrimEnd([|'U';'L'|]))|> fst  ->
            Some (x.TrimEnd([|'U';'L'|]), DuUINT64)
        | _ when x.ToLower() = "true" || x.ToLower() = "false" ->
            Some (x, DuBOOL)
        | _ when x.ToLower() = "t" -> Some ("true", DuBOOL)
        | _ when x.ToLower() = "f" -> Some ("false", DuBOOL)
        | _ when x.EndsWith("L") && Int64.TryParse(x.TrimEnd('L'))|> fst  ->
            Some (x.TrimEnd('L'), DuINT64)
        | _ when x.EndsWith("I") && Int32.TryParse(x.TrimEnd('I'))|> fst  ->
            Some (x.TrimEnd('I'), DuINT32)
        | _ when x.EndsWith("s") && Int16.TryParse(x.TrimEnd('s'))|> fst  ->
            Some (x.TrimEnd('s'), DuINT16)
        | _ when x.EndsWith("y") && SByte.TryParse(x.TrimEnd('y'))|> fst  ->
            Some (x.TrimEnd('y'), DuINT8)

        | _ when Int32.TryParse(x) |> fst ->
            Some (x, DuINT32)
        | _ -> None

    let isValidValue(x) = getTextValueNType x |> Option.isSome
    let getTrimmedValueNType(x)  =
        let trimmedTextValueNDataType = getTextValueNType x
        match trimmedTextValueNDataType with
        | Some (v,ty) -> ty.ToValue(v), ty
        | None -> failwithlog $"TryParse error datatype {x}"

    let toValue (x:string) = getTrimmedValueNType x |> fst

    let toValueType (x:string) = getTrimmedValueNType x |> snd


    type IOType = | In | Out | Memory | NotUsed

    type SlotDataType(slotIndex:int, ioType:IOType, dataType:DataType) =
        member x.SlotIndex = slotIndex
        member x.IOType = ioType
        member x.DataType = dataType

        member x.ToText() = sprintf "%d %A %A" x.SlotIndex x.IOType (x.DataType.ToType().FullName)
        /// 문자열로부터 SlotDataType 생성
        static member Create(slotIndex: string, ioType: string, dataTypeText: string) =
            try
                let slotIndex = int slotIndex
                let ioType =
                    match ioType with
                    | "In" -> IOType.In
                    | "Out" -> IOType.Out
                    | "NotUsed" -> IOType.NotUsed
                    | _ -> failwithf "Invalid IOType: %s" ioType

                let dataType = getDataTypeFromName(dataTypeText.Trim('"'))
                SlotDataType(slotIndex, ioType, dataType)
            with
            | _ as ex ->
                failwithf "Failed to create SlotDataType: %s" ex.Message

    let getBlockType(blockSlottype:string) =
        match blockSlottype.ToLower() with
        | PLCUINT8  -> DuUINT8
        | PLCUINT16 -> DuUINT16
        | PLCUINT32 -> DuUINT32
        | PLCUINT64 -> DuUINT64
        | _ -> failwithf $"'size bit {blockSlottype}' not support getBlockType"


    let tryTextToDataType(typeName:string) =
        match typeName.ToLower() with
        //system1   | system2   | plc
        | "boolean" | "bool"    | "bit"  ->  DuBOOL      |> Some
        | "char"                         ->  DuCHAR      |> Some
        | "float32" | "single"           ->  DuFLOAT32   |> Some
        | "float64" | "double"           ->  DuFLOAT64   |> Some
        | "int16"   | "short"            ->  DuINT16     |> Some
        | "int32"   | "int"              ->  DuINT32     |> Some
        | "int64"   | "long"             ->  DuINT64     |> Some
        | "int8"    | "sbyte"            ->  DuINT8      |> Some
        | "string"                       ->  DuSTRING    |> Some
        | "uint16"  | "ushort"  |"word"  ->  DuUINT16    |> Some
        | "uint32"  | "uint"    |"dword" ->  DuUINT32    |> Some
        | "uint64"  | "ulong"   |"lword" ->  DuUINT64    |> Some
        | "uint8"   | "byte"    |"byte"  ->  DuUINT8     |> Some
        | _ -> None


    let textToDataType(typeName:string) : DataType =
        tryTextToDataType typeName |?? (fun () -> failwithf $"'{typeName}' DataToType Error check type")

    let textToSystemType(typeName:string) : System.Type =
        textToDataType typeName |> fun x -> x.ToType()

    let middleValue (a: obj) (b: obj) : obj option =
        match a, b with
            // Prevent overflow by calculating the midpoint using an alternative method
        | :? int64 as ia, (:? int64 as ib) ->
            if ia > ib then
                Some (box (ia - ((ia - ib) / 2L)))
            else
                Some (box (ib - ((ib - ia) / 2L)))
        | :? int as ia, (:? int as ib) ->
            // Same logic applies to int
            if ia > ib then
                Some (box (ia - ((ia - ib) / 2)))
            else
                Some (box (ib - ((ib - ia) / 2)))
        | :? float as fa, (:? float as fb) ->
            Some (box ((fa + fb) / 2.0))
        | :? single as fsa, (:? single as fsb) ->
            Some (box ((fsa + fsb) / 2.0f))
        | :? sbyte as sba, (:? sbyte as sbb) ->
            Some (box (sbyte ((int sba + int sbb) / 2)))
        | :? int16 as sa, (:? int16 as sb) ->
            Some (box (int16 ((int sa + int sb) / 2)))
        | :? byte as ba, (:? byte as bb) ->
            Some (box (byte ((int ba + int bb) / 2)))
        | :? uint16 as usa, (:? uint16 as usb) ->
            Some (box (uint16 ((int usa + int usb) / 2)))
        | :? uint32 as ua, (:? uint32 as ub) ->
            Some (box ((ua + ub) / 2u))
        | :? uint64 as ula, (:? uint64 as ulb) ->
            // Prevent overflow for uint64
            if ula > ulb then
                Some (box (ula - ((ula - ulb) / 2UL)))
            else
                Some (box (ulb - ((ulb - ula) / 2UL)))
        | _ -> None

