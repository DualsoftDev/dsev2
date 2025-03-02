// Copyright (c) Dualsoft  All Rights Reserved.
namespace Dual.Ev2

open Dual.Common.Core.FS
open System
open System.Collections.Generic


[<AutoOpen>]
module DsDataType =
    //data 타입 지원 항목 : 알파벳 순 정렬 (Alt+Shift+L, Alt+Shift+S)
    let [<Literal>] BOOL    = "Boolean"
    let [<Literal>] CHAR    = "Char"
    let [<Literal>] FLOAT32 = "Single"
    let [<Literal>] FLOAT64 = "Double"
    let [<Literal>] INT16   = "Int16"
    let [<Literal>] INT32   = "Int32"
    let [<Literal>] INT64   = "Int64"
    let [<Literal>] INT8    = "SByte"
    let [<Literal>] STRING  = "String"
    let [<Literal>] UINT16  = "UInt16"
    let [<Literal>] UINT32  = "UInt32"
    let [<Literal>] UINT64  = "UInt64"
    let [<Literal>] UINT8   = "Byte"
    let [<Literal>] OBJECT  = "Object"

    let [<Literal>] PLCBOOL    = "bit"
    let [<Literal>] PLCUINT8   = "byte"
    let [<Literal>] PLCUINT16  = "word"
    let [<Literal>] PLCUINT32  = "dword"
    let [<Literal>] PLCUINT64  = "lword"


    /// DataType Enum
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



    // TypeInfo 클래스 정의
    type TypeInfo(dataType, typ, typeString, plcType, plcText, defaultValue: obj, defaultString, maxValue: obj, bitSize: int) =
        member val DataType:DataType    = dataType
        member val Type:Type            = typ
        member val TypeString:string    = typeString
        member val PLCType:string       = plcType
        member val PLCText:string       = plcText
        member val DefaultValue:obj     = defaultValue
        member val DefaultString:string = defaultString
        member val MaxValue:obj         = maxValue
        member val BitSize:int          = bitSize

    /// DataType =>         DatType,   SystemType,       TypeStreing, PLCType,    PLCText,  DefaultValue *  DefaultToString,  MaxValue,                   BitSize
    let dataTypeInfo = dict [
        DuBOOL,    TypeInfo(DuBOOL,    typedefof<bool>,   BOOL,       PLCBOOL,    "BOOL",   box false,      "false",          box true,                    1)
        DuCHAR,    TypeInfo(DuCHAR,    typedefof<char>,   CHAR,       CHAR,       "CHAR",   box ' ',        "' '",            box '\uFFFF',                8)
        DuFLOAT32, TypeInfo(DuFLOAT32, typedefof<single>, FLOAT32,    FLOAT32,    "REAL",   box 0.0f,       "0.0f",           box Single.MaxValue,        32)
        DuFLOAT64, TypeInfo(DuFLOAT64, typedefof<double>, FLOAT64,    FLOAT64,    "LREAL",  box 0.0,        "0.0",            box Double.MaxValue,        64)
        DuINT16,   TypeInfo(DuINT16,   typedefof<int16>,  INT16,      INT16,      "INT",    box 0s,         "0s",             box Int16.MaxValue,         16)
        DuINT32,   TypeInfo(DuINT32,   typedefof<int32>,  INT32,      INT32,      "DINT",   box 0,          "0",              box Int32.MaxValue,         32)
        DuINT64,   TypeInfo(DuINT64,   typedefof<int64>,  INT64,      INT64,      "LINT",   box 0L,         "0L",             box Int64.MaxValue,         64)
        DuINT8,    TypeInfo(DuINT8,    typedefof<sbyte>,  INT8,       INT8,       "SINT",   box 0y,         "0y",             box SByte.MaxValue,          8)
        DuSTRING,  TypeInfo(DuSTRING,  typedefof<string>, STRING,     STRING,     "STRING", box "",         "\"\"",           box "",                     32 * 8)
        DuUINT16,  TypeInfo(DuUINT16,  typedefof<uint16>, UINT16,     PLCUINT16,  "UINT",   box 0us,        "0us",            box UInt16.MaxValue,        16)
        DuUINT32,  TypeInfo(DuUINT32,  typedefof<uint32>, UINT32,     PLCUINT32,  "UDINT",  box 0u,         "0u",             box UInt32.MaxValue,        32)
        DuUINT64,  TypeInfo(DuUINT64,  typedefof<uint64>, UINT64,     PLCUINT64,  "ULINT",  box 0UL,        "0UL",            box UInt64.MaxValue,        64)
        DuUINT8,   TypeInfo(DuUINT8,   typedefof<byte>,   UINT8,      PLCUINT8,   "BYTE",   box 0uy,        "0uy",            box Byte.MaxValue,           8)
    ]

    let blockSizeMappings = dict [
        DuUINT16, (16, PLCUINT16)
        DuUINT32, (32, PLCUINT32)
        DuUINT64, (64, PLCUINT64)
        DuUINT8, (8, PLCUINT8)
    ]

    type DataType with
        member x.ToText()      = dataTypeInfo[x].TypeString
        member x.ToPLCText()   = dataTypeInfo[x].PLCType
        member x.ToPLCType()   = dataTypeInfo[x].PLCText
        member x.ToType()      = dataTypeInfo[x].Type
        member x.ToBitSize()   = dataTypeInfo[x].BitSize
        member x.ToTextLower() = x.ToText().ToLower()

        member x.ToBlockSizeNText() =
            match blockSizeMappings.TryGetValue x with
            | true, v -> v
            | _ -> failwithf $"'{x}' not support ToBlockSize"

        member x.ToValue(valueText: string) =
            let valueText =
                if x = DuCHAR || x = DuSTRING || x = DuBOOL then valueText
                else valueText.TrimEnd([|'f'; 's'; 'L'; 'u'; 'y'|])

            match x with
            | DuBOOL    -> Convert.ToBoolean valueText |> box
            | DuCHAR    -> Convert.ToChar    valueText |> box
            | DuFLOAT32 -> Convert.ToSingle  valueText |> box
            | DuFLOAT64 -> Convert.ToDouble  valueText |> box
            | DuINT16   -> Convert.ToInt16   valueText |> box
            | DuINT32   -> Convert.ToInt32   valueText |> box
            | DuINT64   -> Convert.ToInt64   valueText |> box
            | DuINT8    -> Convert.ToSByte   valueText |> box
            | DuSTRING  -> box               valueText
            | DuUINT16  -> Convert.ToUInt16  valueText |> box
            | DuUINT32  -> Convert.ToUInt32  valueText |> box
            | DuUINT64  -> Convert.ToUInt64  valueText |> box
            | DuUINT8   -> Convert.ToByte    valueText |> box
            | _ -> failwith $"Unsupported type {x}"

        member x.DefaultValue() = dataTypeInfo[x].DefaultValue



    let typeDict = dict [
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

    let getDataType (typ: System.Type) : DataType =
        match typeDict.TryGetValue(typ.Name) with
        | true, value -> value
        | false, _ -> failwithlog "ERROR"

    let typeDefaultValue    typ = let t = getDataType typ in dataTypeInfo[t].DefaultValue
    let typeMaxValue        typ = let t = getDataType typ in dataTypeInfo[t].MaxValue
    let typeDefaultToString typ = let t = getDataType typ in dataTypeInfo[t].DefaultString



    let getDataTypeFromName (textTypeName:string) =
        getDataType(Type.GetType(textTypeName))

    let ToStringValue (value: obj) =
        match getDataType(value.GetType()), value  with
        | DuBOOL,    _                 -> value.ToString()
        | DuCHAR,    _                 -> sprintf "'%c'" (Convert.ToChar(value))
        | DuFLOAT32, (:? float32 as v) -> sprintf "%gf" v
        | DuFLOAT64, (:? float   as v) -> sprintf "%g" v
        | DuINT16,   (:? int16   as v) -> sprintf "%ds" v
        | DuINT32,   (:? int     as v) -> sprintf "%d" v
        | DuINT64,   (:? int64   as v) -> sprintf "%dL" v
        | DuINT8,    (:? sbyte   as v) -> sprintf "%dy" v
        | DuSTRING,  (:? string  as v) -> sprintf "\"%s\"" v
        | DuUINT16,  (:? uint16  as v) -> sprintf "%dus" v
        | DuUINT32,  (:? uint32  as v) -> sprintf "%du" v
        | DuUINT64,  (:? uint64  as v) -> sprintf "%dUL" v
        | DuUINT8,   (:? byte    as v) -> sprintf "%duy" v
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
        | (:? int64   as ia),  (:? int64   as ib)  -> Some (int64  ((ia + ib)           / 2L)    :> obj)
        | (:? int     as ia),  (:? int     as ib)  -> Some (int    ((ia + ib)           / 2)     )
        | (:? float   as fa),  (:? float   as fb)  -> Some (float  ((fa + fb)           / 2.0)   )
        | (:? single  as fsa), (:? single  as fsb) -> Some (single ((fsa + fsb)         / 2.0f)  )
        | (:? sbyte   as sba), (:? sbyte   as sbb) -> Some (sbyte  ((int sba + int sbb) / 2)     )
        | (:? int16   as sa),  (:? int16   as sb)  -> Some (int16  ((int sa  + int sb)  / 2)     )
        | (:? byte    as ba),  (:? byte    as bb)  -> Some (byte   ((int ba  + int bb)  / 2)     )
        | (:? uint16  as usa), (:? uint16  as usb) -> Some (uint16 ((int usa + int usb) / 2)     )
        | (:? uint32  as ua),  (:? uint32  as ub)  -> Some (uint32 ((ua + ub)           / 2u)    )
        | (:? uint64  as ula), (:? uint64  as ulb) -> Some (uint64 ((ula + ulb)         / 2UL)   )
        | _ -> None

