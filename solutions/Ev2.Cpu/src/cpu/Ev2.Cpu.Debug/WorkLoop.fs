namespace Ev2.Cpu.Debug

open Ev2.Cpu.Core
open Ev2.Cpu.Core.Expression
open Ev2.Cpu.Core.Statement

module WorkLoop =

    let inline private boolConst (value: bool) = Const(box value, typeof<bool>)

    let inline private ton name preset enable =
        call "TON" [ enable; str name; num preset ]

    let program : Program =
        clearVariableRegistry()

        let work1State = intVar "Work1_State"
        let work2State = intVar "Work2_State"

        let work1Running = boolVar "Work1_Running"
        let work2Running = boolVar "Work2_Running"

        let work1StartReq = boolVar "Work1_StartRequest"
        let work2StartReq = boolVar "Work2_StartRequest"

        let work1Complete = boolVar "Work1_Complete"
        let work2Complete = boolVar "Work2_Complete"
        let work1StartEdge = boolVar "Work1_StartEdge"
        let work2StartEdge = boolVar "Work2_StartEdge"
        let work1FinishEdge = boolVar "Work1_FinishEdge"
        let work2FinishEdge = boolVar "Work2_FinishEdge"

        let manualStart = boolVar "Start_Work1"

        let w1StartCondition =
            work1StartReq &&. (!!. work1Running) &&. (work1State ==. num 0) &&. (!!. work2Running)

        let w2StartCondition =
            work2StartReq &&. (!!. work2Running) &&. (work2State ==. num 0) &&. (!!. work1Running)

        let w1FinishCondition =
            work1Running &&. (work1State ==. num 4)

        let w2FinishCondition =
            work2Running &&. (work2State ==. num 4)

        let stepDuration = 200

        let w1Step conditionName stateGuard nextState preset =
            let guard = work1Running &&. (work1State ==. num stateGuard)
            guard &&. ton conditionName preset guard,
            nextState

        let w2Step conditionName stateGuard nextState preset =
            let guard = work2Running &&. (work2State ==. num stateGuard)
            guard &&. ton conditionName preset guard,
            nextState

        let w1Step1, w1Step1Next = w1Step "W1_STEP1" 1 2 stepDuration
        let w1Step2, w1Step2Next = w1Step "W1_STEP2" 2 3 stepDuration
        let w1Step3, w1Step3Next = w1Step "W1_STEP3" 3 4 stepDuration
        
        // 단순화된 완료 타이머 로직
        let w1FinishTimer = ton "W1_STEP4" stepDuration w1FinishCondition
        let w1FinishEdgePrev = boolVar "Work1_FinishEdge_Prev"

        let w2Step1, w2Step1Next = w2Step "W2_STEP1" 1 2 stepDuration
        let w2Step2, w2Step2Next = w2Step "W2_STEP2" 2 3 stepDuration
        let w2Step3, w2Step3Next = w2Step "W2_STEP3" 3 4 stepDuration
        
        // 단순화된 완료 타이머 로직
        let w2FinishTimer = ton "W2_STEP4" stepDuration w2FinishCondition
        let w2FinishEdgePrev = boolVar "Work2_FinishEdge_Prev"

        { Name = "Stn1WorkLoop"
          Inputs =
            [ "Start_Work1", typeof<bool> ]
          Outputs =
            [ "Work1.Active", typeof<bool>
              "Work2.Active", typeof<bool>
              "Work1.StartReset", typeof<bool>
              "Work2.StartReset", typeof<bool>
              "Device1.ADV", typeof<bool>
              "Device1.RET", typeof<bool>
              "Device2.ADV", typeof<bool>
              "Device2.RET", typeof<bool>
              "CycleCountReached", typeof<bool> ]
          Locals =
            [ "Work1_State", typeof<int>
              "Work2_State", typeof<int>
              "Work1_Running", typeof<bool>
              "Work2_Running", typeof<bool>
              "Work1_StartRequest", typeof<bool>
              "Work2_StartRequest", typeof<bool>
              "Work1_Complete", typeof<bool>
              "Work2_Complete", typeof<bool>
              "Work1_StartEdge", typeof<bool>
              "Work2_StartEdge", typeof<bool>
              "Work1_FinishEdge", typeof<bool>
              "Work2_FinishEdge", typeof<bool>
              "Work1_FinishEdge_Prev", typeof<bool>
              "Work2_FinishEdge_Prev", typeof<bool>
              "WorkCycleCount", typeof<int>
              // 디버깅 변수들
              "Debug_W1FinishCondition", typeof<bool>
              "Debug_W1FinishTimer", typeof<bool>
              "Debug_W2FinishCondition", typeof<bool>
              "Debug_W2FinishTimer", typeof<bool> ]
          Body = [
              // ════════════════════════════════════════════════════════════════
              // 디버깅 변수들 (먼저 계산)
              // ════════════════════════════════════════════════════════════════
              DsTag.Bool "Debug_W1FinishCondition" := w1FinishCondition
              DsTag.Bool "Debug_W1FinishTimer" := w1FinishTimer
              DsTag.Bool "Debug_W2FinishCondition" := w2FinishCondition
              DsTag.Bool "Debug_W2FinishTimer" := w2FinishTimer

              // ════════════════════════════════════════════════════════════════
              // Work1 로직 - 원자적 처리
              // ════════════════════════════════════════════════════════════════
              
              // Work1 start request sources (재트리거 보호 추가)
              (manualStart &&. (!!. work1Running) &&. (!!. work1StartReq) &&. (work1State ==. num 0) &&. (!!. work2Running))
                --> (call "MOV" [ boolConst true; Terminal(DsTag.Bool("Work1_StartRequest")) ])

              (work2Complete &&. (!!. work1Running) &&. (!!. work1StartReq) &&. (work1State ==. num 0))
                --> (call "MOV" [ boolConst true; Terminal(DsTag.Bool("Work1_StartRequest")) ])

              // Work1 start edge detection (단순화)
              DsTag.Bool "Work1_StartEdge" := w1StartCondition

              // Work1 finish edge detection (단순화된 로직)
              DsTag.Bool "Work1_FinishEdge" := (w1FinishTimer &&. (!!. w1FinishEdgePrev))
              DsTag.Bool "Work1_FinishEdge_Prev" := w1FinishTimer  // 다음 스캔을 위한 이전값 저장

              // Work1 start actions (원자적 처리)
              work1StartEdge --> (call "MOV" [ boolConst true; Terminal(DsTag.Bool("Work1_Running")) ])
              work1StartEdge --> (call "MOV" [ num 1; Terminal(DsTag.Int("Work1_State")) ])
              work1StartEdge --> (call "MOV" [ boolConst false; Terminal(DsTag.Bool("Work1_StartRequest")) ])
              work1StartEdge --> (call "MOV" [ boolConst false; Terminal(DsTag.Bool("Work1_Complete")) ])
              work1StartEdge --> (call "MOV" [ boolConst false; Terminal(DsTag.Bool("Work2_Complete")) ])

              // Work1 timed sequence
              w1Step1 --> (call "MOV" [ num w1Step1Next; Terminal(DsTag.Int("Work1_State")) ])
              w1Step2 --> (call "MOV" [ num w1Step2Next; Terminal(DsTag.Int("Work1_State")) ])
              w1Step3 --> (call "MOV" [ num w1Step3Next; Terminal(DsTag.Int("Work1_State")) ])

              // Work1 finish actions (원자적 처리)
              work1FinishEdge --> (call "MOV" [ num 0; Terminal(DsTag.Int("Work1_State")) ])
              work1FinishEdge --> (call "MOV" [ boolConst false; Terminal(DsTag.Bool("Work1_Running")) ])
              work1FinishEdge --> (call "MOV" [ boolConst true; Terminal(DsTag.Bool("Work1_Complete")) ])
              work1FinishEdge --> (call "MOV" [ boolConst true; Terminal(DsTag.Bool("Work2_StartRequest")) ])

              // ════════════════════════════════════════════════════════════════
              // Work2 로직 - 원자적 처리
              // ════════════════════════════════════════════════════════════════

              // Work2 start request maintenance (재트리거 보호 추가)
              (work1Complete &&. (!!. work2Running) &&. (!!. work2StartReq) &&. (work2State ==. num 0) &&. (!!. work1Running))
                --> (call "MOV" [ boolConst true; Terminal(DsTag.Bool("Work2_StartRequest")) ])

              // Work2 start edge detection (단순화)
              DsTag.Bool "Work2_StartEdge" := w2StartCondition

              // Work2 finish edge detection (단순화된 로직)
              DsTag.Bool "Work2_FinishEdge" := (w2FinishTimer &&. (!!. w2FinishEdgePrev))
              DsTag.Bool "Work2_FinishEdge_Prev" := w2FinishTimer  // 다음 스캔을 위한 이전값 저장

              // Work2 start actions (원자적 처리)
              work2StartEdge --> (call "MOV" [ boolConst true; Terminal(DsTag.Bool("Work2_Running")) ])
              work2StartEdge --> (call "MOV" [ num 1; Terminal(DsTag.Int("Work2_State")) ])
              work2StartEdge --> (call "MOV" [ boolConst false; Terminal(DsTag.Bool("Work2_StartRequest")) ])
              work2StartEdge --> (call "MOV" [ boolConst false; Terminal(DsTag.Bool("Work2_Complete")) ])
              work2StartEdge --> (call "MOV" [ boolConst false; Terminal(DsTag.Bool("Work1_Complete")) ])

              // Work2 timed sequence
              w2Step1 --> (call "MOV" [ num w2Step1Next; Terminal(DsTag.Int("Work2_State")) ])
              w2Step2 --> (call "MOV" [ num w2Step2Next; Terminal(DsTag.Int("Work2_State")) ])
              w2Step3 --> (call "MOV" [ num w2Step3Next; Terminal(DsTag.Int("Work2_State")) ])

              // Work2 finish actions (원자적 처리)
              work2FinishEdge --> (call "MOV" [ num 0; Terminal(DsTag.Int("Work2_State")) ])
              work2FinishEdge --> (call "MOV" [ boolConst false; Terminal(DsTag.Bool("Work2_Running")) ])
              work2FinishEdge --> (call "MOV" [ boolConst true; Terminal(DsTag.Bool("Work2_Complete")) ])
              work2FinishEdge --> (call "MOV" [ boolConst true; Terminal(DsTag.Bool("Work1_StartRequest")) ])

              // ════════════════════════════════════════════════════════════════
              // 출력 및 상태 표시
              // ════════════════════════════════════════════════════════════════

              // Work1 status outputs
              DsTag.Bool "Work1.Active" := work1Running
              DsTag.Bool "Device1.ADV" := (work1Running &&. ((work1State ==. num 1) ||. (work1State ==. num 3)))
              DsTag.Bool "Device1.RET" := (work1Running &&. ((work1State ==. num 2) ||. (work1State ==. num 4)))

              // Work2 status outputs
              DsTag.Bool "Work2.Active" := work2Running
              DsTag.Bool "Device2.ADV" := (work2Running &&. ((work2State ==. num 1) ||. (work2State ==. num 3)))
              DsTag.Bool "Device2.RET" := (work2Running &&. ((work2State ==. num 2) ||. (work2State ==. num 4)))

              // StartReset handshake pulses
              DsTag.Bool "Work1.StartReset" := work2StartEdge
              DsTag.Bool "Work2.StartReset" := work1StartEdge

              // ════════════════════════════════════════════════════════════════
              // 사이클 카운터 (수정된 로직)
              // ════════════════════════════════════════════════════════════════
              DsTag.Int "WorkCycleCount" :=
                call "CTU" [ str "CYCLE_COUNT"; work2FinishEdge; num 2 ]

              DsTag.Bool "CycleCountReached" :=
                (Terminal(DsTag.Int "WorkCycleCount") >=. num 2)
          ] }
