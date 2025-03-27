open Dual.Common.AppSettings
open Dual.Common.Core.FS
open Dual.Plc2DS
open System
open System.IO
open Dual.Common.Base

// For more information see https://aka.ms/fsharp-console-apps
printfn "Hello from F#"

ModuleInitializer.Initialize()

let appSettingsPath =
    let baseDir = AppDomain.CurrentDomain.BaseDirectory;
    Path.Combine(baseDir, "appsettings.json")
//let appSettings = JsonSetting.GetSectionEx<AppSettings>(appSettingsPath, "AppSettings");
let appSettings = EmJson.FromJson<SemanticSettings>(File.ReadAllText(appSettingsPath));


open System
open System.Text.RegularExpressions
open System.Diagnostics

// 테스트 데이터 생성
let testString = "The number 1234 appears in the middle of this sentence. Another number is 5678."
let pattern = @"^\D+(\d+)\D+(\d+).*$"

// 정규식 객체 생성
let regexNormal = new Regex(pattern, RegexOptions.None)
let regexCompiled = new Regex(pattern, RegexOptions.Compiled)

// 실행 시간 측정 함수
let measureTime f =
    let sw = Stopwatch.StartNew()
    f ()
    sw.Stop()
    sw.ElapsedMilliseconds

// 테스트 실행 횟수
let iterations = 10_000_000

// 일반 정규식 속도 측정
let normalTime = measureTime (fun () ->
    for _ in 1 .. iterations do
        ignore (regexNormal.Match(testString))
)

// 컴파일된 정규식 속도 측정
let compiledTime = measureTime (fun () ->
    for _ in 1 .. iterations do
        ignore (regexCompiled.Match(testString))
)

let xxx = regexCompiled.Match(testString)

// 결과 출력
printfn "Normal Regex Time: %d ms" normalTime
printfn "Compiled Regex Time: %d ms" compiledTime
printfn "Performance Improvement: %.2fx faster" (float normalTime / float compiledTime)




noop()