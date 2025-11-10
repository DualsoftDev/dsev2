namespace DSPLCServer.FS.Database

open System
open System.Collections.Generic
open System.Threading.Tasks
open Dual.PLC.Common.FS

/// 간단한 데이터 포인트
type SimplePLCDataPoint = {
    Id: int64
    TagName: string
    PlcIP: string
    PlcType: string
    Value: obj
    DataType: PlcDataSizeType
    Quality: string
    Timestamp: DateTime
    Address: string
}

/// 간단한 PLC 설정
type SimplePLCConfiguration = {
    Id: int
    PlcIP: string
    PlcType: string
    PlcName: string
    ScanInterval: int
    IsActive: bool
    ConnectionString: string option
    CreatedAt: DateTime
    UpdatedAt: DateTime
}

/// 간단한 태그 설정
type SimpleTagConfiguration = {
    Id: int
    PlcId: int
    TagName: string
    Address: string
    DataType: PlcDataSizeType
    ScanGroup: string
    IsActive: bool
    Comment: string
    CreatedAt: DateTime
    UpdatedAt: DateTime
}

/// 간단한 데이터베이스 인터페이스
type ISimpleDataRepository =
    abstract member InitializeDatabaseAsync: unit -> Task<bool>
    abstract member TestConnectionAsync: unit -> Task<bool>
    abstract member GetPLCByIdAsync: int -> Task<SimplePLCConfiguration option>
    abstract member GetAllPLCsAsync: unit -> Task<SimplePLCConfiguration[]>
    abstract member InsertDataPointAsync: SimplePLCDataPoint -> Task<bool>
    abstract member GetLatestDataPointsAsync: string[] -> Task<SimplePLCDataPoint[]>

/// 메모리 기반 간단한 저장소
type SimpleMemoryRepository() =
    let mutable plcConfigs = [|
        { Id = 1; PlcIP = "192.168.1.100"; PlcType = "LSElectric"; PlcName = "PLC1"; ScanInterval = 1000; IsActive = true; ConnectionString = None; CreatedAt = DateTime.Now; UpdatedAt = DateTime.Now }
        { Id = 2; PlcIP = "192.168.1.101"; PlcType = "Mitsubishi"; PlcName = "PLC2"; ScanInterval = 1000; IsActive = true; ConnectionString = None; CreatedAt = DateTime.Now; UpdatedAt = DateTime.Now }
    |]
    
    let mutable tagConfigs = [|
        { Id = 1; PlcId = 1; TagName = "D100"; Address = "D100"; DataType = PlcDataSizeType.Int32; ScanGroup = "Default"; IsActive = true; Comment = "Test tag"; CreatedAt = DateTime.Now; UpdatedAt = DateTime.Now }
        { Id = 2; PlcId = 1; TagName = "M10"; Address = "M10"; DataType = PlcDataSizeType.Boolean; ScanGroup = "Default"; IsActive = true; Comment = "Test bool"; CreatedAt = DateTime.Now; UpdatedAt = DateTime.Now }
    |]
    
    let dataPoints = System.Collections.Concurrent.ConcurrentBag<SimplePLCDataPoint>()
    
    let convertToStandardTypes (simplePLC: SimplePLCConfiguration) : PLCConfiguration = {
        Id = simplePLC.Id
        PlcIP = simplePLC.PlcIP
        PlcType = simplePLC.PlcType
        PlcName = simplePLC.PlcName
        ScanInterval = simplePLC.ScanInterval
        IsActive = simplePLC.IsActive
        ConnectionString = simplePLC.ConnectionString
        CreatedAt = simplePLC.CreatedAt
        UpdatedAt = simplePLC.UpdatedAt
    }
    
    let convertToStandardDataPoint (simpleDP: SimplePLCDataPoint) : PLCDataPoint = {
        Id = simpleDP.Id
        TagName = simpleDP.TagName
        PlcIP = simpleDP.PlcIP
        PlcType = simpleDP.PlcType
        Value = simpleDP.Value
        DataType = simpleDP.DataType
        Quality = simpleDP.Quality
        Timestamp = simpleDP.Timestamp
        Address = simpleDP.Address
    }
    
    let convertFromStandardDataPoint (dp: PLCDataPoint) : SimplePLCDataPoint = {
        Id = dp.Id
        TagName = dp.TagName
        PlcIP = dp.PlcIP
        PlcType = dp.PlcType
        Value = dp.Value
        DataType = dp.DataType
        Quality = dp.Quality
        Timestamp = dp.Timestamp
        Address = dp.Address
    }
    
    let convertToStandardTag (simpleTag: SimpleTagConfiguration) : TagConfiguration = {
        Id = simpleTag.Id
        PlcId = simpleTag.PlcId
        TagName = simpleTag.TagName
        Address = simpleTag.Address
        DataType = simpleTag.DataType
        ScanGroup = simpleTag.ScanGroup
        IsActive = simpleTag.IsActive
        Comment = simpleTag.Comment
        CreatedAt = simpleTag.CreatedAt
        UpdatedAt = simpleTag.UpdatedAt
    }
    
    interface ISimpleDataRepository with
        member this.InitializeDatabaseAsync() = task {
            printfn "Memory database initialized"
            return true
        }
        
        member this.TestConnectionAsync() = task {
            return true
        }
        
        member this.GetPLCByIdAsync(id: int) = task {
            return plcConfigs |> Array.tryFind (fun p -> p.Id = id)
        }
        
        member this.GetAllPLCsAsync() = task {
            return plcConfigs
        }
        
        member this.InsertDataPointAsync(dataPoint: SimplePLCDataPoint) = task {
            dataPoints.Add(dataPoint)
            return true
        }
        
        member this.GetLatestDataPointsAsync(tagNames: string[]) = task {
            let results = 
                tagNames 
                |> Array.choose (fun tagName ->
                    dataPoints 
                    |> Seq.filter (fun dp -> dp.TagName = tagName)
                    |> Seq.sortByDescending (fun dp -> dp.Timestamp)
                    |> Seq.tryHead)
                
            return results
        }
    
    interface IDataRepository with
        // PLC 설정 관리
        member this.CreatePLCAsync(plcConfig: PLCConfiguration) = task {
            let newId = (plcConfigs |> Array.map (fun p -> p.Id) |> Array.max) + 1
            let simplePLC = {
                Id = newId
                PlcIP = plcConfig.PlcIP
                PlcType = plcConfig.PlcType
                PlcName = plcConfig.PlcName
                ScanInterval = plcConfig.ScanInterval
                IsActive = plcConfig.IsActive
                ConnectionString = plcConfig.ConnectionString
                CreatedAt = plcConfig.CreatedAt
                UpdatedAt = plcConfig.UpdatedAt
            }
            plcConfigs <- Array.append plcConfigs [| simplePLC |]
            return newId
        }
        
        member this.GetPLCByIdAsync(id: int) = task {
            let result = plcConfigs |> Array.tryFind (fun p -> p.Id = id)
            return result |> Option.map convertToStandardTypes
        }
        
        member this.GetAllPLCsAsync() = task {
            return plcConfigs |> Array.map convertToStandardTypes
        }
        
        member this.UpdatePLCAsync(plcConfig: PLCConfiguration) = task {
            let index = plcConfigs |> Array.findIndex (fun p -> p.Id = plcConfig.Id)
            let updated = {
                Id = plcConfig.Id
                PlcIP = plcConfig.PlcIP
                PlcType = plcConfig.PlcType
                PlcName = plcConfig.PlcName
                ScanInterval = plcConfig.ScanInterval
                IsActive = plcConfig.IsActive
                ConnectionString = plcConfig.ConnectionString
                CreatedAt = plcConfigs.[index].CreatedAt
                UpdatedAt = DateTime.Now
            }
            plcConfigs.[index] <- updated
            return true
        }
        
        member this.DeletePLCAsync(id: int) = task {
            let exists = plcConfigs |> Array.exists (fun p -> p.Id = id)
            if exists then
                plcConfigs <- plcConfigs |> Array.filter (fun p -> p.Id <> id)
                return true
            else
                return false
        }
        
        member this.GetActivePLCsAsync() = task {
            return plcConfigs |> Array.filter (fun p -> p.IsActive) |> Array.map convertToStandardTypes
        }
        
        // 태그 설정 관리
        member this.CreateTagAsync(tagConfig: TagConfiguration) = task {
            let newId = (tagConfigs |> Array.map (fun t -> t.Id) |> Array.max) + 1
            let simpleTag = {
                Id = newId
                PlcId = tagConfig.PlcId
                TagName = tagConfig.TagName
                Address = tagConfig.Address
                DataType = tagConfig.DataType
                ScanGroup = tagConfig.ScanGroup
                IsActive = tagConfig.IsActive
                Comment = tagConfig.Comment
                CreatedAt = tagConfig.CreatedAt
                UpdatedAt = tagConfig.UpdatedAt
            }
            tagConfigs <- Array.append tagConfigs [| simpleTag |]
            return newId
        }
        
        member this.GetTagByIdAsync(id: int) = task {
            let result = tagConfigs |> Array.tryFind (fun t -> t.Id = id)
            return result |> Option.map convertToStandardTag
        }
        
        member this.GetTagsByPLCIdAsync(plcId: int) = task {
            return tagConfigs |> Array.filter (fun t -> t.PlcId = plcId) |> Array.map convertToStandardTag
        }
        
        member this.GetAllTagsAsync() = task {
            return tagConfigs |> Array.map convertToStandardTag
        }
        
        member this.UpdateTagAsync(tagConfig: TagConfiguration) = task {
            let index = tagConfigs |> Array.findIndex (fun t -> t.Id = tagConfig.Id)
            let updated = {
                tagConfigs.[index] with
                    TagName = tagConfig.TagName
                    Address = tagConfig.Address
                    DataType = tagConfig.DataType
                    ScanGroup = tagConfig.ScanGroup
                    IsActive = tagConfig.IsActive
                    Comment = tagConfig.Comment
                    UpdatedAt = DateTime.Now
            }
            tagConfigs.[index] <- updated
            return true
        }
        
        member this.DeleteTagAsync(id: int) = task {
            let exists = tagConfigs |> Array.exists (fun t -> t.Id = id)
            if exists then
                tagConfigs <- tagConfigs |> Array.filter (fun t -> t.Id <> id)
                return true
            else
                return false
        }
        
        member this.GetActiveTagsByPLCIdAsync(plcId: int) = task {
            return tagConfigs |> Array.filter (fun t -> t.PlcId = plcId && t.IsActive) |> Array.map convertToStandardTag
        }
        
        // 데이터 기록 및 조회
        member this.InsertDataPointAsync(dataPoint: PLCDataPoint) = task {
            let simpleDP = convertFromStandardDataPoint dataPoint
            dataPoints.Add(simpleDP)
            return true
        }
        
        member this.InsertDataPointsAsync(dataPointsToInsert: PLCDataPoint[]) = task {
            for dp in dataPointsToInsert do
                let simpleDP = convertFromStandardDataPoint dp
                dataPoints.Add(simpleDP)
            return true
        }
        
        member this.GetDataPointsAsync(tagName: string) (fromTime: DateTime) (toTime: DateTime) = task {
            let results = 
                dataPoints 
                |> Seq.filter (fun dp -> dp.TagName = tagName && dp.Timestamp >= fromTime && dp.Timestamp <= toTime)
                |> Seq.sortBy (fun dp -> dp.Timestamp)
                |> Seq.map convertToStandardDataPoint
                |> Array.ofSeq
            return results
        }
        
        member this.GetLatestDataPointsAsync(tagNames: string[]) = task {
            let results = 
                tagNames 
                |> Array.choose (fun tagName ->
                    dataPoints 
                    |> Seq.filter (fun dp -> dp.TagName = tagName)
                    |> Seq.sortByDescending (fun dp -> dp.Timestamp)
                    |> Seq.tryHead
                    |> Option.map convertToStandardDataPoint)
                
            return results
        }
        
        member this.GetDataPointsByPLCAsync(plcIP: string) (fromTime: DateTime) (toTime: DateTime) = task {
            let results = 
                dataPoints 
                |> Seq.filter (fun dp -> dp.PlcIP = plcIP && dp.Timestamp >= fromTime && dp.Timestamp <= toTime)
                |> Seq.sortBy (fun dp -> dp.Timestamp)
                |> Seq.map convertToStandardDataPoint
                |> Array.ofSeq
            return results
        }
        
        // 통계 및 분석
        member this.GetDataCountAsync(tagName: string) (fromTime: DateTime) (toTime: DateTime) = task {
            let count = 
                dataPoints 
                |> Seq.filter (fun dp -> dp.TagName = tagName && dp.Timestamp >= fromTime && dp.Timestamp <= toTime)
                |> Seq.length
                |> int64
            return count
        }
        
        member this.GetAverageValueAsync(tagName: string) (fromTime: DateTime) (toTime: DateTime) = task {
            let values = 
                dataPoints 
                |> Seq.filter (fun dp -> dp.TagName = tagName && dp.Timestamp >= fromTime && dp.Timestamp <= toTime)
                |> Seq.choose (fun dp -> 
                    try Some (Convert.ToDouble(dp.Value))
                    with | _ -> None)
                |> List.ofSeq
            
            if values.Length > 0 then
                return Some (values |> List.average)
            else
                return None
        }
        
        member this.GetMinMaxValueAsync(tagName: string) (fromTime: DateTime) (toTime: DateTime) = task {
            let values = 
                dataPoints 
                |> Seq.filter (fun dp -> dp.TagName = tagName && dp.Timestamp >= fromTime && dp.Timestamp <= toTime)
                |> Seq.choose (fun dp -> 
                    try Some (Convert.ToDouble(dp.Value))
                    with | _ -> None)
                |> List.ofSeq
            
            if values.Length > 0 then
                let min = values |> List.min
                let max = values |> List.max
                return Some (min, max)
            else
                return None
        }
        
        // 데이터베이스 관리
        member this.InitializeDatabaseAsync() = task {
            printfn "Memory database initialized with IDataRepository interface"
            return true
        }
        
        member this.TestConnectionAsync() = task {
            return true
        }
        
        member this.CleanupOldDataAsync(olderThan: DateTime) = task {
            let oldCount = dataPoints |> Seq.length |> int64
            let remaining = dataPoints |> Seq.filter (fun dp -> dp.Timestamp > olderThan) |> List.ofSeq
            dataPoints.Clear()
            for dp in remaining do
                dataPoints.Add(dp)
            let newCount = dataPoints |> Seq.length |> int64
            return oldCount - newCount
        }
        
        member this.GetDatabaseInfoAsync() = task {
            let info = Dictionary<string, obj>()
            info.["Type"] <- "Memory"
            info.["PLCCount"] <- plcConfigs.Length
            info.["TagCount"] <- tagConfigs.Length
            info.["DataPointCount"] <- dataPoints.Count
            return info :> IDictionary<string, obj>
        }

/// 간단한 데이터베이스 팩토리
module SimpleDatabase =
    let createRepository() : ISimpleDataRepository =
        new SimpleMemoryRepository() :> ISimpleDataRepository
    
    let createStandardRepository() : IDataRepository =
        new SimpleMemoryRepository() :> IDataRepository