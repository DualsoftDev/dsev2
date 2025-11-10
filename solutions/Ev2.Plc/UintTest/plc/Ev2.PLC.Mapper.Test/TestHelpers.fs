namespace Ev2.PLC.Mapper.Test

open System
open System.IO
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Logging.Abstractions

/// Test helper utilities
module TestHelpers =

    /// Create a null logger factory for testing
    let createLoggerFactory() =
        NullLoggerFactory.Instance :> ILoggerFactory

    /// Create a test logger
    let createLogger<'T>() =
        let factory = createLoggerFactory()
        factory.CreateLogger<'T>()

    /// Create a temporary file with content
    let createTempFile (extension: string) (content: string) =
        let tempFile = Path.GetTempFileName() + extension
        File.WriteAllText(tempFile, content)
        tempFile

    /// Clean up a temporary file
    let cleanupTempFile (filePath: string) =
        if File.Exists(filePath) then
            File.Delete(filePath)

    /// Create a temporary directory
    let createTempDirectory() =
        let tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
        Directory.CreateDirectory(tempPath) |> ignore
        tempPath

    /// Clean up a temporary directory
    let cleanupTempDirectory (dirPath: string) =
        if Directory.Exists(dirPath) then
            Directory.Delete(dirPath, true)

    /// Execute with cleanup
    let executeWithCleanup (setup: unit -> 'a) (cleanup: 'a -> unit) (test: 'a -> unit) =
        let resource = setup()
        try
            test resource
        finally
            cleanup resource

    /// Execute async with cleanup
    let executeAsyncWithCleanup (setup: unit -> 'a) (cleanup: 'a -> unit) (test: 'a -> Async<unit>) =
        async {
            let resource = setup()
            try
                do! test resource
            finally
                cleanup resource
        }

    /// Compare two lists ignoring order
    let compareListsIgnoringOrder (list1: 'a list) (list2: 'a list) =
        let sorted1 = list1 |> List.sort
        let sorted2 = list2 |> List.sort
        sorted1 = sorted2

    /// Assert async operation completes within timeout
    let assertAsyncTimeout (timeout: TimeSpan) (operation: Async<'a>) =
        async {
            let! child = Async.StartChild(operation, int timeout.TotalMilliseconds)
            return! child
        }

    /// Create test file with multiple sections
    let createMultiSectionFile (sections: (string * string) list) =
        sections
        |> List.map (fun (header, content) -> sprintf "%s\n%s\n" header content)
        |> String.concat "\n"

    /// Parse key-value pairs from string
    let parseKeyValuePairs (separator: string) (content: string) =
        content.Split([|'\n'|], StringSplitOptions.RemoveEmptyEntries)
        |> Array.choose (fun line ->
            let parts = line.Split([|separator|], StringSplitOptions.None)
            if parts.Length = 2 then
                Some (parts.[0].Trim(), parts.[1].Trim())
            else
                None)
        |> Map.ofArray

    /// Generate random test data
    module Random =
        let private rnd = Random()

        let nextBool() = rnd.Next(2) = 1

        let nextInt(min: int, max: int) = rnd.Next(min, max)

        let nextFloat(min: float, max: float) =
            min + (rnd.NextDouble() * (max - min))

        let nextString(length: int) =
            let chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789"
            String(Array.init length (fun _ -> chars.[rnd.Next(chars.Length)]))

        let nextFromList(list: 'a list) =
            list.[rnd.Next(list.Length)]

        let shuffle(list: 'a list) =
            list
            |> List.map (fun x -> (rnd.Next(), x))
            |> List.sortBy fst
            |> List.map snd

    /// Test file templates
    module Templates =
        let createL5KTemplate (controllerName: string) (rungs: string list) =
            sprintf """CONTROLLER %s (ProcessorType := "1756-L75")
PROGRAM MainProgram
ROUTINE TestRoutine
%s
END_ROUTINE
END_PROGRAM""" controllerName (String.concat "\n" rungs)

        let createXMLTemplate (programName: string) (rungs: string list) =
            sprintf """<?xml version="1.0" encoding="UTF-8"?>
<Project>
    <Program Name="%s">
        %s
    </Program>
</Project>""" programName (String.concat "\n        " rungs)

        let createCSVTemplate (headers: string list) (rows: string list list) =
            let headerLine = String.concat "," headers
            let dataLines = rows |> List.map (String.concat ",")
            headerLine :: dataLines |> String.concat "\n"

    /// Assertion helpers
    module Assert =
        open Xunit

        let shouldBeTrue (value: bool) (message: string) =
            Assert.True(value, message)

        let shouldBeFalse (value: bool) (message: string) =
            Assert.False(value, message)

        let shouldEqual (expected: 'a) (actual: 'a) =
            Assert.Equal(expected, actual)

        let shouldNotEqual (expected: 'a) (actual: 'a) =
            Assert.NotEqual(expected, actual)

        let shouldContain (item: 'a) (collection: 'a seq) =
            Assert.Contains(item, collection)

        let shouldNotContain (item: 'a) (collection: 'a seq) =
            Assert.DoesNotContain(item, collection)

        let shouldBeEmpty (collection: 'a seq) =
            Assert.Empty(collection)

        let shouldNotBeEmpty (collection: 'a seq) =
            Assert.NotEmpty(collection)

        let shouldBeNull (value: obj) =
            Assert.Null(value)

        let shouldNotBeNull (value: obj) =
            Assert.NotNull(value)

        let shouldThrow<'TException when 'TException :> Exception> (action: unit -> unit) =
            Assert.Throws<'TException>(Action(action))

        let shouldNotThrow (action: unit -> unit) =
            try
                action()
                true
            with _ ->
                false
            |> fun success -> Assert.True(success, "Expected no exception to be thrown")