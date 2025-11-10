namespace ProtocolTestHelper

open System

/// Assertion helpers specialised for byte-array comparisons.
[<AutoOpen>]
module BufferAssert =

    /// Throws an informative exception when <paramref name="condition"/> is false.
    let private ensure condition message =
        if not condition then raise (InvalidOperationException(message))

    let private isNullArray (value: byte[]) =
        obj.ReferenceEquals(value, null)

    /// Asserts that two byte arrays are equal with the supplied label.
    let equalWithLabel label (expected: byte[]) (actual: byte[]) =
        if isNullArray expected && isNullArray actual then ()
        elif isNullArray expected || isNullArray actual then
            let reason =
                if isNullArray expected then "Expected buffer is null."
                else "Actual buffer is null."
            raise (InvalidOperationException(reason))
        elif not (expected.Length = actual.Length && Array.forall2 (=) expected actual) then
            let description = HexDump.diff expected actual
            raise (InvalidOperationException($"{label}{Environment.NewLine}{description}"))

    /// Asserts that two byte arrays are equal.  The exception message contains a hex diff.
    let equal expected actual =
        equalWithLabel "Buffer mismatch" expected actual

    /// Asserts that <paramref name="actual"/> begins with <paramref name="prefix"/>.
    let startsWith (prefix: byte[]) (actual: byte[]) =
        ensure (not (isNullArray prefix)) "Prefix must not be null."
        ensure (not (isNullArray actual)) "Actual buffer must not be null."
        ensure (actual.Length >= prefix.Length) "Actual buffer shorter than prefix."
        let mismatch =
            Seq.zip prefix actual
            |> Seq.tryFind (fun (e, a) -> e <> a)
        match mismatch with
        | Some (e, a) ->
            raise (InvalidOperationException(sprintf "Actual buffer does not start with expected prefix (expected 0x%02X, actual 0x%02X)." e a))
        | None -> ()

    /// Asserts that each byte satisfies the predicate (useful for wildcard comparisons).
    let forAll (predicate: byte -> bool) (actual: byte[]) =
        ensure (not (isNullArray actual)) "Buffer must not be null."
        actual
        |> Array.tryFindIndex (predicate >> not)
        |> Option.iter (fun idx ->
            raise (InvalidOperationException(sprintf "Byte at index %d violated predicate (value 0x%02X)." idx actual.[idx])))
