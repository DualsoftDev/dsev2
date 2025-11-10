namespace ProtocolTestHelper

open System
open System.Text

/// Common value generators that can be shared across all protocols
module ValueGenerators =
    
    /// Basic primitive value generators
    module Primitives =
        let private rng = Random()
        
        let nextBool () = rng.Next(2) = 1
        let nextByte () = byte (rng.Next(0, 256))
        let nextSByte () = sbyte (rng.Next(-128, 128))
        let nextInt16 () = int16 (rng.Next(int Int16.MinValue, int Int16.MaxValue + 1))
        let nextUInt16 () = uint16 (rng.Next(0, int UInt16.MaxValue + 1))
        let nextInt32 () = rng.Next()
        let nextUInt32 () = uint32 (rng.Next(0, Int32.MaxValue))
        
        let nextInt64 () =
            let bytes = Array.zeroCreate<byte> 8
            rng.NextBytes bytes
            BitConverter.ToInt64(bytes, 0)
        
        let nextUInt64 () =
            let bytes = Array.zeroCreate<byte> 8
            rng.NextBytes bytes
            BitConverter.ToUInt64(bytes, 0)
        
        let nextSingle () = single (rng.NextDouble() * 2000.0 - 1000.0)
        let nextDouble () = rng.NextDouble() * 2000.0 - 1000.0
        
        let nextString (maxLen: int) =
            let length = Math.Max(1, Math.Min(maxLen, 16))
            let sb = StringBuilder(length)
            for _ in 1 .. length do
                sb.Append(char (rng.Next(65, 90))) |> ignore
            sb.ToString()
    
    /// Pattern-based value generators for arrays
    module Patterns =
        
        type ValuePattern =
            | Constant of obj
            | Alternating
            | Sequential
            | Random
            | Ramp of start: int * step: int
            | Sine of amplitude: float * frequency: float * phase: float
        
        let generateBoolPattern (pattern: ValuePattern) (count: int) =
            match pattern with
            | Constant value ->
                let boolVal = Convert.ToBoolean(value)
                Array.init count (fun _ -> boolVal)
            | Alternating ->
                Array.init count (fun i -> i % 2 = 0)
            | Sequential ->
                Array.init count (fun i -> i % 8 < 4)
            | Random ->
                Array.init count (fun _ -> Primitives.nextBool())
            | Ramp (start, step) ->
                Array.init count (fun i -> ((start + step * i) % 2) = 1)
            | Sine (amplitude, frequency, phase) ->
                Array.init count (fun i ->
                    let value = amplitude * sin(frequency * float i + phase)
                    value >= 0.0)
        
        let generateUInt16Pattern (pattern: ValuePattern) (count: int) =
            match pattern with
            | Constant value ->
                let uint16Val = Convert.ToUInt16(value)
                Array.init count (fun _ -> uint16Val)
            | Alternating ->
                Array.init count (fun i -> if i % 2 = 0 then 0xFFFFus else 0x0000us)
            | Sequential ->
                Array.init count (fun i -> uint16 i)
            | Random ->
                Array.init count (fun _ -> Primitives.nextUInt16())
            | Ramp (start, step) ->
                Array.init count (fun i -> uint16 (start + step * i))
            | Sine (amplitude, frequency, phase) ->
                Array.init count (fun i ->
                    let value = amplitude * sin(frequency * float i + phase)
                    uint16 (abs(value) * 32767.0))
        
        let generateInt32Pattern (pattern: ValuePattern) (count: int) =
            match pattern with
            | Constant value ->
                let int32Val = Convert.ToInt32(value)
                Array.init count (fun _ -> int32Val)
            | Alternating ->
                Array.init count (fun i -> if i % 2 = 0 then Int32.MaxValue else Int32.MinValue)
            | Sequential ->
                Array.init count (fun i -> i)
            | Random ->
                Array.init count (fun _ -> Primitives.nextInt32())
            | Ramp (start, step) ->
                Array.init count (fun i -> start + step * i)
            | Sine (amplitude, frequency, phase) ->
                Array.init count (fun i ->
                    let value = amplitude * sin(frequency * float i + phase)
                    int (value * 1000000.0))
    
    /// Type-based value generation
    module TypeBased =
        
        let generateValueForType (valueType: Type) =
            match valueType with
            | t when t = typeof<bool> -> box (Primitives.nextBool())
            | t when t = typeof<byte> -> box (Primitives.nextByte())
            | t when t = typeof<sbyte> -> box (Primitives.nextSByte())
            | t when t = typeof<int16> -> box (Primitives.nextInt16())
            | t when t = typeof<uint16> -> box (Primitives.nextUInt16())
            | t when t = typeof<int32> -> box (Primitives.nextInt32())
            | t when t = typeof<uint32> -> box (Primitives.nextUInt32())
            | t when t = typeof<int64> -> box (Primitives.nextInt64())
            | t when t = typeof<uint64> -> box (Primitives.nextUInt64())
            | t when t = typeof<single> -> box (Primitives.nextSingle())
            | t when t = typeof<double> -> box (Primitives.nextDouble())
            | t when t = typeof<string> -> box (Primitives.nextString(16))
            | _ -> Unchecked.defaultof<obj>
        
        let generateArrayForType (valueType: Type) (count: int) =
            Array.init count (fun _ -> generateValueForType valueType)
    
    /// Random data generators with specific characteristics
    module Random =
        let private rng = Random()
        
        let createRandomBools (count: int) =
            Array.init count (fun _ -> Primitives.nextBool())
        
        let createRandomBytes (count: int) =
            let bytes = Array.zeroCreate count
            rng.NextBytes(bytes)
            bytes
        
        let createRandomWords (count: int) =
            Array.init count (fun _ -> Primitives.nextUInt16())
        
        let createRandomInts (count: int) =
            Array.init count (fun _ -> Primitives.nextInt32())
        
        let createRandomFloats (count: int) =
            Array.init count (fun _ -> Primitives.nextSingle())
    
    /// Common test value sets for different protocols
    module CommonTestValues =
        
        let boolValues = [| true; false |]
        let byteValues = [| 0uy; 1uy; 127uy; 255uy |]
        let wordValues = [| 0us; 1us; 32767us; 65535us |]
        let dwordValues = [| 0u; 1u; 2147483647u; 4294967295u |]
        let intValues = [| Int32.MinValue; -1; 0; 1; Int32.MaxValue |]
        let floatValues = [| -1000.0f; -1.0f; 0.0f; 1.0f; 1000.0f |]
        let stringValues = [| ""; "A"; "Test"; "TestString123" |]
    
    /// Data verification helpers
    module Verification =
        
        let verifyDataTolerance (expected: byte[]) (actual: byte[]) (tolerance: int) =
            if Array.length expected <> Array.length actual then
                Error $"Length mismatch: expected {Array.length expected}, actual {Array.length actual}"
            else
                let mismatchCount = 
                    Array.zip expected actual
                    |> Array.filter (fun (e, a) -> abs(int e - int a) > tolerance)
                    |> Array.length
                
                if mismatchCount = 0 then
                    Ok ()
                else
                    Error $"{mismatchCount} values differ by more than tolerance {tolerance}"
        
        let verifyDataExact (expected: 'T[]) (actual: 'T[]) =
            if Array.length expected <> Array.length actual then
                Error $"Length mismatch: expected {Array.length expected}, actual {Array.length actual}"
            else
                let mismatches = 
                    Array.zip expected actual
                    |> Array.mapi (fun i (e, a) -> if e.Equals(a) then None else Some(i, e, a))
                    |> Array.choose id
                
                if Array.isEmpty mismatches then
                    Ok ()
                else
                    let errorMsg = 
                        mismatches
                        |> Array.take (min 5 mismatches.Length)
                        |> Array.map (fun (i, e, a) -> sprintf "[%d]: expected %A, got %A" i e a)
                        |> String.concat "; "
                    Error $"{mismatches.Length} mismatches found: {errorMsg}"