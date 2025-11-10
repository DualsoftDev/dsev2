# ApiCallParam 정의

`ApiCallParam`은 EV2에서 `ApiCall` 노드가 디바이스와 연동되는 입출력 포트를 정의하는 파라미터 구조입니다.

---

## 📌 타입 정의



```fsharp
type RtApiCall(apiDefGuid:Guid, inAddress:string, outAddress:string,
                inSymbol:string, outSymbol:string,
                valueSpec:IValueSpec option
) =
    inherit RtUnique()
    interface IRtApiCall
    member val ApiDefGuid = apiDefGuid  with get, set
    member val InAddress  = inAddress   with get, set
    member val OutAddress = outAddress  with get, set
    member val InSymbol   = inSymbol    with get, set
    member val OutSymbol  = outSymbol   with get, set

    member val ValueSpec = valueSpec with get, set
    ... 중략
```
### 🔹 ValueSpec 타입
- ValueSpec 은 저장되는 값의 type 을 가지며 (e.g int32, int64, double, ...)
- 다음의 형태로 지정할 수 있다.
    1. 하나의 단일 값.   e.g 1
    2. 복수개의 값.  e.g {1, 3, 5}
    3. 범위 값.  e.g 0 < x < 99.  범위에는 (등호를 포함할 수 있는) 부등호가 사용됨
    4. 복수개의 범위 값.  e.g x < 0 || 20 < x < 30 || 50 <= x <= 60 || 90 < x < 100 || x > 1000
- 저장 형태
    1. 프로그램 코드 내에서는 `IValueSpec` type 에 저장되고
    2. DB 에는 JSON string (혹은 jsonb 를 지원하는 관계형 database 에서는 JSONB type) 으로 저장
    3. *.json 파일 저장시에는 JSON 내에 embedding 된 JSON 으로 저장

- [📁 ValueSpec 소스 보기](../../../src/engine/Ev2.Core.FS/ConstEnums.fs)
```fsharp
type BoundType = | Open | Closed
type Bound<'T> = 'T * BoundType

type RangeSegment<'T> = {
    Lower: option<Bound<'T>>
    Upper: option<Bound<'T>>
}

type IValueSpec =
    abstract member Jsonize:   unit -> string
    abstract member Stringify: unit -> string

type ValueSpec<'T> =
    | Single of 'T
    | Multiple of 'T list
    | Ranges of RangeSegment<'T> list   // 단일 or 복수 범위 모두 표현 가능
    with ... // 중략
```


## 🧪 사용 예시

```fsharp
let apiCallParam =
    RtApiCall.Create()
    |> tee (fun z ->
        z.ApiDefGuid <- edApiDef1Cyl.Guid
        z.Name       <- "ApiCall1aCyl"
        z.InAddress  <- "M100"
        z.OutAddress <- "M200"
        z.InSymbol   <- "SensorReady"
        z.OutSymbol  <- "ActuateStart"
        z.ValueSpec <-
            Some <| Multiple [1; 2; 3] )

let valueSpecSingleValue:IValueSpec = Single 3.14156952
let valueSpecMultipleValues:IValueSpec = Multiple [1; 2; 3]
let valueSpecSingleRange:IValueSpec = Ranges [
    { Lower = None; Upper = Some (3.14, Open) } ]
let valueSpecMultipleRange:IValueSpec = Ranges [
    { Lower = None; Upper = Some (3.14, Open) }
    { Lower = Some (5.0, Open); Upper = Some (6.0, Open) }
    { Lower = Some (7.1, Closed); Upper = None }]

valueSpecSingleValue   .ToString() === "x = 3.14156952"
valueSpecMultipleValues.ToString() === "x ∈ {1, 2, 3}"
valueSpecSingleRange   .ToString() === "x < 3.14"
valueSpecMultipleRange .ToString() === "x < 3.14 || 5.0 < x < 6.0 || 7.1 <= x"
```
- 더 자세한 사항은 - [📁 ValueSpec 테스트 소스 보기](../../../src/unit-test/UnitTest.Core/ValueSpec.Test.fs) 참조


## 💬 비고

- `Value` 필드를 통해 입력 또는 출력 조건에 대한 정량적 범위 또는 단일 값 기준을 설정할 수 있습니다.
- 예: "x >= 100" 또는 "x = true" 같은 조건을 표현할 수 있으며, 이를 통해 검증, 시뮬레이션 또는 조건부 처리에 활용됩니다.

- `InAddress`와 `OutAddress`는 실제 PLC 주소, OPC 태그 등과 연결되며 디지털 또는 아날로그 신호일 수 있습니다.
- `InSymbol` 및 `OutSymbol`은 UI 및 디버깅 시 직관적으로 사용될 수 있는 이름입니다.
- 아날로그 여부는 `ValueParam.DataType`을 통해 동적으로 결정됩니다. `DuREAL`이나 `DuFLOAT` 등으로 해석될 경우 아날로그로 간주합니다.
- 이 파라미터는 `ApiCall`의 구성 정보로서, 각 디바이스 API의 입출력 접점을 구조화하는 데 활용됩니다.
