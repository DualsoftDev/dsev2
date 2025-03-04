namespace PLC.CodeGen.Common.QGraph

open Dual.Common.Core.FS
open Engine.Common

//open Dual.Common.Graph.QuickGraph
//open Dual.Core
//open Dual.Core.Types


[<AutoOpen>]
module CodeGenerationOptionModule =
    type CodeGenerationOption =
        {
            Optimize: bool
            OptimizeNodeStartAndNodeFinish: bool

            /// 출력 자기 유지 적용 여부
            UseSelfHold: bool
            /// 출력 interlock 사용 여부.  A+ 출력 조건에 A- 출력 비접 추가
            UseOutputInterlock: bool
            /// 출력의 reset (출력 완료 비접) 사용 여부.  A+ 출력 조건에 A+ 센서 비접 추가.  Physical model debugging 용
            UseOutputResetByWorkFinish: bool
            /// Set / Reset 조건을 반대로 만든 relay 여부.  사용할때도 negation 필요
            /// Debugging 용도로 false 로 설정.  production 에서는 항상 true 가 되어야 함
            RevertRelays: bool



            /// Relay 이름 생성기
            RelayGenerator: unit -> string

            // { Task States

            /// QgVertex 에서 출력 coil 태그 생성함수.
            ///
            /// e.g : "Ap" vertex 에 대해서 "QAp" 로 생성하고 싶은 경우 사용
            CoilTagGenerator: (IVertex -> string) option
            /// QgVertex 에서 입력 Sensor 태그 생성함수..
            ///
            /// e.g : "Ap" vertex 에 대해서 "IAp" 로 생성하고 싶은 경우 사용
            SensorTagGenerator: (IVertex -> string) option

            ResetNameGenerator: (IVertex -> string) option

            /// Task의 Running/Going 상태 생성 함수
            GoingStateNameGenerator: (IVertex -> string) option
            ///// Task의 Finish 상태 생성 함수
            FinishStateNameGenerator: (IVertex -> string) option
            /// Standby / Ready 상태의 이름 생성 함수.  None 이면 해당 rung 을 생성하지 않는다.
            StandbyStateNameGenerator: (IVertex -> string) option
            /// Homing 상태의 이름 생성 함수.  None 이면 해당 rung 을 생성하지 않는다.
            HomingStateNameGenerator: (IVertex -> string) option
            /// Origin 상태의 이름 생성 함수.  None 이면 해당 rung 을 생성하지 않는다.
            OriginStateNameGenerator: (IVertex -> string) option

            ResetLockRelayNameGenerator: (IVertex -> int -> string) option

            ForceFinishNameGenerator: (IVertex -> string) option

        // } Task States
        }

    /// Relay 이름 생성기.  start 번호부터 시작하는 name + 번호 relay 이름 생성.  e.g {"R0", "R1", ... }
    let relayGenerator name start =
        let relay = name |> String.defaultValue "R"
        incrementalKeywordGenerator relay start


    /// 개념 검증 용 : Abstact 레벨에서의 code 생성 option
    let createAbstractCodeGenerationOption () =
        { Optimize = false
          OptimizeNodeStartAndNodeFinish = false
          UseSelfHold = false
          UseOutputInterlock = false
          UseOutputResetByWorkFinish = false
          RevertRelays = false
          CoilTagGenerator = None
          SensorTagGenerator = None
          GoingStateNameGenerator = None
          FinishStateNameGenerator = None
          StandbyStateNameGenerator = None
          HomingStateNameGenerator = None
          ResetNameGenerator = None
          OriginStateNameGenerator = None
          ResetLockRelayNameGenerator = None
          ForceFinishNameGenerator = None
          RelayGenerator = relayGenerator null 0 }

    /// Optimize 없이 production 용 code 생성 option
    let createDefaultCodeGenerationOption () =
        { createAbstractCodeGenerationOption () with
            UseSelfHold = false
            UseOutputInterlock = true
            UseOutputResetByWorkFinish = true }

    /// 최대 기능 살린 code 생성 option
    let createFullBlownCodeGenerationOption () =
        { createDefaultCodeGenerationOption () with
            Optimize = true
            OptimizeNodeStartAndNodeFinish = true }
