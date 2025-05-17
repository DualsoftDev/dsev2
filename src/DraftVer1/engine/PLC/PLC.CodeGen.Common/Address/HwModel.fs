[<AutoOpen>]
module PLCHwModel
open System.Runtime.CompilerServices

    type TagType = Bit | I1 | I2 | I4 | I8 | F4

    type DataLengthType = Undefined | Bit | Byte | Word | DWord | LWord
    type CpuType = Xgk | Xgi | Xgr | XgbMk | XgbIEC | Unknown
    type DataType = Bit | Byte | Word | DWord | LWord | Continuous

    type DeviceType =
        | P  | T
        | M  | C
        | L  | R
        | K  | I
        | F  | Q
        | D  | A
        | U  | W
        | N  | S
        | Z  | ZR //S Step제어용 디바이스 수집 불가

    let toText = function
        | Xgk -> "XGK" | Xgi -> "XGI" | Xgr -> "XGR" | XgbMk -> "XGBMK" | XgbIEC -> "XGBIEC" | _ -> failwith "ERROR"

    let isIEC = function
        | Xgi | XgbIEC -> true | _ -> false

    let getBitLength = function Bit -> 1 | Byte -> 8 | Word -> 16 | DWord -> 32 | LWord -> 64 | Continuous -> failwith "ERROR"

    let getByteLength = function Bit -> 1 | x -> getBitLength x / 8

    let toDataLengthType = function Bit -> Bit | Byte -> Byte | Word -> Word | DWord -> DWord | LWord -> LWord | _ -> failwith "ERROR"

    let toMnemonic = function Bit -> "X" | Byte -> "B" | Word -> "W" | DWord -> "D" | LWord -> "L" | _ -> failwith "ERROR"

    let fromDeviceMnemonic = function "X" -> Bit | "B" -> Byte | "W" -> Word | "D" -> DWord | "L" -> LWord | _ -> failwith "ERROR"

    let toDeviceText = function
        | P -> "P" | M -> "M" | L -> "L" | K -> "K" | F -> "F" | D -> "D" | U -> "U" | N -> "N" | Z -> "Z"
        | T -> "T" | C -> "C" | R -> "R" | I -> "I" | Q -> "Q" | A -> "A" | W -> "W" | S -> "S" | ZR -> "ZR"

    [<AutoOpen>]
    type PLCHwModelExt =
        [<Extension>] static member ToText(x:CpuType) = toText x
        [<Extension>] static member IsIEC(x:CpuType) = isIEC x
        [<Extension>] static member FromDeviceMnemonic(x:string) : DataType = fromDeviceMnemonic x
        [<Extension>] static member GetBitLength(x:DataType)  = getBitLength x
        [<Extension>] static member ToMnemonic(x:DataType)  = toMnemonic x
        [<Extension>] static member GetByteLength(x:DataType)  = getByteLength x
        [<Extension>] static member ToDeviceText(x:DeviceType)  = toDeviceText x
