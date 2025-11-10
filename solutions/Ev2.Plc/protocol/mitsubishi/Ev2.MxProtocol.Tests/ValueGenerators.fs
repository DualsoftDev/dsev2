module Ev2.MxProtocol.Tests.ValueGenerators

open System
open Ev2.MxProtocol.Core
open ProtocolTestHelper.ValueGenerators

type ValuePattern =
    | Constant of uint16
    | Alternating
    | Sequential
    | Random
    | Ramp of start: uint16 * step: uint16
    | Sine of amplitude: float * frequency: float * phase: float

let generateBitPattern pattern count =
    match pattern with
    | Constant value ->
        Patterns.generateBoolPattern (Patterns.Constant (box value)) count
    | Alternating ->
        Patterns.generateBoolPattern Patterns.Alternating count
    | Sequential ->
        Patterns.generateBoolPattern Patterns.Sequential count
    | Random ->
        Patterns.generateBoolPattern Patterns.Random count
    | Ramp (start, step) ->
        Patterns.generateBoolPattern (Patterns.Ramp (int start, int step)) count
    | Sine (amplitude, frequency, phase) ->
        Patterns.generateBoolPattern (Patterns.Sine (amplitude, frequency, phase)) count

let generateWordPattern pattern count =
    match pattern with
    | Constant value ->
        Patterns.generateUInt16Pattern (Patterns.Constant (box value)) count
    | Alternating ->
        Patterns.generateUInt16Pattern Patterns.Alternating count
    | Sequential ->
        Patterns.generateUInt16Pattern Patterns.Sequential count
    | Random ->
        Patterns.generateUInt16Pattern Patterns.Random count
    | Ramp (start, step) ->
        Patterns.generateUInt16Pattern (Patterns.Ramp (int start, int step)) count
    | Sine (amplitude, frequency, phase) ->
        Patterns.generateUInt16Pattern (Patterns.Sine (amplitude, frequency, phase)) count

let createTestData (deviceCode: DeviceCode) address count pattern =
    if deviceCode.IsWordDevice() then
        generateWordPattern pattern count
        |> Array.map (fun v -> [| byte v; byte (v >>> 8) |])
        |> Array.concat
    else
        generateBitPattern pattern count
        |> Array.map (fun b -> if b then 0x01uy else 0x00uy)

let verifyData expected actual tolerance =
    Verification.verifyDataTolerance expected actual tolerance