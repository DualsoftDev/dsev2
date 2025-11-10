namespace ProtocolTestHelper

open System

/// Shared helpers for reading protocol test environment configuration.
[<AutoOpen>]
module TestEnvironment =

    let private tryGet (names: string list) (parser: string -> 'a option) =
        names
        |> List.tryPick (fun name ->
            match Environment.GetEnvironmentVariable(name) with
            | null
            | "" -> None
            | value -> parser value)

    /// Retrieves the first environment variable that contains a non-empty value.
    let getString (names: string list) (fallback: string) =
        tryGet names (fun value -> Some value)
        |> Option.defaultValue fallback

    /// Retrieves and parses an environment variable as an integer.
    let getInt (names: string list) (fallback: int) =
        tryGet names (fun value ->
            match Int32.TryParse value with
            | true, parsed -> Some parsed
            | _ -> None)
        |> Option.defaultValue fallback

    /// Retrieves and parses an environment variable as a byte.
    let getByte (names: string list) (fallback: byte) =
        tryGet names (fun value ->
            match Byte.TryParse value with
            | true, parsed -> Some parsed
            | _ ->
                match Int32.TryParse value with
                | true, parsed when parsed >= 0 && parsed <= 255 -> Some (byte parsed)
                | _ -> None)
        |> Option.defaultValue fallback

    /// Retrieves and parses an environment variable as a boolean.
    let getBool (names: string list) (fallback: bool) =
        let parseBool (value: string) =
            match Boolean.TryParse value with
            | true, parsed -> Some parsed
            | _ ->
                match value.Trim().ToLowerInvariant() with
                | "1"
                | "yes"
                | "y" -> Some true
                | "0"
                | "no"
                | "n" -> Some false
                | _ -> None
        tryGet names parseBool |> Option.defaultValue fallback

    /// Produces a dictionary of variables (useful for diagnostics).
    let snapshot (names: string list) =
        names
        |> List.map (fun name ->
            name,
            match Environment.GetEnvironmentVariable(name) with
            | null -> ""
            | value -> value)
        |> dict
