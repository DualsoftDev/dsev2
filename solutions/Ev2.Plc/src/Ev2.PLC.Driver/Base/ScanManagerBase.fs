namespace Ev2.PLC.Driver.Base

open System
open System.Collections.Generic
open Ev2.PLC.Common.Types

[<AbstractClass>]
type DsScanManagerBase<'T when 'T :> DsScanBase>() =

    let scanners = Dictionary<string, 'T>()

    abstract member CreateScanner: string -> 'T

    member this.StartScan(ip: string, tags: seq<TagInfo>) =
        let scanner =
            match scanners.TryGetValue ip with
            | true, existing -> existing
            | _ ->
                let created = this.CreateScanner ip
                scanners.Add(ip, created)
                created

        scanner.Scan(tags)

    member this.StartScanReadOnly(ip: string, addresses: seq<string>) =
        let tags =
            addresses
            |> Seq.map (fun address ->
                { Name = address
                  Address = address
                  Comment = ""
                  DataType = None
                  IsLowSpeedArea = false
                  IsOutput = false })
        this.StartScan(ip, tags)

    member this.UpdateScan(ip: string, tags: seq<TagInfo>) =
        match scanners.TryGetValue ip with
        | true, scanner -> ignore (scanner.Scan(tags))
        | _ -> invalidArg "ip" (sprintf "Scanner for %s does not exist." ip)

    member this.UpdateScanReadOnly(ip: string, addresses: seq<string>) =
        let tags =
            addresses
            |> Seq.map (fun address ->
                { Name = address
                  Address = address
                  Comment = ""
                  DataType = None
                  IsLowSpeedArea = false
                  IsOutput = false })
        this.UpdateScan(ip, tags)

    member this.IsConnected(ip: string) =
        match scanners.TryGetValue ip with
        | true, scanner -> scanner.IsConnected
        | _ -> false

    member this.StopScan(ip: string) =
        match scanners.TryGetValue ip with
        | true, scanner ->
            scanner.StopScan()
            scanners.Remove ip |> ignore
        | _ -> ()

    member this.StopAll() =
        scanners.Keys |> Seq.toList |> List.iter this.StopScan
        scanners.Clear()

    member this.ActiveIPs = scanners.Keys |> Seq.toList

    member this.GetScanner(ip: string) =
        match scanners.TryGetValue ip with
        | true, scanner -> Some scanner
        | _ -> None
