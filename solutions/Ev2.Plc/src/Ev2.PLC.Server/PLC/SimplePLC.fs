namespace DSPLCServer.FS.PLC

open System
open System.Collections.Generic
open System.Threading.Tasks

/// 간단한 PLC 연결 상태
type SimplePLCStatus =
    | Connected = 1
    | Disconnected = 2
    | Error = 3

/// 간단한 PLC Manager 기본 클래스
[<AbstractClass>]
type SimplePLCManagerBase(plcIP: string, manufacturer: string) =
    let mutable isConnected = false
    let mutable lastError = ""
    
    member this.PlcIP = plcIP
    member this.Manufacturer = manufacturer
    member this.IsConnected = isConnected
    member this.LastError = lastError
    
    abstract member ConnectAsync: unit -> Task<bool>
    abstract member DisconnectAsync: unit -> Task<bool>
    abstract member ReadTagAsync: string -> Task<obj option>
    abstract member WriteTagAsync: string -> obj -> Task<bool>
    abstract member ScanTagsAsync: string[] -> Task<Map<string, obj>>
    
    member this.SetConnectionStatus(connected: bool, error: string) =
        isConnected <- connected
        lastError <- error
    
    interface IDisposable with
        member this.Dispose() =
            this.DisconnectAsync() |> ignore

/// LS Electric PLC Manager
type LSElectricPLCManager(plcIP: string) =
    inherit SimplePLCManagerBase(plcIP, "LSElectric")
    
    let random = Random()
    
    override this.ConnectAsync() = task {
        printfn "[LS Electric] Connecting to PLC at %s..." plcIP
        do! Task.Delay(500) // 연결 시뮬레이션
        this.SetConnectionStatus(true, "")
        printfn "[LS Electric] Connected successfully"
        return true
    }
    
    override this.DisconnectAsync() = task {
        printfn "[LS Electric] Disconnecting from PLC..."
        this.SetConnectionStatus(false, "")
        return true
    }
    
    override this.ReadTagAsync(tagName: string) = task {
        if this.IsConnected then
            // 더미 데이터 반환
            let value = 
                if tagName.StartsWith("D") then box (random.Next(0, 1000))
                elif tagName.StartsWith("M") then box (random.Next(0, 2) = 1)
                else box (random.NextDouble() * 100.0)
            return Some value
        else
            return None
    }
    
    override this.WriteTagAsync(tagName: string) (value: obj) = task {
        if this.IsConnected then
            printfn "[LS Electric] Writing %s = %A" tagName value
            return true
        else
            return false
    }
    
    override this.ScanTagsAsync(tagNames: string[]) = task {
        let results = Dictionary<string, obj>()
        for tagName in tagNames do
            let! value = this.ReadTagAsync(tagName)
            match value with
            | Some v -> results.[tagName] <- v
            | None -> results.[tagName] <- box null
        return Map.ofSeq (results |> Seq.map (fun kvp -> kvp.Key, kvp.Value))
    }

/// Mitsubishi PLC Manager
type MitsubishiPLCManager(plcIP: string) =
    inherit SimplePLCManagerBase(plcIP, "Mitsubishi")
    
    let random = Random()
    
    override this.ConnectAsync() = task {
        printfn "[Mitsubishi] Connecting to PLC at %s..." plcIP
        do! Task.Delay(300)
        this.SetConnectionStatus(true, "")
        printfn "[Mitsubishi] Connected successfully"
        return true
    }
    
    override this.DisconnectAsync() = task {
        printfn "[Mitsubishi] Disconnecting from PLC..."
        this.SetConnectionStatus(false, "")
        return true
    }
    
    override this.ReadTagAsync(tagName: string) = task {
        if this.IsConnected then
            let value = 
                if tagName.StartsWith("D") then box (random.Next(100, 2000))
                elif tagName.StartsWith("M") then box (random.Next(0, 2) = 1)
                else box (random.NextDouble() * 200.0)
            return Some value
        else
            return None
    }
    
    override this.WriteTagAsync(tagName: string) (value: obj) = task {
        if this.IsConnected then
            printfn "[Mitsubishi] Writing %s = %A" tagName value
            return true
        else
            return false
    }
    
    override this.ScanTagsAsync(tagNames: string[]) = task {
        let results = Dictionary<string, obj>()
        for tagName in tagNames do
            let! value = this.ReadTagAsync(tagName)
            match value with
            | Some v -> results.[tagName] <- v
            | None -> results.[tagName] <- box null
        return Map.ofSeq (results |> Seq.map (fun kvp -> kvp.Key, kvp.Value))
    }

/// Allen-Bradley PLC Manager
type AllenBradleyPLCManager(plcIP: string) =
    inherit SimplePLCManagerBase(plcIP, "AllenBradley")
    
    let random = Random()
    
    override this.ConnectAsync() = task {
        printfn "[Allen-Bradley] Connecting to PLC at %s..." plcIP
        do! Task.Delay(400)
        this.SetConnectionStatus(true, "")
        printfn "[Allen-Bradley] Connected successfully"
        return true
    }
    
    override this.DisconnectAsync() = task {
        printfn "[Allen-Bradley] Disconnecting from PLC..."
        this.SetConnectionStatus(false, "")
        return true
    }
    
    override this.ReadTagAsync(tagName: string) = task {
        if this.IsConnected then
            let value = 
                if tagName.Contains("INT") then box (random.Next(500, 3000))
                elif tagName.Contains("BOOL") then box (random.Next(0, 2) = 1)
                else box (random.NextDouble() * 500.0)
            return Some value
        else
            return None
    }
    
    override this.WriteTagAsync(tagName: string) (value: obj) = task {
        if this.IsConnected then
            printfn "[Allen-Bradley] Writing %s = %A" tagName value
            return true
        else
            return false
    }
    
    override this.ScanTagsAsync(tagNames: string[]) = task {
        let results = Dictionary<string, obj>()
        for tagName in tagNames do
            let! value = this.ReadTagAsync(tagName)
            match value with
            | Some v -> results.[tagName] <- v
            | None -> results.[tagName] <- box null
        return Map.ofSeq (results |> Seq.map (fun kvp -> kvp.Key, kvp.Value))
    }

/// 간단한 PLC 팩토리
module SimplePLCFactory =
    let createManager (manufacturer: string) (plcIP: string) : SimplePLCManagerBase =
        match manufacturer.ToLowerInvariant() with
        | "lselectric" | "ls" -> new LSElectricPLCManager(plcIP) :> SimplePLCManagerBase
        | "mitsubishi" | "melsec" -> new MitsubishiPLCManager(plcIP) :> SimplePLCManagerBase
        | "allenbradley" | "ab" -> new AllenBradleyPLCManager(plcIP) :> SimplePLCManagerBase
        | _ -> failwith $"Unsupported PLC manufacturer: {manufacturer}"
    
    let getSupportedManufacturers() = [| "LSElectric"; "Mitsubishi"; "AllenBradley" |]