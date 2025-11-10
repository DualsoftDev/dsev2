namespace Ev2.Cpu.Generation.System

open System
open Ev2.Cpu.Core
open Ev2.Cpu.Core.Expression
open Ev2.Cpu.Core.Statement
open Ev2.Cpu.Generation.Core

/// System Relay 생성 함수들
module SystemRelays =
    
    /// System Relay - 간결한 패턴
    let sysRelay kind setExpr resetExpr =
        let tag = SystemKind.toTag kind
        Relay.Create(tag, setExpr, resetExpr)
    
    let sysLatch kind setExpr resetExpr =
        let tag = SystemKind.toTag kind
        latchWhen setExpr resetExpr tag
        
    let sysPulse kind trigger =
        let tag = SystemKind.toTag kind
        Generation.pulse trigger tag

    /// System 코드 생성
    let generateSystemCode (name: string) : SystemCode =
        let relays =
            [
                sysRelay SystemKind._ON (boolConst true) (boolConst false)
                sysRelay SystemKind._OFF (boolConst false) (boolConst true)
                sysLatch SystemKind._INIT (boolTag "sys_init_req") (boolTag "sys_init_done")
                sysRelay SystemKind.readyMonitor (boolTag "ready_state") (~~(boolTag "ready_state"))
            ]

        { Name = name
          Relays = relays }
