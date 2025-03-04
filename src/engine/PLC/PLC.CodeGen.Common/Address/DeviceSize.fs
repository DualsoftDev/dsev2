[<AutoOpen>]
module DeviceSizeImpl

open System
open System.IO
open System.Collections.Generic


type DeviceCPUInfo =
    { nID: int
      nPLCID: int
      strDevice: string
      nSizeWord: int }

type PLCType =
    { nPLCID: int
      nAPPID: int
      strPLCType: string
      strCPUType: string
      nShowPLC: bool }

type ModelInfo =
    { Name: string
      Id: int
      Type: CpuType
      IsIEC: bool }


let GetModelName (cpuId: int) =
    $"CpuID: {cpuId}"

let folderNfile m d =
    @$"LS Electric\{m.strPLCType.Split('-')[0]}\{d.strDevice}"

let parseCSV (csvLines:string array) =
    csvLines
    |> Array.skip 1
    |> Array.map (fun line -> line.Split(','))
    |> Array.toSeq

//nPLCID,nAPPID,strPLCType,strCPUType,nShowPLC
//0,1,XGK-CPUH,0xA001,1
//1,1,XGK-CPUS,0xA002,1
//2,2,XGB-XBMS,0xB001,1
let modelPLCs =
    let rows = parseCSV (CSVPLCTypeList.Data.Split('\n')|> Seq.toArray)
    rows|> Seq.map (fun f ->
        { nPLCID = int f.[0]
          nAPPID = int f.[1]
          strPLCType = f.[2]
          strCPUType = f.[3]
          nShowPLC = f.[4] = "1" })
//nID,nPLCID,strDevice,nSize
//1,0,P,2048
//3,0,K,2048
//5,0,T,128
let dataDevice =
    parseCSV (CSVDeviceSizeInfo.Data.Split('\n')|> Seq.toArray)
    |> Seq.map (fun f ->
        { nID = int f.[0]
          nPLCID = int f.[1]
          strDevice = f.[2]
          nSizeWord = int f.[3] })


let models =
    modelPLCs
    |> Seq.map (fun m ->

        let plcType =
            match m.strPLCType.Split('-')[0] with
            | "XGI" -> CpuType.Xgi
            | "XGK" -> CpuType.Xgk
            | "XGB" ->
                match m.strPLCType with
                | "XGB-KL"
                | "XGB-KL"
                | "XGB-GIPAM"
                | "XGB-XECS"
                | "XGB-XECE"
                | "XGB-XECH"
                | "XGB-XECU"
                | "XGB-XEMHP"
                | "XGB-XEMH2" -> CpuType.XgbIEC
                | _ -> CpuType.XgbMk
            | _ -> CpuType.Unknown

        let IsIec =
            match plcType with
            | CpuType.Xgi
            | CpuType.XgbIEC -> true
            | _ -> false

        { Name = m.strPLCType
          Id = m.nPLCID
          Type = plcType
          IsIEC = IsIec })


let getModelByID nPLCID =
    modelPLCs |> Seq.filter (fun m -> m.nPLCID = nPLCID)

let getModelByName cpuName =
    modelPLCs |> Seq.filter (fun m -> m.strPLCType = cpuName) |> Seq.head

let cpuDeviceMap =

    let dicModelDevice =
        modelPLCs |> Seq.map (fun m -> m, HashSet<DeviceCPUInfo>()) |> dict

    dataDevice
    |> Seq.iter (fun f ->
        let m = getModelByID f.nPLCID |> Seq.toList
        assert (m.Length <= 1)

        if m.Length = 0 then
            Console.WriteLine $"Device {f.strDevice} nPLCID: {f.nPLCID} cannot be found in the PLC list."
        else
            dicModelDevice.[m[0]].Add f |> ignore)

    dicModelDevice
