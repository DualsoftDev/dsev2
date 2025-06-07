# ApiDefParam 정의

`ApiDefParam`은 EV2의 디바이스 시스템에 정의된 API 동작 정의(`ApiDef`)에 대한 파라미터 구조입니다.

---

## 📌 타입 정의

```fsharp

type RtApiDef(isPush:bool) =
    inherit RtUnique()
    interface IRtApiDef

    member val IsPush = isPush
    member x.System   = x.RawParent >>= tryCast<RtSystem>

    // system 에서 현재 ApiDef 을 사용하는 ApiCall 들
    member x.ApiUsers:RtApiCall[] =
        x.System
        |-> (fun s ->
            s.ApiCalls
            |> filter (fun c -> c.ApiDef = x)
            |> toArray)
        |? [||]

```

---

## 🧪 사용 예시

```fsharp
let apiDef:RtApiDef =
    RtApiDef.Create(Name = "ApiDef1Cyl")
    |> tee(fun z -> z.IsPush <- false)

```

---

## 💬 비고

- `ActionType`은 동작 방식의 모드 설정용 열거형입니다.
  - `ActionNormal`: 일반 동작 (명령 지속형)
  - `Push`: 순간 동작 (펄스 또는 트리거 방식)
- 이는 `ApiCall`이 실제 동작을 실행할 때 어떤 방식으로 제어 신호를 전송할지를 결정하는 데 사용됩니다.
- 대부분의 실린더, 밸브, 릴레이 등에서 `Push`는 짧은 펄스(예: 100ms), `ActionNormal`은 일정 유지 신호를 의미합니다.
- 이 정보는 AASX 구조에서 `ConceptDescription` 또는 `EnumValue` 형태로 변환될 수 있습니다.
