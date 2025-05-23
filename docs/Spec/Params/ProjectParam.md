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

}
```

---

## 💬 비고


- 저장 구조에서는 이 정보가 JSON 직렬화되어 DB 및 AASX 모델에 포함됩니다.
