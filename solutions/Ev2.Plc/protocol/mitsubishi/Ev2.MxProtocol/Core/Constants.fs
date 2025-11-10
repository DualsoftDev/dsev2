namespace Ev2.MxProtocol.Core

open System

/// Protocol constants
[<AutoOpen>]
module Constants =
    // Subheader values
    [<Literal>]
    let SubheaderRequestBinary = 0x0050us
    
    [<Literal>]  
    let SubheaderResponseBinary = 0x00D0us
    
    [<Literal>]
    let SubheaderRequestAscii = "5000"
    
    [<Literal>]
    let SubheaderResponseAscii = "D000"
    
    // Frame sizes
    [<Literal>]
    let FrameHeaderLength3E = 9
    
    [<Literal>]
    let FrameHeaderWithEndCode = 11
    
    [<Literal>]
    let FrameHeaderLength3C = 5
    
    [<Literal>]
    let FrameHeaderLength4C = 11
    
    // Limits - MELSEC Protocol Specifications
    [<Literal>]
    let MaxBatchReadBits = 7904  // Maximum bit points for batch read
    
    [<Literal>]
    let MaxBatchReadWords = 960  // Maximum word points for batch read/write
    
    [<Literal>]
    let MaxBatchWriteBits = 7904  // Maximum bit points for batch write (same as read)
    
    [<Literal>]
    let MaxRandomReadPoints = 192  // Maximum random read points
    
    [<Literal>]
    let MaxRandomWriteBitPoints = 188  // Maximum random write bit points
    
    [<Literal>]
    let MaxMonitorPoints = 192  // Maximum monitor registration points
    
    [<Literal>]
    let MaxMultiBlockBits = 7168  // Maximum bit points for multi-block operations
    
    [<Literal>]
    let WordSizeBytes = 2
    
    module Defaults =
        let config name host port  =
            {
                Name = name
                Host = host
                Port = port
                Timeout = TimeSpan.FromSeconds(2.0)
                FrameType = FrameType.QnA_3E_Binary
                AccessRoute = {
                    NetworkNumber = 0x00uy
                    StationNumber = 0xFFuy
                    IoNumber = 0x03FFus
                    RelayType = 0x00uy
                }
                MonitoringTimer = 0x0010us
            }