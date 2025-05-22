# SystemParam 정의

`SystemParam`은 EV2의 각 시스템(`DsSystem`)에 대한 설정 정보와 실행 정의를 담는 파라미터 구조입니다.

---

## 📌 타입 정의

```fsharp
type SystemParam = {
    LangVersion: string            // 사용 언어 버전
    EngineVersion: string          // 엔진 버전
    Description: string option     // 시스템 설명
    Iri: string option             // AASX용 식별 URI (옵션)
} with interface IParameter
```

---

## 🧪 사용 예시

```fsharp
let systemParam: SystemParam = {
    LangVersion = "1.0.0"
    EngineVersion = "2.1.5"
    Description = Some "로봇 조립 공정 시스템"
    Iri = Some "urn:dualsoft:system:RobotSys"
}
```

---

## 💬 비고

- `LangVersion`과 `EngineVersion`은 실행기 및 DSL 코드 해석기의 버전 정합성을 위해 필요합니다.
- `Iri`는 AASX로 내보낼 때 각 시스템을 글로벌하게 식별할 수 있도록 하는 URI입니다.
- 이 파라미터는 시스템 정의(`DsSystem`) 내 `param` 필드에 포함되어 저장 및 직렬화됩니다.
- 모든 필드는 JSON 또는 DB에 직렬화 가능하며, 필요 시 AAS Submodel의 식별 정보로도 활용됩니다.
