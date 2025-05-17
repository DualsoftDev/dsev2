[<AutoOpen>]
module HwModelManagerImpl

open System.Linq
open System.IO
open System.Collections.Generic
open Dual.Common.Core.FS


type HwModelManager =

    static member Models = models
    static member GetCPUInfosByCpu(cpu: string) = cpuDeviceMap.[getModelByName cpu]

    static member GetCPUInfosByID(id: int) : DeviceCPUInfo seq option =
        let xs = getModelByID id
        assert (xs.Count() <= 1)

        if xs.IsEmpty() then
            Some(cpuDeviceMap.[xs.Head()].ToArray())
        else
            None

    static member GetMemoryInfos(cpu: string) =
        modelPLCs
        |> Seq.where (fun m -> m.strPLCType = cpu)
        |> Seq.collect (fun m -> cpuDeviceMap.[getModelByName cpu] |> Seq.map (fun d -> d, (folderNfile m d)))
