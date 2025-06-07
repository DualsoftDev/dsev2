# CallParam 정의

`CallParam`은 EV2의 `Call` 노드에서 API 실행 방식과 조건을 제어하는 파라미터 구조입니다.

---

## 📌 타입 정의

```fsharp
// type CallParam = {
//     CallType: string                // 호출 유형 (예: "Normal", "Parallel", "Repeat")
//     Timeout: int                    // 실행 타임아웃(ms)
//     AutoPreConditions: string list  // 사전 조건 식 (자동 실행 조건)
//     SafetyConditions: string list   // 안전 조건 식 (실행 보호조건)
// } with interface IParameter

// TODO: autoPre, safety => 복수개의 string.  이름 변경 AutoCondition, CommonCondition, 
type RtCall(callType:DbCallType, apiCallGuids:Guid seq, autoPre:string, safety:string, isDisabled:bool, timeout:int option) =
    inherit RtUnique()
    interface IRtCall
    member val CallType   = callType   with get, set
    member val AutoPre    = autoPre    with get, set
    member val Safety     = safety     with get, set
    member val IsDisabled = isDisabled with get, set
    member val Timeout    = timeout    with get, set
    member val Status4 = Option<DbStatus4>.None with get, set
    member val ApiCallGuids = ResizeArray apiCallGuids    // DB 저장시에는 callId 로 저장

    member x.Work = x.RawParent >>= tryCast<RtWork>
    member x.ApiCalls =
        let sys = (x.RawParent >>= _.RawParent).Value :?> RtSystem
        sys.ApiCalls |> filter(fun ac -> x.ApiCallGuids |> contains ac.Guid ) |> toList    // DB 저장시에는 callId 로 저장

```

---

## 🧪 사용 예시

```fsharp
// let callParam: CallParam = {
//     CallType = "Normal"
//     Timeout = 1000
//     AutoPreConditions = ["x >= 10"; "sensorReady"]
//     SafetyConditions = ["not emergency"]
// }

let call:RtCall =
    RtCall.Create()
    |> tee(fun z ->
        z.Name     <- "Call1a"
        z.Status4  <- Some DbStatus4.Ready
        z.CallType <- DbCallType.Parallel
        z.AutoPre  <- "AutoPre 테스트 1"
        z.Safety   <- "안전조건1"
        z.Timeout  <- Some 30
        z.Parameter <- {| Type="call"; Count=3; Pi=3.14 |} |> EmJson.ToJson
        z.ApiCallGuids.AddRange [edApiCall1a.Guid] )


```

---

## 💬 비고

- `CallType`은 호출 시점의 실행 방식 지정. (예: `Parallel`은 병렬 실행)
- `Timeout`은 호출 지연을 감지하는 시간 기준이며, 단위는 밀리초입니다.
-`AutoPreConditions`는 실행 직전 자동 평가되는 조건이며, `ValueParam.ToText()` 스타일 문자열이 사용될 수 있습니다.
- `SafetyConditions`는 PLC 또는 시뮬레이터와의 연동 시 강제 실행 제한 역할을 합니다.
- 이 구조는 `Call` 단위의 실행 제어 및 조건 로직 자동화에 활용됩니다.
