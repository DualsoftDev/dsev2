# SystemParam 정의

`SystemParam`은 EV2의 각 시스템(`DsSystem`)에 대한 설정 정보와 실행 정의를 담는 파라미터 구조입니다.

---

## 📌 타입 정의

```fsharp
    type RtSystem internal(protoGuid:Guid option, flows:RtFlow[], works:RtWork[],
            arrows:RtArrowBetweenWorks[], apiDefs:RtApiDef[], apiCalls:RtApiCall[]
    ) =
        inherit RtUnique()

        (* RtSystem.Name 은 prototype 인 경우, prototype name 을, 아닌 경우 loaded system name 을 의미한다. *)
        interface IParameterContainer
        interface IRtSystem
        member val internal RawFlows    = ResizeArray flows
        member val internal RawWorks    = ResizeArray works
        member val internal RawArrows   = ResizeArray arrows
        member val internal RawApiDefs  = ResizeArray apiDefs
        member val internal RawApiCalls = ResizeArray apiCalls
        /// Origin Guid: 복사 생성시 원본의 Guid.  최초 생성시에는 복사원본이 없으므로 null
        member val OriginGuid = noneGuid with get, set
        member val PrototypeSystemGuid = protoGuid with get, set

        member val IRI           = nullString with get, set
        member val Author        = Environment.UserName with get, set
        member val EngineVersion = Version()  with get, set
        member val LangVersion   = Version()  with get, set
        member val Description   = nullString with get, set

        // serialize 대상 아님
        member x.Project = x.RawParent >>= tryCast<RtProject>

        member x.Flows    = x.RawFlows    |> toList
        member x.Works    = x.RawWorks    |> toList
        member x.Arrows   = x.RawArrows   |> toList
        member x.ApiDefs  = x.RawApiDefs  |> toList
        member x.ApiCalls = x.RawApiCalls |> toList
```

---

## 🧪 사용 예시

```fsharp
let system:RtSystem =
    RtSystem.Create()
    |> tee (fun z ->
        z.Name <- "MainSystem"
        z.Author <- "kwak@dualsoft.com"
        z.LangVersion <- Version(1, 0, 0)
        z.EngineVersion <- Version(2, 1, 5)
        z.Description <- "로봇 조립 공정 시스템"
        z.IRI <- "urn:dualsoft:system:RobotSys" )

```

---

## 💬 비고

- `LangVersion`과 `EngineVersion`은 실행기 및 DSL 코드 해석기의 버전 정합성을 위해 필요합니다.
- `Iri`는 AASX로 내보낼 때 각 시스템을 글로벌하게 식별할 수 있도록 하는 URI입니다.
- 이 파라미터는 시스템 정의(`DsSystem`) 내 `param` 필드에 포함되어 저장 및 직렬화됩니다.
- 모든 필드는 JSON 또는 DB에 직렬화 가능하며, 필요 시 AAS Submodel의 식별 정보로도 활용됩니다.
