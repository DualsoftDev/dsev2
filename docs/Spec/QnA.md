- Works 간 arrow 연결 정보는 system 에서만 가짐.  Flow 에는 arrow 정보 기록하지 않음.

- Project 및 System 관계
    - system 의 모델 정보는 기본적으로 project 에 귀속.  project 삭제시 해당 system 삭제
    - project 에서 public 으로 publish 한 system 에 대해서는 귀속 관계 삭제.  해당 project 삭제하더라도 system 은 존속.
    - 필요시, 수동으로 dangling system 삭제

- System 구분
    - Project local System : 내 project 에서 모델이 정의된 system
        - Active system (Target) : CPU 코드 내릴 대상
        - Passive system : simulation 대상
    - Project non-local system : 내 project 에서 모델이 정의되지 않은 system
        - White system : 내부 모델 정보에 접근 가능한 system.
            - e.g 공용 library system.  DB 상에 publish 된 system.  cylinder, pin 등
        - Black system : 내부 모델 정보에 접근 불가능한 시스템.  API 호출만 허용.  API def 는 없음.
            - e.g API 를 통해 외부 CPU 제어  

```fsharp
type SystemReference =
    | ActiveSystem of Guid
    | PassiveSystem of Guid
```



- ~~Target/Link/Device System 구분 필요.  F# type 수준에서 구분 필요?~~
    - Target : 모델 정보(system/work/flow/call), instance 상태
    - Link: 외부 호출 API 및 instance 상태 필요
    - Device: 호출 API 및 instance 상태

