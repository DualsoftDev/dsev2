module Ev2.MxProtocol.Tests.Integration.PerformanceTests

open System
open System.Diagnostics
open Xunit
open Ev2.MxProtocol.Core
open Ev2.MxProtocol.Client
open Ev2.MxProtocol.Tests.TestHelpers
open Ev2.MxProtocol.Tests.TestAttributes
open Ev2.MxProtocol.Tests.ClientHelpers
open Ev2.MxProtocol.Protocol
open Ev2.MxProtocol.Tests.ValueGenerators
open Ev2.MxProtocol.Tests

[<Category(TestCategory.Performance)>]
[<RequiresMelsecPLC>]
let ``Measure single word read performance`` () =
    withConnectedClient (fun client ->
        let iterations = 1000
        let sw = Stopwatch.StartNew()
        
        for i in 1..iterations do
            match client.ReadWords(DeviceCode.D, 0, 1) with
            | Ok _ -> ()
            | Error msg -> failWithLogs $"Read failed: {msg}"
        
        sw.Stop()
        let avgMs = sw.Elapsed.TotalMilliseconds / float iterations
        
        Assert.True(avgMs < 100.0, $"Average read time {avgMs}ms exceeds threshold")
        printfn $"Single word read: {avgMs:F2}ms average ({iterations} iterations)"
    )

[<Category(TestCategory.Performance)>]
[<RequiresMelsecPLC>]
let ``Measure bulk read performance`` () =
    withConnectedClient (fun client ->
        let iterations = 100
        let wordsPerRead = 960 // Maximum typical size
        let sw = Stopwatch.StartNew()
        
        for i in 1..iterations do
            match client.ReadWords(DeviceCode.D, 0, wordsPerRead) with
            | Ok _ -> ()
            | Error msg -> failWithLogs $"Read failed: {msg}"
        
        sw.Stop()
        let totalWords = iterations * wordsPerRead
        let wordsPerSecond = float totalWords / sw.Elapsed.TotalSeconds
        
        Assert.True(wordsPerSecond > 1000.0, $"Throughput {wordsPerSecond} words/sec below threshold")
        printfn $"Bulk read: {wordsPerSecond:F0} words/sec ({totalWords} words in {sw.Elapsed.TotalSeconds:F2}s)"
    )

[<Category(TestCategory.Performance)>]
[<RequiresMelsecPLC>]
let ``Measure write performance`` () =
    withConnectedClient (fun client ->
        let iterations = 100
        let testData = generateWordPattern ValuePattern.Sequential 100
        let sw = Stopwatch.StartNew()
        
        for i in 1..iterations do
            match client.WriteWords(DeviceCode.D, 1000, testData) with
            | Ok _ -> ()
            | Error msg -> failWithLogs $"Write failed: {msg}"
        
        sw.Stop()
        let avgMs = sw.Elapsed.TotalMilliseconds / float iterations
        
        Assert.True(avgMs < 200.0, $"Average write time {avgMs}ms exceeds threshold")
        printfn $"Bulk write: {avgMs:F2}ms average for {testData.Length} words"
    )

