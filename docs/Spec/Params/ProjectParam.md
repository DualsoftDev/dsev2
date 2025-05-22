# ProjectParam 정의

`ProjectParam`은 EV2 모델의 최상위 단위인 `Project`에 대한 메타데이터와 실행 대상 시스템을 명시하는 파라미터 구조입니다.

---

## 📌 타입 정의

```fsharp
type ProjectParam = {
    Name: string                     // 프로젝트 이름
    Version: string                  // 버전 정보 (예: "1.0.0")
    Description: string option       // 설명 (선택)
    Author: string option            // 작성자 정보 (선택)
    CreatedAt: System.DateTime       // 생성 시간
    TargetSystemIDs: string list     // 제어 대상 시스템 GUID 목록
    LinkSystemIDs: string list       // 외부 시스템 참조 (링크된 시스템 GUID 목록)
} with interface IParameter
```

---

## 🧪 사용 예시

```fsharp
let exampleParam: ProjectParam = {
    Name = "SmartLine"
    Version = "1.2.0"
    Description = Some "스마트 팩토리 공정 실행 흐름"
    Author = Some "dualsoft"
    CreatedAt = System.DateTime.UtcNow
    TargetSystemIDs = ["guidXXXX1"; "guidXXXX2"]
    LinkSystemIDs = ["guidYYYY1"; "guidYYYY2"]
}
```

---

## 💬 비고

- `TargetSystemIDs`는 프로젝트 내에서 **제어 코드가 생성되는 주요 시스템**을 지정합니다.
- `LinkSystemIDs`는 외부 프로젝트 또는 공용 정의된 시스템을 **읽기 전용으로 참조**할 때 사용합니다.
- ID는 일반적으로 GUID 기반이며, UI에서는 Name 매핑과 함께 사용됩니다.
- 저장 구조에서는 이 정보가 JSON 직렬화되어 DB 및 AASX 모델에 포함됩니다.
