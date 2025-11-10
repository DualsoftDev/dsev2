namespace Ev2.LsProtocol.Core

module Constants =
    
    /// XGT 프로토콜 기본 설정
    [<Literal>]
    let DefaultPort = 2004
    
    [<Literal>]
    let DefaultTimeout = 5000
    
    /// XGT 프레임 구조 상수
    [<Literal>]
    let FrameHeaderSize = 20
    
    [<Literal>]
    let MaxDataSize = 1024
    
    /// XGT 명령어 코드
    [<Literal>]
    let CMD_READ = 0x54us
    
    [<Literal>]
    let CMD_WRITE = 0x58us
    
    [<Literal>]
    let CMD_READ_MULTI = 0x5Cus
    
    [<Literal>]
    let CMD_WRITE_MULTI = 0x5Dus
    
    /// XGI 디바이스 코드
    let XgiDevices = [
        "I"; "Q"; "M"; "L"; "N"; "K"; "R"; "A"; "W"; "F"; "U"
    ]
    
    /// XGK 디바이스 코드  
    let XgkDevices = [
        "P"; "M"; "K"; "F"; "T"; "C"; "N"; "D"; "R"; "L"; "ZR"; "U";
        "X"; "Y"; "B"; "SB"; "SM"; "DX"; "DY"; "SX"
    ]
    
    /// 데이터 타입별 크기 (비트 단위)
    let DataTypeSizes = [
        ("X", 1); ("B", 8); ("W", 16); ("D", 32); ("L", 64)
                         ] |> Map.ofList