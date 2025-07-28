# 데이터베이스 제약 조건 설계 가이드

## 개요
데이터베이스 제약 조건 구현 시 TABLE CONSTRAINT vs 별도 INDEX 선택 기준 및 구현 사례

## 제약 조건 구현 방식 비교

### 1. TABLE CONSTRAINT (테이블 내 제약)
```sql
CONSTRAINT table_uniq UNIQUE (column1, column2)
```

#### 장점
- 스키마에서 제약 조건이 명확히 표시됨
- 메타데이터 관리가 용이함
- 일반적으로 더 표준적인 방식

#### 단점
- 조건부 제약 구현 불가능 (전체 행에 적용)
- NULL 값 처리가 제한적
- WHERE 절 조건 사용 불가

### 2. 별도 UNIQUE INDEX (부분 인덱스)
```sql
CREATE UNIQUE INDEX idx_name 
ON table(column1, column2) 
WHERE condition;
```

#### 장점
- 조건부 제약 구현 가능 (WHERE 절)
- NULL 값 제외 가능
- 복잡한 조건 적용 가능
- 공간 효율적 (부분 인덱스)

#### 단점
- 제약 조건이 덜 명확함 (인덱스로만 표시)
- 관리 복잡성 증가

## 구현 사례: ApiDef 테이블

### 사례 1: 단순 UNIQUE 제약
```sql
-- systemId와 name의 조합이 항상 unique해야 함
CONSTRAINT ApiDef_uniq UNIQUE (systemId, name)
```
**선택 이유**: 조건 없이 항상 적용되는 제약이므로 TABLE CONSTRAINT 사용

### 사례 2: 조건부 UNIQUE 제약
```sql
-- CHECK 제약: 둘 다 null이거나 둘 다 non-null이어야 함
CONSTRAINT ApiDef_topic_check CHECK (
    (topicIndex IS NULL AND isTopicOrigin IS NULL) OR 
    (topicIndex IS NOT NULL AND isTopicOrigin IS NOT NULL)
)

-- 부분 인덱스: 둘 다 non-null인 경우에만 unique
CREATE UNIQUE INDEX idx_apidef_topic_uniq
ON ApiDef(systemId, topicIndex, isTopicOrigin)
WHERE topicIndex IS NOT NULL AND isTopicOrigin IS NOT NULL;
```
**선택 이유**: 
- CHECK 제약으로 기본 조건 보장
- 부분 인덱스로 조건부 unique 제약 구현 (TABLE CONSTRAINT로는 불가능)

## 선택 기준

### TABLE CONSTRAINT 선택 시기
- 조건 없이 항상 적용되는 제약
- 스키마 문서화가 중요한 경우
- 표준적인 제약 조건

### 별도 INDEX 선택 시기
- 조건부 제약이 필요한 경우 (추천)
- NULL 값을 제외해야 하는 경우
- 복잡한 WHERE 조건이 필요한 경우
- 공간 효율성이 중요한 경우

## 권장사항

1. **기본적으로 TABLE CONSTRAINT 우선 고려**
2. **조건부 제약이 필요한 경우에만 별도 INDEX 사용**
3. **두 방식을 혼용할 때는 명확한 주석으로 의도 표시**
4. **SQLite와 PostgreSQL 모두 지원하는 구문 사용**

## 데이터베이스별 지원 현황

| 기능 | SQLite | PostgreSQL | 사용 권장 |
|------|--------|------------|-----------|
| TABLE UNIQUE CONSTRAINT | 지원 | 지원 | 권장 |
| 부분 UNIQUE INDEX | 지원 | 지원 | 권장 |
| CHECK CONSTRAINT | 지원 | 지원 | 권장 |
| IF NOT EXISTS (INDEX) | 지원 | 지원 | 권장 |