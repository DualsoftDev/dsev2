# FlowParam 정의

`FlowParam`은 EV2의 `Flow` 또는 `Work` 그룹에서 사용되는 제어 항목(버튼, 램프, 조건, 액션)의 정의 목록을 담는 파라미터 구조입니다.

---

## 📌 타입 정의

```fsharp
type FlowParam = {
    ButtonDefs: ButtonDef list          // 버튼 정의
    LampDefs: LampDef list              // 램프 정의
    ConditionDefs: ConditionDef list    // 조건 정의
    ActionDefs: ActionDef list          // 액션 정의
} with interface IParameter
```

---

## 🧪 사용 예시

```fsharp
let flowParam: FlowParam = {
    ButtonDefs = [ ButtonDef.CreateAutoSelect(); ButtonDef.CreateManualStart() ]
    LampDefs = [ LampDef.CreateReadyLamp() ]
    ConditionDefs = [ ConditionDef.Create("x > 100") ]
    ActionDefs = [ ActionDef.Create("OpenValve") ]
}
```

---

## 💬 비고

- 버튼, 램프, 조건, 액션은 UI 기반 시나리오에서 주로 사용되며, 실시간 입력/출력 처리에 대응합니다.
- 각 `Def` 타입은 별도의 구조로 정의되어 있으며, 시각화 및 논리 연결의 기준 역할을 합니다.
- 이 구조는 `Work`, `Flow`, 또는 UI Panel에 종속된 하드웨어 논리 구성을 구조화하는 데 활용됩니다.
