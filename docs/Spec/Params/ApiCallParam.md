# ApiCallParam 정의

`ApiCallParam`은 EV2에서 `ApiCall` 노드가 디바이스와 연동되는 입출력 포트를 정의하는 파라미터 구조입니다.

---

## 📌 타입 정의



```fsharp
type ApiCallParam = {
    InAddress: string            // 디바이스 입력 주소
    OutAddress: string           // 디바이스 출력 주소
    InSymbol: string             // 입력 신호 이름
    OutSymbol: string            // 출력 신호 이름
    Value: ValueParam option     // 값 범위 또는 단일 값 조건 정의 (선택 사항)
} with interface IParameter with interface IParameter
```
### 🔹 ValueParam 타입
```fsharp
type ValueParam = {
    TargetValue: obj option           // 단일 값 기준
    Min: obj option                   // 최소값 (범위 조건일 경우)
    Max: obj option                   // 최대값 (범위 조건일 경우)
    IsNegativeTarget: bool            // 조건 부정 여부 (!값)
    IsInclusiveMin: bool              // 최소값 포함 여부
    IsInclusiveMax: bool              // 최대값 포함 여부
}
```


## 🧪 사용 예시

```fsharp
let apiCallParam: ApiCallParam = {
    InAddress = "M100"
    OutAddress = "M200"
    InSymbol = "SensorReady"
    OutSymbol = "ActuateStart"
    Value = Some {
        TargetValue = Some(box true)
        Min = None
        Max = None
        IsNegativeTarget = false
        IsInclusiveMin = false
        IsInclusiveMax = false
    }
}
```



## 💬 비고

- `Value` 필드를 통해 입력 또는 출력 조건에 대한 정량적 범위 또는 단일 값 기준을 설정할 수 있습니다.
- 예: "x >= 100" 또는 "x = true" 같은 조건을 표현할 수 있으며, 이를 통해 검증, 시뮬레이션 또는 조건부 처리에 활용됩니다.

- `InAddress`와 `OutAddress`는 실제 PLC 주소, OPC 태그 등과 연결되며 디지털 또는 아날로그 신호일 수 있습니다.
- `InSymbol` 및 `OutSymbol`은 UI 및 디버깅 시 직관적으로 사용될 수 있는 이름입니다.
- 아날로그 여부는 `ValueParam.DataType`을 통해 동적으로 결정됩니다. `DuREAL`이나 `DuFLOAT` 등으로 해석될 경우 아날로그로 간주합니다.
- 이 파라미터는 `ApiCall`의 구성 정보로서, 각 디바이스 API의 입출력 접점을 구조화하는 데 활용됩니다.
