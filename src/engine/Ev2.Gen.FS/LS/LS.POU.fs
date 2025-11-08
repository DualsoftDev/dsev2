namespace Ev2.Gen

open System
open System.Collections.Generic
open Dual.Common.Base

[<AutoOpen>]
module POUModule =

    type POU = {
        Storage:Storage
        Program:Program
    }

    type Project(?globalStorage:Storage, ?scanPrograms:POU seq) =
        interface IProject
        member val ScanPrograms = ResizeArray(scanPrograms |? [])
        member val GlobalStorage = globalStorage |? Storage() with get, set

    type StateDic = Dictionary<string, obj>
    type IECProject(?globalStorage:Storage, ?scanPrograms:POU seq, ?udts:Struct seq, ?functions:POU seq, ?functionBlocks:POU seq) =
        inherit Project(?globalStorage=globalStorage, ?scanPrograms=scanPrograms)
        member val UDTs = ResizeArray(udts |? [])
        member val FunctionPrograms = ResizeArray(functions |? [])
        member val FBPrograms = ResizeArray(functionBlocks |? [])
        member val internal FBInstanceStates = Dictionary<FBInstance, StateDic>() with get
        member val internal FBInstanceStatesByName = Dictionary<string, StateDic>(StringComparer.OrdinalIgnoreCase) with get
