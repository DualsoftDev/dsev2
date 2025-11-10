#!/bin/bash

# Ev2.PLC.Mapper 빌드 검증 스크립트
echo "🔧 Ev2.PLC.Mapper 빌드 검증 시작..."

PROJECT_DIR="/mnt/c/ds/ds_apps/DsDotNet/Apps/DSPLCServer/Ev2.PLC.Mapper"
cd "$PROJECT_DIR"

echo "📂 프로젝트 디렉토리: $PROJECT_DIR"

# 1. 파일 존재 확인
echo "📋 필수 파일 확인..."

REQUIRED_FILES=(
    "Ev2.PLC.Mapper.fsproj"
    "MapperFactory.fs"
    "README.md"
    "Core/Types/ProjectTypes.fs"
    "Core/Types/VariableTypes.fs"
    "Core/Types/MappingTypes.fs"
    "Core/Types/LogicTypes.fs"
    "Core/Types/ValidationTypes.fs"
    "Core/Interfaces/IPlcProgramParser.fs"
    "Core/Interfaces/IVariableAnalyzer.fs"
    "Core/Engine/VariableAnalyzer.fs"
    "Parsers/LSElectric/LSElectricParser.fs"
    "Parsers/AllenBradley/AllenBradleyParser.fs"
    "SampleTest.fs"
    "IMPLEMENTATION_SUMMARY.md"
)

MISSING_FILES=()
for file in "${REQUIRED_FILES[@]}"; do
    if [[ -f "$file" ]]; then
        echo "  ✅ $file"
    else
        echo "  ❌ $file (MISSING)"
        MISSING_FILES+=("$file")
    fi
done

if [[ ${#MISSING_FILES[@]} -gt 0 ]]; then
    echo "⚠️  누락된 파일이 있습니다: ${MISSING_FILES[*]}"
else
    echo "✅ 모든 필수 파일이 존재합니다."
fi

# 2. 프로젝트 파일 구조 검증
echo ""
echo "🏗️ 프로젝트 구조 검증..."

if [[ -f "Ev2.PLC.Mapper.fsproj" ]]; then
    echo "  📄 .fsproj 파일 분석..."
    
    # F# 소스 파일 개수 확인
    FS_FILES_IN_PROJ=$(grep -c '<Compile Include=' Ev2.PLC.Mapper.fsproj || echo "0")
    FS_FILES_ACTUAL=$(find . -name "*.fs" | wc -l)
    
    echo "    프로젝트 파일에 포함된 .fs 파일: $FS_FILES_IN_PROJ"
    echo "    실제 .fs 파일 개수: $FS_FILES_ACTUAL"
    
    if [[ "$FS_FILES_IN_PROJ" -eq "$FS_FILES_ACTUAL" ]]; then
        echo "    ✅ 프로젝트 파일과 실제 파일 개수가 일치합니다."
    else
        echo "    ⚠️  프로젝트 파일과 실제 파일 개수가 다릅니다."
    fi
fi

# 3. 의존성 확인
echo ""
echo "📦 의존성 확인..."

if [[ -f "Ev2.PLC.Mapper.fsproj" ]]; then
    echo "  NuGet 패키지:"
    grep -o 'Include="[^"]*"' Ev2.PLC.Mapper.fsproj | grep PackageReference | sed 's/Include="/    ✅ /' | sed 's/"//'
    
    echo "  프로젝트 참조:"
    grep -o 'Include="[^"]*"' Ev2.PLC.Mapper.fsproj | grep ProjectReference | sed 's/Include="/    🔗 /' | sed 's/"//'
fi

# 4. 컴파일 순서 검증
echo ""
echo "🔄 컴파일 순서 검증..."

# F# 파일들의 종속성 간략 분석
echo "  타입 정의 파일들:"
find Core/Types -name "*.fs" | sort | sed 's/^/    📋 /'

echo "  인터페이스 파일들:"
find Core/Interfaces -name "*.fs" | sort | sed 's/^/    🔌 /'

echo "  구현 파일들:"
find Core/Engine -name "*.fs" | sort | sed 's/^/    ⚙️ /'
find Parsers -name "*.fs" | sort | sed 's/^/    🔧 /'

# 5. 문서 완성도 확인
echo ""
echo "📚 문서 완성도 확인..."

if [[ -f "README.md" ]]; then
    README_LINES=$(wc -l < README.md)
    echo "  📖 README.md: $README_LINES 줄"
    if [[ $README_LINES -gt 200 ]]; then
        echo "    ✅ 상세한 문서가 작성되어 있습니다."
    else
        echo "    ⚠️  문서가 간략할 수 있습니다."
    fi
fi

if [[ -f "IMPLEMENTATION_SUMMARY.md" ]]; then
    SUMMARY_LINES=$(wc -l < IMPLEMENTATION_SUMMARY.md)
    echo "  📋 IMPLEMENTATION_SUMMARY.md: $SUMMARY_LINES 줄"
fi

# 6. 테스트 파일 확인
echo ""
echo "🧪 테스트 구성 확인..."

if [[ -f "SampleTest.fs" ]]; then
    TEST_FUNCTIONS=$(grep -c "let.*Test\|let.*test" SampleTest.fs || echo "0")
    echo "  🧪 SampleTest.fs에서 발견된 테스트 함수: $TEST_FUNCTIONS 개"
fi

# 7. 요약
echo ""
echo "📊 검증 결과 요약:"

TOTAL_FS_FILES=$(find . -name "*.fs" | wc -l)
CORE_FILES=$(find Core -name "*.fs" | wc -l)
PARSER_FILES=$(find Parsers -name "*.fs" | wc -l)

echo "  📁 총 F# 파일: $TOTAL_FS_FILES 개"
echo "  🏗️ 핵심 파일: $CORE_FILES 개"
echo "  🔧 파서 파일: $PARSER_FILES 개"
echo "  📚 문서 파일: $(find . -name "*.md" | wc -l) 개"

if [[ ${#MISSING_FILES[@]} -eq 0 ]]; then
    echo "  🎉 전체적으로 프로젝트 구조가 완성되었습니다!"
    echo ""
    echo "💡 다음 단계 제안:"
    echo "  1. dotnet build 명령으로 컴파일 테스트"
    echo "  2. SampleTest.fs 실행으로 기능 검증"
    echo "  3. 실제 PLC 파일로 통합 테스트"
    echo "  4. Mitsubishi/Siemens 파서 구현"
else
    echo "  ⚠️ 일부 파일이 누락되어 있습니다."
fi

echo ""
echo "🏁 검증 완료!"