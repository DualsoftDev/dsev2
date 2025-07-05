pip install jsonschema
jsonschema aas.json -i System.json



# AAS 폴더 구조
- see devdoc/SampleAASFolder/
```
.
|-- [Content_Types].xml              ← MIME 타입 선언 (필수)
|-- _rels
|   `-- .rels                        ← 루트 관계 정의 (필수)
`-- aasx
    |-- _rels
    |   `-- aasx-origin.rels         ← AASX 내부 관계 정의
    |-- aas
    |   `-- aas.aas.xml              ← AAS 메타데이터, 실제 AAS 문서
    `-- aasx-origin                  ← 관계 시작점 역할 (빈 파일 가능)
```

### 동작 흐름 요약
1. Content_Types.xml이 파일 종류 선언
1. _rels/.rels이 aasx-origin을 EntryPoint로 지정
1. aasx-origin은 아무 내용이 없지만 중요
1. aasx/_rels/aasx-origin.rels에서 실제 AAS XML을 가리킴
1. aasx/aas/aas.aas.xml이 실제 AAS 환경 정의


