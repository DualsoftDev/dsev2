module T.TestHelpers

open System
open System.IO

/// 테스트 데이터 디렉토리 경로
let testDataDir() = Path.Combine(__SOURCE_DIRECTORY__, @"test-data")

/// 테스트 메서드 이름과 타임스탬프를 기반으로 고유한 파일 경로 생성
let getUniqueTestPath (testName: string) (extension: string) =
    let timestamp = DateTime.Now.Ticks
    let fileName = sprintf "%s_%d%s" testName timestamp extension
    Path.Combine(testDataDir(), fileName)

/// 테스트 메서드 이름과 GUID를 기반으로 고유한 파일 경로 생성 (더 짧은 이름)
let getUniqueTestPathWithGuid (testName: string) (extension: string) =
    let guid = Guid.NewGuid().ToString("N").Substring(0, 8)
    let fileName = sprintf "%s_%s%s" testName guid extension
    Path.Combine(testDataDir(), fileName)

/// 타임스탬프 기반 고유 파일 경로 생성
let getUniquePathByTime (extension: string) =
    let timestamp = DateTime.Now.Ticks
    let fileName = sprintf "test_%d%s" timestamp extension
    Path.Combine(testDataDir(), fileName)

/// GUID 기반 고유 파일 경로 생성
let getUniquePathByGuid (extension: string) =
    let guid = Guid.NewGuid().ToString("N").Substring(0, 8)
    let fileName = sprintf "test_%s%s" guid extension
    Path.Combine(testDataDir(), fileName)

/// 테스트별 고유 SQLite DB 경로 생성
let getUniqueSqlitePath () =
    getUniquePathByGuid ".sqlite3"

/// 테스트별 고유 AASX 파일 경로 생성
let getUniqueAasxPath () =
    getUniquePathByGuid ".aasx"

/// 테스트별 고유 JSON 파일 경로 생성
let getUniqueJsonPath () =
    getUniquePathByGuid ".json"

/// 테스트 종료 시 생성된 임시 파일 정리
let cleanupTestFile (filePath: string) =
    try
        if File.Exists filePath then
            File.Delete filePath
    with
    | _ -> () // 삭제 실패 시 무시

/// 테스트 종료 시 생성된 임시 파일들 정리
let cleanupTestFiles (filePaths: string list) =
    filePaths |> List.iter cleanupTestFile