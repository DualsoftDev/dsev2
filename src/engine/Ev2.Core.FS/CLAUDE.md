# EV2.Core.FS

## RuntimeType, NJType, ORMType 에 대한 이해
  - RuntimeType: Project, DsSystem, Flow, Work, Call, ArrowXXX, Button, Lamp, ...\
    - Runtime 에 사용될 Type 들
  - NjType: NjProject, NjSystem, NjFlow, ...
    - Newtonsoft Json serialize 를 위한 type 들
  - ORMType: ORMProject, ...
    - Database CRUD 를 위한 type 들

  - Third party 확장 용 type: 예시 --> Hmc.Aas/Hmc.Aas.csproj
    - CustomProject, CustomSystem, ...
    - Engine core (Ev2.Core.FS, Ev2.Aas.FS) 에서는 third party class 에 대해서 몰라야 한다.

  - 각 type 들은 서로 대응 관계에 있다.  e.g Project <--> NjProject,   Project <--> ORMProject
  - 각 type 들은 계층적 구조를 표현한다.  Project > DsSystem (> Flow) > Work > Call > ..

### 시나리오
  - Runtime 객체 <--> NjType 변환을 통해 Json 파일 serialize/deserialize
  - Runtime 객체 <--> ORMType 변환을 통해 Database 에서 CRUD
  - NjType 을 이용해서 AASX 파일로 저장
  - 
