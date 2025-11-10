# 1. Storage

## 1.1. Id, Guid 전제
1. Guid: 모든 대부분의 객체가 생성시 부여 받음
2. Id: Database 에 insert 시 부여 받음


## 1.2. Database
### 1.2.1. 지원 database
1. SQLite
2. Postgresql
3. 향후 필요에 따라 확장
### 1.2.2. Requirements
1. 다중 사용자
2. 다중 Project
3. 다중 purpose
   1. 제어 / 시뮬레이션 / 모니터링 / 3D 연동 등 여러 용도의 project 저장가능
4. 지속성
   1. 요소 삭제 시, 관련된 요소도 깔끔하게 삭제되어야 함
      1. e.g 시스템 삭제시 시스템이 포함하던 flow, work, call 정보 등 계층적으로 삭제
## 1.3. JSON
1. disk 상의 기본 저장 파일 단위
2. 하나의 project 에 대한 정보를 저장

### 1.3.1. Requirements
1. Runtime 정보를 제외한 완전한 정보 포함
   1. Runtime 정보 (e.g RGFH 등의 상태 정보)는 제외 (DB 에만 저장됨)
   2. *.json 파일 내에 참조하는 device 의 type 별로 system 정보가 1번 반드시 완전히 포함될 것 (prototype)
   3. Instance 정보 저장
      1. 여러 instance 정보는, prototype 정보를 이용해서 다음 정보만 따로 저장 (see `ReferenceInstance`)
         1. instance 의 이름
         2. prototype 의 guid: 어떤 prototye 으로부터 만들었는지의 정보 추적용
         3. instance 의 guid
   4. cf. Database 저장 단계에서는 prototype, instance 의 구분 없음.  모두 instance 화
2. Human readable
   1. 불필요한 type 정보 제거
   2. 참조 객체에 대한 추상화 저장 (e.g Guid 등으로 대체)
3. Database 와 offline 상태에서도 가능한 기능들은 동작 해야 함. (e.g 단독 시뮬레이션)

## 1.4. AASX
1. JSON 에 포함되는 정보와 동일한 양의 정보가 AASX 파일의 하나의 submodel 에 저장 (가칭 seq control)
2. 신규 저장: Project 의 정보만으로 aasx 파일 새로 구성 (하나의 submodel 만 생성됨)
3. Injection 저장 : 기존 aasx 파일에 seq control submodel 만 교체해서 저장
4. 읽기 : aasx 파일로부터 seq control submodel 정보만 추출해서 JSON 과 동일한 양의 정보 추출
5. 