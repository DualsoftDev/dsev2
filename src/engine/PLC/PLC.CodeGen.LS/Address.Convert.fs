module AddressConvertUnused

//open Dual.Common.Core.FS
//open PLC.CodeGen.Common
//open System.Collections.Generic

// 산전 PLC 주소 체계 : https://tech-e.tistory.com/10


///// CPU info.  XGK: 0xA0, XGI: 0XA4, XGR: 0xA8
//// 사용설명서_XGB FEnet_국문_V1.5.pdf, 5.2.3
//type CpuType =
//    | Xgk    // 0xA0uy    // 160
//    | Xgi    // 0xA4uy    // 164
//    | Xgr    // 0xA8uy    // 168
//    | XgbMk  // 0xB0uy
//    | XgbIEC // 0xB4uy
//    | Unknown
//        member x.ToByte() =
//            match x with
//            | Xgk -> 0xA0uy
//            | Xgi -> 0xA4uy
//            | Xgr -> 0xA8uy
//            | XgbMk -> 0xB0uy
//            | XgbIEC -> 0xB4uy
//            | _ -> failwithlog "ERROR"
//        member x.ToText() =
//            match x with
//            | Xgk -> "XGK"
//            | Xgi -> "XGI"
//            | Xgr -> "XGR"
//            | XgbMk -> "XGBMK"
//            | XgbIEC -> "XGBIEC"
//            | _ -> failwithlog "ERROR"
//        static member FromByte (by:byte) =
//            match by with
//            | 0xA0uy -> CpuType.Xgk
//            | 0xA4uy -> CpuType.Xgi
//            | 0xA8uy -> CpuType.Xgr
//            | 0xB0uy -> CpuType.XgbMk
//            | 0xB4uy -> CpuType.XgbIEC
//            | _ -> failwithlog "ERROR"

//        static member FromID(cpuId:int) =
//            match cpuId with
//            //XGI-CPUE 106
//            //XGI-CPUH 102
//            //XGI-CPUS 104
//            //XGI-CPUU 100
//            //XGI-CPUU/D 107
//            //XGI-CPUUN 111
//            | 100 | 102 | 104 | 106 | 107 | 111  -> Xgi
//            //XGK-CPUA 3
//            //XGK-CPUE 4
//            //XGK-CPUH 0
//            //XGK-CPUHN 16
//            //XGK-CPUS 1
//            //XGK-CPUSN 17
//            //XGK-CPUU 5
//            //XGK-CPUUN 14
//            | 0 | 1 | 3 | 4 | 14 | 16 | 17  -> Xgk
//            //XGB-DR16C3 6
//            //XGB-GIPAM 114
//            //XGB-KL 113
//            //XGB-XBCE 9
//            //XGB-XBCH 7
//            //XGB-XBCS 10
//            //XGB-XBCU 15
//            //XGB-XBCXS 22
//            //XGB-XBMH 18
//            //XGB-XBMH2 21
//            //XGB-XBMHP 19
//            //XGB-XBMS 2
//            //XGB-XECE 109
//            //XGB-XECH 103
//            //XGB-XECS 108
//            //XGB-XECU 112
//            //XGB-XEMH2 116
//            //XGB-XEMHP 115
//            | 103 | 108 | 109 | 112 | 113 | 114 | 115 | 116 -> XgbIEC
//            | 2 | 6 | 7 | 9 | 10 | 15 | 18 | 19 | 21 | 22 -> XgbMk
//            | _ -> failwithlog "ERROR"

///// 16.  개별 읽기 모드에서의 최대 접점 수
//let maxRandomReadTagCount = 16


///// 연속 읽기 모드에서의 최대 byte 수.  XGB 일때는 512, 나머지는 1400
//let getMaxBlockReadByteCount = function
//    | XgbMk -> 512
//    | _ -> 1400


//// "%MX1A" : word = 1, bit offset = 10  ==> 절대 bit offset = 1 * 16 + 10 = 26
//let wordAndBit2AbsBit (word, bit) = word * 16 + bit

//// 절대 bit offset 26 ==> word = 1, bit = A
//let bit2WordAndBit nthBit =
//    let word = nthBit / 16
//    let bit = nthBit % 16
//    (word, bit)



///// LS H/W PLC 통신을 위한 data type
//type DataType =
//    | Bit
//    | Byte
//    | Word
//    | DWord
//    | LWord
//    | Continuous
//        /// Packet 통신에 사용하기 위한 length identifier 값
//        member x.ToUInt16() =
//            match x with
//            | Bit   -> 0us
//            | Byte  -> 1us
//            | Word  -> 2us
//            | DWord -> 3us
//            | LWord -> 4us
//            | Continuous -> 0x14us
//        member x.GetBitLength() =
//            match x with
//            | Bit   -> 1
//            | Byte  -> 8
//            | Word  -> 16
//            | DWord -> 32
//            | LWord -> 64
//            | Continuous -> failwithlog "ERROR"
//        member x.GetByteLength() =
//            match x with
//            | Bit   -> 1
//            | _ -> x.GetBitLength() / 8
//        member x.ToTagType() =
//            match x with
//            | Bit   -> TagType.Bit
//            | Byte  -> TagType.I1
//            | Word  -> TagType.I2
//            | DWord -> TagType.I4
//            | LWord -> TagType.I8
//            | Continuous -> failwithlog "ERROR"
//        member x.ToDataLengthType() =
//            match x with
//            | Bit   -> DataLengthType.Bit
//            | Byte  -> DataLengthType.Byte
//            | Word  -> DataLengthType.Word
//            | DWord -> DataLengthType.DWord
//            | LWord -> DataLengthType.LWord
//            | Continuous -> failwithlog "ERROR"
//        member x.ToMnemonic() =
//            match x with
//            | Bit   -> "X"
//            | Byte  -> "B"
//            | Word  -> "W"
//            | DWord -> "D"
//            | LWord -> "L"
//            | Continuous -> failwithlog "ERROR"
//        member x.Totext() =
//            match x with
//            | Bit   -> "BIT"
//            | Byte  -> "BYTE"
//            | Word  -> "WORD"
//            | DWord -> "DWORD"
//            | LWord -> "LWORD"
//            | Continuous -> failwithlog "ERROR"

//        /// uint64 를 data type 에 맞게 boxing 해서 반환
//        member x.BoxUI8(v:uint64) =
//            match x with
//            | Bit   -> v <> 0UL |> box
//            | Byte  -> byte v   |> box
//            | Word  -> uint16 v |> box
//            | DWord -> uint32 v |> box
//            | LWord -> uint64 v |> box
//            | Continuous -> failwithlog "ERROR"

//        /// Boxing 된 값 v 를 uint64 로 unboxing 해서 반환
//        member x.Unbox2UI8(v:obj) =
//            match (v, x) with
//            | (:? bool as b, Bit)     -> if b then 1UL else 0UL
//            | (:? byte as b, Byte)    -> uint64 b
//            | (:? uint16 as w, Word)  -> uint64 w
//            | (:? uint32 as d, DWord) -> uint64 d
//            | (:? uint64 as l, LWord) -> l
//            | (:? uint64 as l, _) ->
//                logWarn "Mismatched type: %A(%A)" x v
//                l
//            | _ -> failwithlog "ERROR"

//        static member FromDeviceMnemonic = function
//            | "X" -> Bit
//            | "B" -> Byte
//            | "W" -> Word
//            | "D" -> DWord
//            | "L" -> LWord
//            | _ -> failwithlog "ERROR"

////let inline xToBytes (x:'a) = x |> uint16 |> fun x -> x.ToBytes()

//type DeviceType = P | M | L | K | F | S | D | U | N | Z | T | C | R | I | Q | W

//let (|DevicePattern|_|) (str:string) = DU.fromString<DeviceType> str

//let (|DataTypePattern|_|) str =
//    try
//        Some <| DataType.FromDeviceMnemonic str
//    with exn -> None

//type LsTagAnalysis = {
//    /// Original Tag name
//    Tag:string
//    Device:DeviceType
//    DataType:DataType
//    BitOffset:int
//} with
//    member x.ByteLength = (max 8 x.BitLength) / 8
//    member x.BitLength  = x.DataType.GetBitLength()
//    member x.ByteOffset = x.BitOffset / 8
//    member x.WordOffset = x.BitOffset / 16

//let (|LsTagPatternXgi|_|) tag =
//    match tag with
//    // XGI IEC 61131 : bit
//    | RegexPattern @"%([MLKFW])X(\d+)$"
//        [DevicePattern device; Int32Pattern bitOffset] ->
//        Some {
//            Tag       = tag
//            Device    = device
//            DataType  = DataType.Bit
//            BitOffset = bitOffset }

//    | RegexPattern @"%([IQ])X(\d+).(\d+).(\d+)$"
//        [DevicePattern device; Int32Pattern file; Int32Pattern element; Int32Pattern bit] ->
//        Some {
//            Tag       = tag
//            Device    = device
//            DataType  = DataType.Bit
//            BitOffset = element * 64 + bit }
//    // U 영역은 특수 처리 (서보 및 드라이버)
//    | RegexPattern @"%(U)([XBWDL])(\d+).(\d+).(\d+)$"
//        [DevicePattern device; DataTypePattern dataType; Int32Pattern file; Int32Pattern element; Int32Pattern bit] ->
//        let byteOffset = element * dataType.GetByteLength()
//        let fileOffset = file * 16 * 512  //max %U file.element(16).bit(512)
//        Some {
//            Tag       = tag
//            Device    = device
//            DataType  = dataType
//            BitOffset = fileOffset + byteOffset + bit }
//    // XGI IEC 61131 : byte / word / dword / lword
//    | RegexPattern @"%([IQMLKFWU])([BWDL])(\d+)$"
//        [DevicePattern device; DataTypePattern dataType; Int32Pattern offset;] ->
//        let byteOffset = offset * dataType.GetByteLength()
//        Some {
//            Tag       = tag
//            Device    = device
//            DataType  = dataType
//            BitOffset = byteOffset * 8}
//    | _ ->
//        None

//let (|LsTagPatternXgk|_|) tag =
//    let getBitTag device wordOffset bitOffset = {
//        Tag       = tag
//        Device    = device
//        DataType  = DataType.Bit
//        BitOffset = wordOffset * 16 + bitOffset}
//    let getWordTag device wordOffset = {
//        Tag       = tag
//        Device    = device
//        DataType  = DataType.Word
//        BitOffset = wordOffset * 16}
//    match tag with
//    //word + bit 타입은 word 가 4자리 고정
//    | RegexPattern @"([PMKF])(\d\d\d\d)([\da-fA-F])"
//        [DevicePattern device; Int32Pattern wordOffset; HexPattern bitOffset] ->
//        Some (getBitTag device wordOffset bitOffset)
//    | RegexPattern @"([PMKF])(\d\d\d\d)"
//        [DevicePattern device; Int32Pattern wordOffset;] ->
//        Some (getWordTag device wordOffset)

//    //L타입은 word + bit 타입은 word 가 4자리 고정
//    | RegexPattern @"(L)(\d\d\d\d\d)([\da-fA-F])"
//        [DevicePattern device; Int32Pattern wordOffset; HexPattern bitOffset] ->
//        Some (getBitTag device wordOffset bitOffset)
//    | RegexPattern @"(L)(\d\d\d\d\d)"
//        [DevicePattern device; Int32Pattern wordOffset;] ->
//        Some (getWordTag device wordOffset)

//    //R or D 타입은 word + bit 타입은 '.' 표기로 구분
//    | RegexPattern @"([RD])(\d+)$"
//        [DevicePattern device;  Int32Pattern wordOffset;] ->
//        Some (getWordTag device wordOffset)
//    // word.bit 타입
//    | RegexPattern @"([RD])(\d+)\.([\da-fA-F])"
//        [DevicePattern device; Int32Pattern wordOffset; HexPattern bitOffset] ->
//        Some (getBitTag device wordOffset bitOffset)

//    //S타입 word.bit
//    | RegexPattern @"(S)(\d+)\.(\d+)$"  //마지막 비트 단위가 100인 특수 디바이스
//        [DevicePattern device;  Int32Pattern wordOffset; Int32Pattern bitOffset] ->
//        Some (getBitTag device 0 (wordOffset*100+bitOffset))
//    //U타입 word
//    | RegexPattern @"(U)(\d+)\.(\d+)$"
//        [DevicePattern device;  Int32Pattern wordOffsetA; Int32Pattern wordOffsetB;] ->
//        Some (getWordTag device (wordOffsetA*32+wordOffsetB))
//    //U타입 word.bit 타입
//    | RegexPattern @"(U)(\d+).(\d+)\.([\da-fA-F])$"
//        [DevicePattern device; Int32Pattern wordOffsetA;Int32Pattern wordOffsetB; HexPattern bitOffset] ->
//        Some (getBitTag device 0 (wordOffsetA*16*32 + wordOffsetB*16 + bitOffset))


//    // 수집 전용 타입
//    | RegexPattern @"%([PMLKFWURD])([BWDL])(\d+)$"
//        [DevicePattern device; DataTypePattern dataType; Int32Pattern offset;] ->
//            let byteOffset = offset * dataType.GetByteLength()
//            Some {
//                Tag       = tag
//                Device    = device
//                DataType  = dataType
//                BitOffset = byteOffset * 8}

//    | _ ->
//        None

//let tryParseTag (cpu:CpuType) tag =
//    match (cpu, tag) with
//    | CpuType.Xgk, LsTagPatternXgk x -> Some x
//    | CpuType.XgbMk, LsTagPatternXgk x -> Some x
//    | CpuType.Xgi, LsTagPatternXgi x -> Some x
//    | _ ->
//        //logWarn "Failed to parse tag : %s" tag
//        None

////let tryParseIECTag (tag) =
////    match tag with
////    | RegexPattern @"([PMLKF])(\d\d\d\d)([\da-fA-F])"
////        [DevicePattern device; Int32Pattern offset; HexPattern bitOffset] ->
////        Some (sprintf "%%%sX%d" (device.ToString()) (offset*16 + bitOffset)), Some(DataType.Bit)
////    | RegexPattern @"([PMLKF])(\d+)$"
////        [DevicePattern device; Int32Pattern offset;] ->
////        if(offset > 9999)
////        then  Some (sprintf "%%%sX%d" (device.ToString()) ((offset/10*16)+(offset%16))), Some(DataType.Bit)
////        else  Some (sprintf "%%%sW%d" (device.ToString()) offset), Some(DataType.Word)

////    | RegexPattern @"([RD])(\d+)$"
////        [DevicePattern device;  Int32Pattern offset] ->
////        Some (sprintf "%%%sW%d" (device.ToString()) offset), Some(DataType.Word)
////    | RegexPattern @"([RD])(\d+)\.([\da-fA-F])"
////        [DevicePattern device;  Int32Pattern offset; HexPattern bitOffset] ->
////        Some (sprintf "%%%sW%d.%d" (device.ToString()) offset bitOffset), Some(DataType.Bit)
////    | _ ->
////        //logWarn "Failed to parse tag : %s" tag
////        None, None



///// LS PLC 의 tag 명을 기준으로 data 의 bit 수를 반환
//let getBitSize tag =
//    match tryParseTag cpu tag with
//    |Some v -> v.BitLength
//    |None -> failwithlogf "Cannot getBitSize '%s'" tag

//let getBitOffset tag =
//    match tryParseTag cpu tag with
//    |Some v -> v.BitOffset
//    |None -> failwithlogf "Cannot getBitOffset '%s'" tag

//let getByteSize cpu tag =
//    match tryParseTag cpu tag with
//    |Some v -> v.ByteLength
//    |None -> failwithlogf "Cannot getByteSize '%s'" tag

//let getDataType cpu tag =
//    match tryParseTag cpu tag with
//    |Some v -> v.DataType
//    |None -> failwithlogf "Cannot getDataType '%s'" tag


