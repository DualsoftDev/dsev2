namespace Ev2.AbProtocol.Client

open System
open System.Text.RegularExpressions
open System.Globalization
open Ev2.AbProtocol.Core

/// ABClient 유틸리티 함수 모듈
[<AutoOpen>]
module ABClientUtil =
    
    // ========================================
    // 정규표현식 패턴
    // ========================================
    
    let private bitSelectorRegex = Regex(@"^(?<base>.+)\[(?<index>\d+)\]\.(?<bit>\d+)$", RegexOptions.Compiled)
    let private indexSelectorRegex = Regex(@"^(?<base>.+)\[(?<index>\d+)\]$", RegexOptions.Compiled)
    let private simpleBitSelectorRegex = Regex(@"^(?<base>.+)\.(?<bit>\d+)$", RegexOptions.Compiled)

    /// Converts a 32-bit floating point value to its integer bit representation (netstandard2.0 fallback).
    let inline private singleToInt32Bits (value: single) =
        BitConverter.ToInt32(BitConverter.GetBytes(value), 0)

    /// Converts an integer bit pattern back to a 32-bit floating point value.
    let inline private int32BitsToSingle (bits: int) =
        BitConverter.ToSingle(BitConverter.GetBytes(bits), 0)

    let inline private bytesOfSingle (value: single) = BitConverter.GetBytes(value)

    // ========================================
    // 태그 이름 파싱
    // ========================================
    
    /// 비트 선택자 파싱 (예: Tag[0].3 또는 Tag.3)
    let tryParseBitSelector (tagName: string) =
        let m = bitSelectorRegex.Match(tagName)
        if m.Success then
            let baseTag = m.Groups.["base"].Value
            let index = int m.Groups.["index"].Value
            let bit = int m.Groups.["bit"].Value
            Some (baseTag, Some index, bit)
        else
            let m2 = simpleBitSelectorRegex.Match(tagName)
            if m2.Success then
                let baseTag = m2.Groups.["base"].Value
                let bit = int m2.Groups.["bit"].Value
                Some (baseTag, None, bit)
            else
                None

    /// 인덱스 선택자 파싱 (예: Tag[0])
    let tryParseIndexer (tagName: string) =
        let m = indexSelectorRegex.Match(tagName)
        if m.Success then
            let baseTag = m.Groups.["base"].Value
            let index = int m.Groups.["index"].Value
            Some (baseTag, index)
        else
            None
    
    /// 태그 이름 유효성 검사
    let isValidTagName (tagName: string) =
        not (String.IsNullOrWhiteSpace(tagName)) &&
        tagName.Length <= 40 &&
        (Char.IsLetter(tagName.[0]) || tagName.[0] = '_')
    
    /// 비트 범위 검증 (타입별)
    let validateBitRange (dataType: DataType) (bit: int) =
        match dataType with
        | BOOL -> bit = 0
        | SINT | USINT -> bit >= 0 && bit < 8
        | INT | UINT -> bit >= 0 && bit < 16
        | DINT | UDINT | REAL -> bit >= 0 && bit < 32
        | LINT | ULINT | LREAL -> bit >= 0 && bit < 64
        | _ -> false
    
    // ========================================
    // 비트 연산
    // ========================================
    
    /// 정수에서 비트 추출
    let bitFromInt value bit = 
        if bit < 0 || bit >= 32 then false
        else ((value >>> bit) &&& 1) = 1
    
    /// 부호없는 32비트 정수에서 비트 추출
    let bitFromUInt (value: uint32) bit = 
        if bit < 0 || bit >= 32 then false
        else bitFromInt (int value) bit
    
    /// 부호없는 16비트 정수에서 비트 추출
    let bitFromUInt16 (value: uint16) bit = 
        if bit < 0 || bit >= 16 then false
        else bitFromInt (int value) bit
    
    /// 부호없는 64비트 정수에서 비트 추출
    let bitFromUInt64 (value: uint64) bit = 
        if bit < 0 || bit >= 64 then false
        else ((value >>> bit) &&& 1UL) = 1UL
    
    /// 정수에 비트 설정
    let setBitInt value bit desired = 
        if bit < 0 || bit >= 32 then value
        else
            let mask = 1 <<< bit
            if desired then value ||| mask else value &&& ~~~mask
    
    /// 부호없는 정수에 비트 설정
    let setBitUInt (value: uint32) bit desired = 
        if bit < 0 || bit >= 32 then value
        else
            let mask = 1u <<< bit
            if desired then value ||| mask else value &&& ~~~mask
    
    /// 64비트 정수에 비트 설정
    let setBitInt64 (value: int64) bit desired =
        if bit < 0 || bit >= 64 then value
        else
            let mask = 1L <<< bit
            if desired then value ||| mask else value &&& ~~~mask
    
    /// 부호없는 64비트 정수에 비트 설정
    let setBitUInt64 (value: uint64) bit desired =
        if bit < 0 || bit >= 64 then value
        else
            let mask = 1UL <<< bit
            if desired then value ||| mask else value &&& ~~~mask
    
    // ========================================
    // 값 추출 헬퍼 (비트 접근 개선)
    // ========================================
    
    /// 객체에서 비트 값 추출 (DINT[N].0~31, LINT[N].0~63 지원)
    let tryExtractBitValue (raw: obj) (bit: int) =
        if bit < 0 then None
        else
            match raw with
            // BOOL 타입
            | :? bool as value -> 
                Some value
        
            // DINT/UDINT/REAL (32비트)
            | :? int32 as value when bit < 32 -> 
                Some (bitFromInt value bit)
            | :? uint32 as value when bit < 32 -> 
                Some (bitFromUInt value bit)
            | :? single as value when bit < 32 ->
                let bits = singleToInt32Bits value
                Some (bitFromInt bits bit)
        
            // LINT/ULINT/LREAL (64비트)
            | :? int64 as value when bit < 64 -> 
                Some (bitFromUInt64 (uint64 value) bit)
            | :? uint64 as value when bit < 64 -> 
                Some (bitFromUInt64 value bit)
            | :? double as value when bit < 64 ->
                let bits = System.BitConverter.DoubleToInt64Bits value
                Some (bitFromUInt64 (uint64 bits) bit)
        
            // INT/UINT (16비트)
            | :? int16 as value when bit < 16 -> 
                Some (bitFromInt (int value) bit)
            | :? uint16 as value when bit < 16 -> 
                Some (bitFromUInt16 value bit)
        
            // SINT/USINT (8비트)
            | :? byte as value when bit < 8 -> 
                Some (bitFromInt (int value) bit)
            | :? sbyte as value when bit < 8 -> 
                Some (bitFromInt (int value) bit)
        
            | _ -> None
    /// 배열에서 단일 요소 추출
    let sliceElement (raw: obj) index =
        if index < 0 then None 
        else 
            match raw with
            | :? (int32[]) as values when index < values.Length -> Some (box [| values.[index] |])
            | :? (uint32[]) as values when index < values.Length -> Some (box [| values.[index] |])
            | :? (int16[]) as values when index < values.Length -> Some (box [| values.[index] |])
            | :? (uint16[]) as values when index < values.Length -> Some (box [| values.[index] |])
            | :? (byte[]) as values when index < values.Length -> Some (box [| values.[index] |])
            | :? (sbyte[]) as values when index < values.Length -> Some (box [| values.[index] |])
            | :? (int64[]) as values when index < values.Length -> Some (box [| values.[index] |])
            | :? (uint64[]) as values when index < values.Length -> Some (box [| values.[index] |])
            | :? (single[]) as values when index < values.Length -> Some (box [| values.[index] |])
            | :? (double[]) as values when index < values.Length -> Some (box [| values.[index] |])
            | :? (bool[]) as values when index < values.Length -> Some (box [| values.[index] |])
            | _ -> None
    
    // ========================================
    // 데이터 변환
    // ========================================
    
    /// 바이트 배열을 값으로 디코딩
    let decodeValue (dataType: DataType) (bytes: byte[]) =
        if bytes.Length = 0 then None else
        try
            let value =
                match dataType with
                | DataType.BOOL -> box (bytes.[0] <> 0uy)
                | DataType.SINT -> box (sbyte bytes.[0])
                | DataType.USINT -> box bytes.[0]
                | DataType.INT -> box (BitConverter.ToInt16(bytes, 0))
                | DataType.UINT -> box (BitConverter.ToUInt16(bytes, 0))
                | DataType.DINT -> box (BitConverter.ToInt32(bytes, 0))
                | DataType.UDINT -> box (BitConverter.ToUInt32(bytes, 0))
                | DataType.REAL -> box (BitConverter.ToSingle(bytes, 0))
                | DataType.LINT -> box (BitConverter.ToInt64(bytes, 0))
                | DataType.ULINT -> box (BitConverter.ToUInt64(bytes, 0))
                | DataType.LREAL -> box (BitConverter.ToDouble(bytes, 0))
                | _ -> null
            if isNull value then None else Some value
        with _ -> None
    
    /// 값을 바이트 배열로 인코딩
    let encodeValue (dataType: DataType) (value: obj) =
        try
            match dataType, value with
            | DataType.BOOL, (:? bool as b) -> Some [| if b then 1uy else 0uy |]
            | DataType.SINT, v -> Some [| byte (Convert.ToSByte(v)) |]
            | DataType.USINT, v -> Some [| Convert.ToByte(v) |]
            | DataType.INT, v -> Some (BitConverter.GetBytes(Convert.ToInt16(v)))
            | DataType.UINT, v -> Some (BitConverter.GetBytes(Convert.ToUInt16(v)))
            | DataType.DINT, v -> Some (BitConverter.GetBytes(Convert.ToInt32(v)))
            | DataType.UDINT, v -> Some (BitConverter.GetBytes(Convert.ToUInt32(v)))
            | DataType.REAL, v ->
                let value: single = Convert.ToSingle(v)
                Some (BitConverter.GetBytes(value))
            | DataType.LINT, v -> Some (BitConverter.GetBytes(Convert.ToInt64(v)))
            | DataType.ULINT, v -> Some (BitConverter.GetBytes(Convert.ToUInt64(v)))
            | DataType.LREAL, v -> Some (BitConverter.GetBytes(Convert.ToDouble(v)))
            | _ -> None
        with _ -> None

    /// 타입 변환 시도
    let tryConvertWithType (targetType: System.Type) (value: obj) : obj option =
        match value with
        | null -> None
        | v when v.GetType() = targetType -> Some v
        | :? IConvertible as convertible ->
            try
                Some (System.Convert.ChangeType(convertible, targetType, CultureInfo.InvariantCulture))
            with _ -> None
        | _ -> None
    
    // ========================================
    // 배열 요소 패킹
    // ========================================
    
    /// 배열 요소 업데이트 및 바이트 배열로 변환
    let packElement (raw:obj) index (value:obj) : (DataType * byte[]) option =
        match raw with
        | :? (int32[]) as src when index < src.Length ->
            match tryConvertWithType typeof<int32> value with
            | Some v ->
                let copy = Array.copy src
                copy.[index] <- unbox<int32> v
                Some (DataType.DINT, copy |> Array.collect (fun v -> BitConverter.GetBytes(v)))
            | None -> None
        | :? (uint32[]) as src when index < src.Length ->
            match tryConvertWithType typeof<uint32> value with
            | Some v ->
                let copy = Array.copy src
                copy.[index] <- unbox<uint32> v
                Some (DataType.UDINT, copy |> Array.collect BitConverter.GetBytes)
            | None -> None
        | :? (int16[]) as src when index < src.Length ->
            match tryConvertWithType typeof<int16> value with
            | Some v ->
                let copy = Array.copy src
                copy.[index] <- unbox<int16> v
                Some (DataType.INT, copy |> Array.collect BitConverter.GetBytes)
            | None -> None
        | :? (uint16[]) as src when index < src.Length ->
            match tryConvertWithType typeof<uint16> value with
            | Some v ->
                let copy = Array.copy src
                copy.[index] <- unbox<uint16> v
                Some (DataType.UINT, copy |> Array.collect BitConverter.GetBytes)
            | None -> None
        | :? (byte[]) as src when index < src.Length ->
            match tryConvertWithType typeof<byte> value with
            | Some v ->
                let copy = Array.copy src
                copy.[index] <- unbox<byte> v
                Some (DataType.USINT, copy)
            | None -> None
        | :? (sbyte[]) as src when index < src.Length ->
            match tryConvertWithType typeof<sbyte> value with
            | Some v ->
                let copy = Array.copy src
                copy.[index] <- unbox<sbyte> v
                Some (DataType.SINT, copy |> Array.map byte)
            | None -> None
        | :? (int64[]) as src when index < src.Length ->
            match tryConvertWithType typeof<int64> value with
            | Some v ->
                let copy = Array.copy src
                copy.[index] <- unbox<int64> v
                Some (DataType.LINT, copy |> Array.collect BitConverter.GetBytes)
            | None -> None
        | :? (uint64[]) as src when index < src.Length ->
            match tryConvertWithType typeof<uint64> value with
            | Some v ->
                let copy = Array.copy src
                copy.[index] <- unbox<uint64> v
                Some (DataType.ULINT, copy |> Array.collect BitConverter.GetBytes)
            | None -> None
        | :? (bool[]) as src when index < src.Length ->
            match tryConvertWithType typeof<bool> value with
            | Some v ->
                let copy = Array.copy src
                copy.[index] <- unbox<bool> v
                Some (DataType.BOOL, copy |> Array.map (fun b -> if b then 1uy else 0uy))
            | None -> None
        | :? (single[]) as src when index < src.Length ->
            match tryConvertWithType typeof<single> value with
            | Some v ->
                let copy = Array.copy src
                copy.[index] <- unbox<single> v
                Some (DataType.REAL, copy |> Array.collect (fun v -> BitConverter.GetBytes(v: single)))
            | None -> None
        | :? (double[]) as src when index < src.Length ->
            match tryConvertWithType typeof<double> value with
            | Some v ->
                let copy = Array.copy src
                copy.[index] <- unbox<double> v
                Some (DataType.LREAL, copy |> Array.collect BitConverter.GetBytes)
            | None -> None
        | _ -> None
    
    // ========================================
    // 비트 쓰기 준비 (DINT[N].0~31, LINT[N].0~63 지원)
    // ========================================
    
    /// 배열 전체에서 특정 인덱스의 비트 쓰기 준비
    let tryPrepareBitWriteFull (raw: obj) (index: int) (bit: int) (desired: bool) : (DataType * byte[]) option =
        if index < 0 || bit < 0 then None
        else
            let inline collectBytes (items: 'a[]) (convert: 'a -> byte[]) =
                items |> Array.collect convert
            match raw with
            // DINT 배열 (32비트) - DINT[N].0~31
            | :? (int32[]) as values when index < values.Length && bit < 32 ->
                let copy = Array.copy values
                copy.[index] <- setBitInt values.[index] bit desired
                Some (DataType.DINT, collectBytes copy BitConverter.GetBytes)
            | :? (uint32[]) as values when index < values.Length && bit < 32 ->
                let copy = Array.copy values
                copy.[index] <- setBitUInt values.[index] bit desired
                Some (DataType.UDINT, collectBytes copy BitConverter.GetBytes)
            | :? (single[]) as values when index < values.Length && bit < 32 ->
                let copy = Array.copy values
                let bits = singleToInt32Bits copy.[index]
                let updated = setBitInt bits bit desired
                copy.[index] <- int32BitsToSingle updated
                Some (DataType.REAL, collectBytes copy (fun v -> BitConverter.GetBytes(v: single)))
            
            // LINT 배열 (64비트) - LINT[N].0~63 중요!
            | :? (int64[]) as values when index < values.Length && bit < 64 ->
                let copy = Array.copy values
                copy.[index] <- setBitInt64 values.[index] bit desired
                Some (DataType.LINT, collectBytes copy BitConverter.GetBytes)
            | :? (uint64[]) as values when index < values.Length && bit < 64 ->
                let copy = Array.copy values
                copy.[index] <- setBitUInt64 values.[index] bit desired
                Some (DataType.ULINT, collectBytes copy BitConverter.GetBytes)
            | :? (double[]) as values when index < values.Length && bit < 64 ->
                let copy = Array.copy values
                let bits = System.BitConverter.DoubleToInt64Bits copy.[index]
                let updated = setBitInt64 bits bit desired
                copy.[index] <- System.BitConverter.Int64BitsToDouble updated
                Some (DataType.LREAL, collectBytes copy BitConverter.GetBytes)
            
            // INT 배열 (16비트)
            | :? (int16[]) as values when index < values.Length && bit < 16 ->
                let copy = Array.copy values
                copy.[index] <- setBitInt (int values.[index]) bit desired |> int16
                Some (DataType.INT, collectBytes copy BitConverter.GetBytes)
            | :? (uint16[]) as values when index < values.Length && bit < 16 ->
                let copy = Array.copy values
                copy.[index] <- setBitInt (int values.[index]) bit desired |> uint16
                Some (DataType.UINT, collectBytes copy BitConverter.GetBytes)
            
            // SINT 배열 (8비트)
            | :? (byte[]) as values when index < values.Length && bit < 8 ->
                let copy = Array.copy values
                copy.[index] <- setBitInt (int copy.[index]) bit desired |> byte
                Some (DataType.USINT, copy)
            | :? (sbyte[]) as values when index < values.Length && bit < 8 ->
                let copy = Array.copy values
                copy.[index] <- setBitInt (int copy.[index]) bit desired |> sbyte
                Some (DataType.SINT, copy |> Array.map byte)
            
            // BOOL 배열 - 직접 비트 배열
            | :? (bool[]) as values when bit < 32 ->
                let chunkCount = (values.Length + 31) / 32
                if index < chunkCount then
                    let chunkStart = index * 32
                    let copy = Array.copy values
                    let targetPos = chunkStart + bit
                    if targetPos < copy.Length then
                        copy.[targetPos] <- desired

                    let mutable chunkValue = 0
                    for offset = 0 to 31 do
                        let pos = chunkStart + offset
                        let bitValue =
                            if pos = targetPos && pos >= copy.Length then desired
                            elif pos < copy.Length then copy.[pos]
                            else false
                        if bitValue then
                            chunkValue <- chunkValue ||| (1 <<< offset)

                    Some (DataType.DINT, BitConverter.GetBytes(chunkValue))
                else None
            | :? (bool[]) as _ -> None
            | _ -> None

    /// 단일 값의 비트 쓰기 준비
    let tryPrepareBitWrite (raw: obj) (bit: int) (desired: bool) : (DataType * byte[]) option =
        if bit < 0 then None
        else
            match raw with
            // DINT (32비트)
            | :? (int32[]) as values when values.Length > 0 && bit < 32 ->
                let next = setBitInt values.[0] bit desired
                Some (DataType.DINT, BitConverter.GetBytes(next))
            | :? (uint32[]) as values when values.Length > 0 && bit < 32 ->
                let next = setBitUInt values.[0] bit desired
                Some (DataType.UDINT, BitConverter.GetBytes(next))
            | :? (single[]) as values when values.Length > 0 && bit < 32 ->
                let bits = singleToInt32Bits values.[0]
                let updated = setBitInt bits bit desired
                let next = int32BitsToSingle updated
                Some (DataType.REAL, BitConverter.GetBytes(next))
            
            // LINT (64비트)
            | :? (int64[]) as values when values.Length > 0 && bit < 64 ->
                let next = setBitInt64 values.[0] bit desired
                Some (DataType.LINT, BitConverter.GetBytes(next))
            | :? (uint64[]) as values when values.Length > 0 && bit < 64 ->
                let next = setBitUInt64 values.[0] bit desired
                Some (DataType.ULINT, BitConverter.GetBytes(next))
            | :? (double[]) as values when values.Length > 0 && bit < 64 ->
                let bits = System.BitConverter.DoubleToInt64Bits values.[0]
                let updated = setBitInt64 bits bit desired
                let next = System.BitConverter.Int64BitsToDouble updated
                Some (DataType.LREAL, BitConverter.GetBytes(next))
            
            // INT (16비트)
            | :? (int16[]) as values when values.Length > 0 && bit < 16 ->
                let next = setBitInt (int values.[0]) bit desired |> int16
                Some (DataType.INT, BitConverter.GetBytes(next))
            | :? (uint16[]) as values when values.Length > 0 && bit < 16 ->
                let next = setBitInt (int values.[0]) bit desired |> uint16
                Some (DataType.UINT, BitConverter.GetBytes(next))
            
            // SINT (8비트)
            | :? (byte[]) as values when values.Length > 0 && bit < 8 ->
                let next = setBitInt (int values.[0]) bit desired |> byte
                Some (DataType.USINT, [| next |])
            | :? (sbyte[]) as values when values.Length > 0 && bit < 8 ->
                let next = setBitInt (int values.[0]) bit desired |> sbyte
                Some (DataType.SINT, [| byte next |])
            
            // BOOL
            | :? (bool[]) as _ -> Some (DataType.BOOL, [| if desired then 1uy else 0uy |])
            | :? bool -> Some (DataType.BOOL, [| if desired then 1uy else 0uy |])
            | _ -> None
    
    // ========================================
    // 패킷 크기 결정 (Unconnected vs Connected)
    // ========================================
    
    /// 최적 패킷 크기 결정
    let determineOptimalPacketSize (useConnected: bool) (plcType: PlcType) =
        if useConnected then
            match plcType with
            | CompactLogix -> 4002
            | ControlLogix -> 4000
            | _ -> 2000
        else
            484  // Unconnected의 안전한 크기
    
    /// 대용량 전송 필요 여부 판단
    let requiresLargeTransfer (totalBytes: int64) (useConnected: bool) =
        let threshold = if useConnected then 3500L else 480L
        totalBytes > threshold
    
    // ========================================
    // 에러 매핑
    // ========================================
    
    /// 경로 에러인지 확인
    let isPathError = function
        | CIPError (status, _) when status = Constants.CIP.StatusPathSegmentError -> true
        | UnknownError msg when msg.IndexOf("Path segment error", StringComparison.OrdinalIgnoreCase) >= 0 -> true
        | _ -> false
    
    /// 타임아웃 에러인지 확인
    let isTimeoutError = function
        | TimeoutError -> true
        | _ -> false
    
    /// 연결 에러인지 확인
    let isConnectionError = function
        | ConnectionError _ | SessionError _ | NetworkError _ -> true
        | _ -> false
