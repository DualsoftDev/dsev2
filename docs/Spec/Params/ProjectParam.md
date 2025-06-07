# ProjectParam 정의

`ProjectParam`은 EV2 모델의 최상위 단위인 `Project`에 대한 메타데이터와 실행 대상 시스템을 명시하는 파라미터 구조입니다.

---

## 📌 타입 정의

```fsharp
type RtProject(prototypeSystems:RtSystem[], activeSystems:RtSystem[], passiveSystems:RtSystem[]) as this =
    inherit RtUnique()
    do
        activeSystems  |> iter (setParentI this)
        passiveSystems |> iter (setParentI this)

    interface IRtProject
    interface IParameterContainer

    // { JSON 용
    /// 마지막 저장 db 에 대한 connection string
    member val Database = getNull<DbProvider>() with get, set // DB 연결 문자열.  JSON 저장시에는 사용하지 않음.  DB 저장시에는 사용됨

    member val Author        = $"{Environment.UserName}@{Environment.UserDomainName}" with get, set
    member val Version       = Version()  with get, set
    //member val LangVersion   = langVersion   |? Version()  with get, set
    //member val EngineVersion = engineVersion |? Version()  with get, set
    member val Description   = nullString with get, set

    member val internal RawActiveSystems    = ResizeArray activeSystems
    member val internal RawPassiveSystems   = ResizeArray passiveSystems
    member val internal RawPrototypeSystems = ResizeArray prototypeSystems

    member x.PrototypeSystems = x.RawPrototypeSystems |> toList
    // { Runtime/DB 용
    member x.ActiveSystems = x.RawActiveSystems |> toList
    member x.PassiveSystems = x.RawPassiveSystems |> toList
    member x.Systems = (x.ActiveSystems @ x.PassiveSystems) |> toList
    // } Runtime/DB 용
```

---

## 🧪 사용 예시

```fsharp
let project:RtProject =
    RtProject.Create(Name = "SmartLine")
    |> tee (fun z ->
        z.Description <- Some "스마트 팩토리 공정 실행 흐름"
        z.Author <- "dualsoft"
        z.Version <- Version(1, 2, 0)
        z.DateTime <- System.DateTime.UtcNow)
```

---

## 💬 비고


- 저장 구조에서는 이 정보가 JSON 직렬화되어 DB 및 AASX 모델에 포함됩니다.
