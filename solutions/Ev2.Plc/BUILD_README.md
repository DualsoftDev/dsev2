# dsev2plc.sln 빌드 가이드

## 🚀 빠른 시작

Windows 명령 프롬프트에서:

```cmd
cd C:\ds\dsev2cpu\src

# 옵션 1: 간단한 빌드
build_dsev2plc.cmd

# 옵션 2: 오류 자동 수정 시도
quick_fix_common_errors.cmd

# 옵션 3: 상세 분석과 함께 빌드
build_and_fix.cmd
```

## 📋 빌드 스크립트 설명

### 1. **build_dsev2plc.cmd**
- 기본 빌드 스크립트
- 솔루션 전체를 한번에 빌드
- 간단한 성공/실패 보고

### 2. **quick_fix_common_errors.cmd**
- 일반적인 빌드 오류 자동 수정
- 의존성 순서대로 개별 프로젝트 빌드
- bin/obj 폴더 정리, NuGet 캐시 클리어

### 3. **build_and_fix.cmd**
- 상세한 오류 분석
- build_output.txt에 전체 로그 저장
- 특정 오류 패턴 감지 및 해결 방법 제시

## 🔧 수동 빌드 (오류 발생 시)

### 단계별 빌드 순서:

```cmd
# 1. 정리
dotnet clean dsev2plc.sln

# 2. 패키지 복원
dotnet restore dsev2plc.sln

# 3. 핵심 라이브러리 빌드
dotnet build UintTest\plc\Ev2.PLC.ProtocolTestHelper\Ev2.ProtocolTestHelper.fsproj
dotnet build plc\Ev2.PLC.Common.FS\Ev2.PLC.Common.fsproj

# 4. 프로토콜 구현 빌드
dotnet build protocol\ab\Ev2.ABProtocol\Ev2.ABProtocol.fsproj
dotnet build protocol\lselectric\Ev2.LsElectricProtocol\Ev2.LsElectricProtocol.fsproj
dotnet build protocol\mitsubishi\Ev2.MitsubishiProtocol\Ev2.MitsubishiProtocol.fsproj
dotnet build protocol\siemens\Ev2.SiemensProtocol\Ev2.SiemensProtocol.fsproj

# 5. PLC 드라이버 빌드
dotnet build plc\Ev2.PLC.Driver\Ev2.PLC.Driver.fsproj
dotnet build plc\Ev2.PLC.Mapper\Ev2.PLC.Mapper.fsproj
dotnet build plc\Ev2.PLC.Server\Ev2.PLC.Server.fsproj

# 6. 테스트 프로젝트 빌드
dotnet build protocol\mitsubishi\Ev2.MitsubishiProtocol.Tests\Ev2.MitsubishiProtocol.Tests.fsproj
dotnet build protocol\lselectric\Ev2.LsElectricProtocol.Tests\Ev2.LsElectricProtocol.Tests.fsproj

# 7. 전체 솔루션 빌드
dotnet build dsev2plc.sln
```

## ❌ 일반적인 오류 및 해결

### 1. FS0039: 값 또는 생성자가 정의되지 않음
**원인**: F# 컴파일 순서 문제
**해결**: 
- .fsproj 파일에서 `<Compile>` 항목 순서 확인
- 의존성이 있는 파일이 먼저 컴파일되도록 조정

### 2. FS0001: 형식이 일치하지 않음
**원인**: 타입 불일치
**해결**: 
- 에러 메시지의 예상 타입과 실제 타입 확인
- 명시적 타입 어노테이션 추가

### 3. PackageReference 버전 충돌
**원인**: 프로젝트 간 패키지 버전 불일치
**해결**: 
```cmd
dotnet list package --include-transitive
```
모든 프로젝트에서 동일한 버전 사용

### 4. 파일을 찾을 수 없음
**원인**: 프로젝트에 포함되지 않은 파일 참조
**해결**: .fsproj 파일에 누락된 파일 추가

## 📁 빌드 출력 위치

빌드 성공 시 출력 파일 위치:
- **프로토콜 DLL**: `protocol\[protocol]\bin\Debug\net8.0\`
- **테스트 DLL**: `protocol\[protocol]\[TestProject]\bin\Debug\net8.0\`
- **PLC 드라이버**: `plc\Ev2.PLC.Driver\bin\Debug\net8.0\`

## 🧪 빌드 후 테스트

```cmd
# Mitsubishi 프로토콜 테스트
cd protocol\mitsubishi
run_tests.cmd

# LS Electric 프로토콜 테스트
cd protocol\lselectric
dotnet test

# 전체 테스트 실행
cd C:\ds\dsev2cpu\src
dotnet test dsev2plc.sln
```

## 💡 팁

1. **빌드 캐시 문제**: `dotnet build --no-incremental` 사용
2. **상세 로그**: `dotnet build --verbosity detailed`
3. **특정 프레임워크**: `dotnet build --framework net8.0`
4. **Release 빌드**: `dotnet build --configuration Release`

## 🔍 문제 진단

빌드 실패 시:
1. `build_output.txt` 확인 (build_and_fix.cmd 사용 시)
2. 첫 번째 오류부터 해결
3. F# 프로젝트는 컴파일 순서가 중요함을 기억

## 🆘 도움이 필요하면

1. 전체 오류 메시지 캡처:
```cmd
dotnet build dsev2plc.sln > build_errors.txt 2>&1
```

2. 오류 파일 내용을 공유하여 구체적인 해결책 요청