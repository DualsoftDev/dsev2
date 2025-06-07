# WorkParam 정의

`WorkParam`은 EV2의 `Work` 노드 단위에서 동작 조건, 반복 횟수, 완료 상태 등을 제어하기 위한 파라미터 구조입니다.

---

## 📌 타입 정의

```fsharp
    type RtWork internal(calls:RtCall seq, arrows:RtArrowBetweenCalls seq, flow:RtFlow option) as this =
        inherit RtUnique()
        do
            calls  |> iter (setParentI this)
            arrows |> iter (setParentI this)

        interface IRtWork
        member val internal RawCalls  = ResizeArray calls
        member val internal RawArrows = ResizeArray arrows
        member val Flow = flow with get, set

        member val Motion     = nullString with get, set
        member val Script     = nullString with get, set
        member val IsFinished = false      with get, set
        member val NumRepeat  = 0          with get, set
        member val Period     = 0          with get, set
        member val Delay      = 0          with get, set

        member val Status4 = Option<DbStatus4>.None with get, set

        member x.Calls  = x.RawCalls  |> toList
        member x.Arrows = x.RawArrows |> toList
        member x.System = x.RawParent >>= tryCast<RtSystem>
```

---

## 🧪 사용 예시

```fsharp
let work:RtWork =
    RtWork.Create()
    |> tee (fun z ->
        z.Name    <- "BoundedWork1"
        z.Status4 <- Some DbStatus4.Ready
        z.Motion  <- "PushCylinder"
        z.Script  <- "auto_push.fsx"
        z.NumRepeat  <- 1
        z.IsFinished = false
        z.Period  <- 500    // ms
        z.Delay   <- 50     // ms
        z.Parameter <- {| Name="kwak"; Company="dualsoft"; Room=510 |} |> EmJson.ToJson)

```

---

## 💬 비고

- `Motion`은 하드웨어 또는 논리 작업의 명시적 이름이며, 시각화 및 문서화 용도로 사용됩니다.
- `Script`는 이 Work 단위에서 실행될 코드(내장 DSL 또는 외부 파일 경로)를 지정합니다.
- `DsTime`은 `(주기, 지연)` 구조로, 반복 실행 시 타이밍 제어용입니다.
- `Finished`는 작업 완료 여부를 수동 또는 외부 트리거로 지정할 수 있습니다.
- `RepeatCount`는 이 Work 단위를 반복 실행할 횟수를 나타내며, `0`이면 무한 반복으로 간주될 수 있습니다.
