namespace Ev2.Cpu.Generation.System

open System
open Ev2.Cpu.Core
open Ev2.Cpu.Core.Expression

type SystemKind  =
    | _ON                      = 0000
    | _OFF                     = 0001
    | _INIT                    = 0002
    | _SHUTDOWN                = 0003
    
    // Flicker/Pulse signals
    | _T20MS                   = 0004
    | _T100MS                  = 0005
    | _T200MS                  = 0006
    | _T1S                     = 0007
    | _T2S                     = 0008
    | _T5S                     = 0009
    | _T10S                    = 0010
    
    // DateTime components (int types)
    | datetime_yy              = 0011
    | datetime_mm              = 0012
    | datetime_dd              = 0013
    | datetime_h               = 0014
    | datetime_m               = 0015
    | datetime_s               = 0016
    | datetime_ms              = 0017
    | datetime_dow             = 0018  // Day of week
    | datetime_woy             = 0019  // Week of year
    | datetime_doy             = 0020  // Day of year
    
    // System monitors
    | pauseMonitor             = 0021
    | idleMonitor              = 0022
    | autoMonitor              = 0023
    | manualMonitor            = 0024
    | driveMonitor             = 0025
    | testMonitor              = 0026
    | errorMonitor             = 0027
    | emergencyMonitor         = 0028
    | readyMonitor             = 0029
    | originMonitor            = 0030
    | goingMonitor             = 0031
    | executingMonitor         = 0032
    | maintenanceMonitor       = 0033
    
    // System performance
    | cpuLoad                  = 0040
    | memoryUsage              = 0041
    | scanTime                 = 0042
    | maxScanTime              = 0043
    | minScanTime              = 0044
    | avgScanTime              = 0045
    | cycleCount               = 0046
    | upTime                   = 0047
    
    // Communication status
    | commHealthy              = 0050
    | commError                = 0051
    | commTimeout              = 0052
    | commRetrying             = 0053
    | networkConnected         = 0054
    | networkDisconnected      = 0055
    
    // Safety and interlocks
    | safetyOk                 = 0060
    | safetyTripped            = 0061
    | interlockActive          = 0062
    | permissiveOk             = 0063
    | guardOpen                = 0064
    | guardClosed              = 0065
    
    // Temporary data
    | tempData                 = 9998
    | tempBit                  = 9999

/// Enum helpers
module EnumEx =
    /// Extract (value, name) pairs from an enum<'T> (backed by int)
    let Extract<'T when 'T : enum<int>>() : (int * string)[] =
        let typ = typeof<'T>
        let values = Enum.GetValues(typ) :?> 'T[] |> Seq.cast<int> |> Array.ofSeq
        let names  = Enum.GetNames(typ)
        Array.zip values names

/// SystemKind를 DsTag로 변환
[<RequireQualifiedAccess>]
module SystemKind =
    let toTag (k: SystemKind) : DsTag = 
        EnumEx.Extract<SystemKind>()
        |> Array.tryFind (fun (value, _name) -> value = int k)
        |> function
            | Some (_value, name) -> 
                // DateTime 및 performance 관련 태그는 Int 타입
                match k with
                | SystemKind.datetime_yy | SystemKind.datetime_mm | SystemKind.datetime_dd
                | SystemKind.datetime_h | SystemKind.datetime_m | SystemKind.datetime_s 
                | SystemKind.datetime_ms | SystemKind.datetime_dow | SystemKind.datetime_woy
                | SystemKind.datetime_doy | SystemKind.cpuLoad | SystemKind.memoryUsage
                | SystemKind.scanTime | SystemKind.maxScanTime | SystemKind.minScanTime
                | SystemKind.avgScanTime | SystemKind.cycleCount | SystemKind.upTime ->
                    DsTag.Int(name)
                | SystemKind.tempData ->
                    DsTag.Double(name)
                | _ ->
                    DsTag.Bool(name)
            | None -> failwithf "Unknown SystemKind: %A" k