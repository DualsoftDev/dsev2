Redis Pub/Sub 및 Hash 기반 key-value 정보 교환 테스트 계획

     현황 분석

     - 기존 Dual.Common.Redis.FS 라이브러리 존재 (pub/sub 기능 구현됨)
     - 현재 Backend 프로젝트에는 기본적인 테스트 구조만 있음
     - StackExchange.Redis, Newtonsoft.Json 등 필요한 의존성들이 이미 준비됨

     구현할 테스트 시나리오

     1. Redis Hash 기능 테스트

     - Hash 데이터 저장/조회 (HSET, HGET, HGETALL)
     - Hash 필드 업데이트 및 삭제 (HDEL)
     - Hash 존재 여부 확인 (HEXISTS)
     - 복합 데이터 구조 저장/조회

     2. Redis Pub/Sub 기능 테스트

     - 기존 RPC 패턴 활용한 요청-응답 테스트
     - 단순 pub/sub 메시지 교환 테스트
     - 다중 채널 구독/발행 테스트
     - 메시지 필터링 및 패턴 매칭 테스트

     3. 통합 시나리오 테스트

     - Hash에 데이터 저장 후 pub/sub으로 알림
     - 실시간 데이터 동기화 시뮬레이션
     - 여러 클라이언트 간 상태 공유

     구현 단계

     1. 프로젝트 설정 업데이트
       - Redis 라이브러리 참조 추가
       - 테스트 실행을 위한 설정 추가
     2. Hash 기능 테스트 구현
       - RedisHashTest.fs 파일에 Hash 관련 테스트 메서드들 추가
       - 기본 CRUD 작업부터 복합 데이터 구조까지 테스트
     3. Pub/Sub 기능 테스트 구현  
       - 기존 RPC 클라이언트/서버 패턴 활용
       - 새로운 단순 pub/sub 테스트 추가
     4. 통합 테스트 및 문서화
       - 실제 사용 사례를 반영한 통합 테스트
       - 테스트 실행 방법 및 결과 확인 방법 문서화

     예상 결과물

     - 완성된 테스트 코드가 포함된 RedisHashTest.fs
     - Redis 설치 및 실행이 전제되는 실행 가능한 테스트
     - 다양한 Redis 기능의 동작 확인 및 성능 측정


Todos
  ⎿  ☐ 프로젝트에 Redis 라이브러리 참조 추가                  
     ☐ Redis Hash 기능 테스트 구현 (HSET, HGET, HGETALL, HDEL)
     ☐ Redis Pub/Sub 기능 테스트 구현 (기존 RPC 패턴 활용)
     ☐ 단순 pub/sub 메시지 교환 테스트 추가
     ☐ Hash + Pub/Sub 통합 시나리오 테스트
     ☐ 테스트 실행 및 결과 확인     