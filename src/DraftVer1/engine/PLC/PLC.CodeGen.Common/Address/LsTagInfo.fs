[<AutoOpen>]
module LsTagInfoImpl

open System
open System.IO
open System.Collections.Generic


type LsTagInfo =
    {
        /// Original Tag name
        Tag: string
        Device: DeviceType
        DataType: PLCHwModel.DataType
        BitOffset: int
    }

    member private x.DataSizeByte = (max 8 x.DataSize) / 8
    member private x.DataSize = getBitLength x.DataType

    member x.ByteOffset = x.BitOffset / 8
    member x.OffsetByDataType =
        match x.DataType with
        | Bit -> x.BitOffset
        | Byte -> x.BitOffset / 8
        | Word -> x.BitOffset / 16
        | DWord -> x.BitOffset / 32
        | LWord -> x.BitOffset / 64
        | Continuous -> failwithf $"error Continuous tag :{x.Tag}"

    static member Create(tag: string, (device: DeviceType), dataType, bitOffset: int, modelId: int option) =
        if
            modelId.IsSome //None 이면 체크 생략
        then
            match HwModelManager.GetCPUInfosByID(modelId.Value) with
            | Some(devs) ->
                let sizeWord =
                    devs
                    |> Seq.find (fun r -> r.strDevice = (device |>toDeviceText))
                    |> fun f -> f.nSizeWord

                if bitOffset / 16 > sizeWord then
                    failwithf $"size over tag:{tag}, device:{device}{bitOffset}, dataType{dataType}"

            | None -> ()


        { Tag = tag
          Device = device
          DataType = dataType
          BitOffset = bitOffset }
