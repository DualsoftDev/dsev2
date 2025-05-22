# WorkParam 정의

`WorkParam`은 EV2의 `Work` 노드 단위에서 동작 조건, 반복 횟수, 완료 상태 등을 제어하기 위한 파라미터 구조입니다.

---

## 📌 타입 정의

```fsharp
type WorkParam = {
    Motion: string                 // 동작 또는 모션 이름 (예: "PickAndPlace")
    Script: string                 // 연결된 스크립트 명 또는 DSL 코드
    DsTime: int * int              // (주기, 지연) 단위: ms
    Finished: bool                 // 완료 여부
    RepeatCount: int              // 반복 횟수
} with interface IParameter
```

---

## 🧪 사용 예시

```fsharp
let workParam: WorkParam = {
    Motion = "PushCylinder"
    Script = "auto_push.fsx"
    DsTime = (500, 50)
    Finished = false
    RepeatCount = 1
}
```

---

## 💬 비고

- `Motion`은 하드웨어 또는 논리 작업의 명시적 이름이며, 시각화 및 문서화 용도로 사용됩니다.
- `Script`는 이 Work 단위에서 실행될 코드(내장 DSL 또는 외부 파일 경로)를 지정합니다.
- `DsTime`은 `(주기, 지연)` 구조로, 반복 실행 시 타이밍 제어용입니다.
- `Finished`는 작업 완료 여부를 수동 또는 외부 트리거로 지정할 수 있습니다.
- `RepeatCount`는 이 Work 단위를 반복 실행할 횟수를 나타내며, `0`이면 무한 반복으로 간주될 수 있습니다.
