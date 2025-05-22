# CallParam 정의

`CallParam`은 EV2의 `Call` 노드에서 API 실행 방식과 조건을 제어하는 파라미터 구조입니다.

---

## 📌 타입 정의

```fsharp
type CallParam = {
    CallType: string                // 호출 유형 (예: "Normal", "Parallel", "Repeat")
    Timeout: int                    // 실행 타임아웃(ms)
    AutoPreConditions: string list  // 사전 조건 식 (자동 실행 조건)
    SafetyConditions: string list   // 안전 조건 식 (실행 보호조건)
} with interface IParameter
```

---

## 🧪 사용 예시

```fsharp
let callParam: CallParam = {
    CallType = "Normal"
    Timeout = 1000
    AutoPreConditions = ["x >= 10"; "sensorReady"]
    SafetyConditions = ["not emergency"]
}
```

---

## 💬 비고

- `CallType`은 호출 시점의 실행 방식 지정. (예: `Parallel`은 병렬 실행)
- `Timeout`은 호출 지연을 감지하는 시간 기준이며, 단위는 밀리초입니다.
-`AutoPreConditions`는 실행 직전 자동 평가되는 조건이며, `ValueParam.ToText()` 스타일 문자열이 사용될 수 있습니다.
- `SafetyConditions`는 PLC 또는 시뮬레이터와의 연동 시 강제 실행 제한 역할을 합니다.
- 이 구조는 `Call` 단위의 실행 제어 및 조건 로직 자동화에 활용됩니다.
