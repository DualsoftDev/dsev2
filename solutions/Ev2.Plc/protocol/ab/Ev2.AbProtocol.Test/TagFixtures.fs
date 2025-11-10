namespace Ev2.AbProtocol.Test

open Ev2.AbProtocol.Core

module TagFixtures =
    
    type TagDescriptor =
        { Name: string
          DataType: DataType
          ElementCount: int }
    
    module Tags =
        let boolScalar =
            { Name = "Motor_Enable"
              DataType = DataType.BOOL
              ElementCount = 1 }
        
        let intScalar =
            { Name = "Temperature"
              DataType = DataType.INT
              ElementCount = 1 }
        
        let dintScalar =
            { Name = "Total_Count"
              DataType = DataType.DINT
              ElementCount = 1 }
        
        let smallDintArray =
            { Name = "B100"
              DataType = DataType.DINT
              ElementCount = 20 }
        
        let mediumIntArray =
            { Name = "ARRINT2500"
              DataType = DataType.INT
              ElementCount = 2500 }
        
        let largeIntArray =
            { Name = "Stress_Test_2"
              DataType = DataType.INT
              ElementCount = 5000 }
        
        let largeDintArray =
            { Name = "Stress_Test_3"
              DataType = DataType.DINT
              ElementCount = 3000 }
        
        let boolArrayBase =
            { Name = "BitArr"
              DataType = DataType.BOOL
              ElementCount = 1024 }
        
        let nonExistent =
            { Name = "UNKNOWN_TAG_12345"
              DataType = DataType.BOOL
              ElementCount = 1 }
    
    let inline expectBool (value: obj) =
        match value with
        | :? bool as b -> b
        | _ -> failwithf "Expected bool payload but received %s" (value.GetType().FullName)
    
    let inline expectInt16 (value: obj) =
        match value with
        | :? int16 as v -> v
        | :? (int16[]) as arr when arr.Length > 0 -> arr.[0]
        | _ -> failwithf "Expected int16 payload but received %s" (value.GetType().FullName)
    
    let inline expectInt32 (value: obj) =
        match value with
        | :? int32 as v -> v
        | :? (int32[]) as arr when arr.Length > 0 -> arr.[0]
        | _ -> failwithf "Expected int32 payload but received %s" (value.GetType().FullName)
