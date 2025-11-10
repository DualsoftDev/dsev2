module Ev2.MxProtocol.Tests.TagDefinitions

open Ev2.MxProtocol.Core

type TestTag = {
    Name: string
    Device: DeviceCode
    Address: int
    DataType: string
    Description: string
}

let testTags = [
    { Name = "M0"; Device = DeviceCode.M; Address = 0; DataType = "Bit"; Description = "Test bit M0" }
    { Name = "M100"; Device = DeviceCode.M; Address = 100; DataType = "Bit"; Description = "Test bit M100" }
    { Name = "Y0"; Device = DeviceCode.Y; Address = 0; DataType = "Bit"; Description = "Output Y0" }
    { Name = "X0"; Device = DeviceCode.X; Address = 0; DataType = "Bit"; Description = "Input X0" }
    { Name = "D0"; Device = DeviceCode.D; Address = 0; DataType = "Word"; Description = "Data register D0" }
    { Name = "D100"; Device = DeviceCode.D; Address = 100; DataType = "Word"; Description = "Data register D100" }
    { Name = "D1000"; Device = DeviceCode.D; Address = 1000; DataType = "Word"; Description = "Data register D1000" }
    { Name = "W0"; Device = DeviceCode.W; Address = 0; DataType = "Word"; Description = "Link register W0" }
    { Name = "R0"; Device = DeviceCode.R; Address = 0; DataType = "Word"; Description = "File register R0" }
    { Name = "T0"; Device = DeviceCode.T; Address = 0; DataType = "Word"; Description = "Timer T0" }
    { Name = "C0"; Device = DeviceCode.C; Address = 0; DataType = "Word"; Description = "Counter C0" }
    { Name = "SM0"; Device = DeviceCode.SM; Address = 0; DataType = "Bit"; Description = "Special relay SM0" }
    { Name = "SD0"; Device = DeviceCode.SD; Address = 0; DataType = "Word"; Description = "Special register SD0" }
]

let getTestTag name =
    testTags |> List.tryFind (fun t -> t.Name = name)

let getBitTags() =
    testTags |> List.filter (fun t -> t.DataType = "Bit")

let getWordTags() =
    testTags |> List.filter (fun t -> t.DataType = "Word")

let createTagRange device startAddress count dataType =
    [0..count-1]
    |> List.map (fun i ->
        {
            Name = $"{device}{startAddress + i}"
            Device = device
            Address = startAddress + i
            DataType = dataType
            Description = $"Test {dataType} {device}{startAddress + i}"
        }
    )