namespace PLC.CodeGen.LS
open System
open System.Linq
open System.Collections.Generic
open Dual.Common.Core.FS
/// 래더에서 타입 체크할때 사용하기 위한 타입
// RecordType.h : LS 산전에서 전달받은 header file
// #define BOOL_CHECKTYPE			0x00000001
// pp.60, https://sol.ls-electric.com/uploads/document/16572861196090/XGI%20%EC%B4%88%EA%B8%89_V21_.pdf
[<Flags>]
type CheckType =
    | BOOL          = 0x00000001
    | BYTE          = 0x00000002
    | WORD          = 0x00000004
    | DWORD         = 0x00000008
    | LWORD         = 0x00000010
    /// int8.   1 byte 크기
    | SINT          = 0x00000020
    /// int16.  2 byte 크기
    | INT           = 0x00000040
    /// int32.  4 byte 크기
    | DINT          = 0x00000080
    /// int64.  8 byte 크기.  XGK 에서는 사용하지 않음
    | LINT          = 0x00000100
    /// uint8.  1 byte 크기
    | USINT         = 0x00000200
    /// uint16. 2 byte 크기
    | UINT          = 0x00000400
    /// uint32. 4 byte 크기
    | UDINT         = 0x00000800
    /// uint64. 8 byte 크기.  XGK 에서는 사용하지 않음
    | ULINT         = 0x00001000
    /// single. 4 byte 크기
    | REAL          = 0x00002000
    /// double. 8 byte 크기
    | LREAL         = 0x00004000
    | TIME          = 0x00008000
    | DATE          = 0x00010000
    | TOD           = 0x00020000
    | DT            = 0x00040000
    | STRING        = 0x00080000
    | WSTRING       = 0x00100000
    | CONSTANT      = 0x00200000
    | ARRAY         = 0x00400000
    | STRUCTURE     = 0x00800000   // 사용자 평션 및 펑션 블록에서  구조체 타입        05.10.24
    | FBINSTANCE    = 0x01000000   // 사용자펑션 블록에서 FB_INST 타입                05.10.24
    | ANYARRAY      = 0x02000000
    | ONLYDIRECTVAR = 0x04000000
    | NIBBLE        = 0x08000000
    | SAFEBOOL      = 0x10000000   // SAFEBOOL 추가 2012.10.8
    | ONLYCONSTANT  = 0x20000000   // 상수만 가능 추가 2012.10.8
    | ARRAYSIZE     = 0x40000000   // 배열 포인터 타입 (배열 포인터의 사이즈 체크 처리)
    | POINTER       = 0x80000000   // 포인터 타입 (시작주소, 타입크기, 사이즈)

/// function, function Block의 ANY 타입을 구별하기 위함
type AnyType =
   | REAL_LREAL                                                                                                      = 101
   | STRING_WSTRING                                                                                                  = 102
   | BYTE_WORD_DWORD_LWORD                                                                                           = 103
   | WORD_UINT_STRING_WSTRING                                                                                        = 104
   | DWORD_UDINT_STRING_WSTRING                                                                                      = 105
   | INT_DINT_LINT_UINT_UDINT_ULINT                                                                                  = 106
   | SINT_INT_DINT_LINT_USINT_UINT_UDINT_ULINT                                                                       = 107
   | SINT_INT_DINT_LINT_USINT_UINT_UDINT_ULINT_REAL_LREAL                                                            = 108
   | BOOL_BYTE_WORD_DWORD_LWORD                                                                                      = 109
   | BYTE_WORD_DWORD_LWORD_SINT_INT_DINT_LINT_USINT_UINT_UDINT_ULINT_STRING_WSTRING                                  = 110
   | BOOL_WORD_DWORD_LWORD_SINT_INT_DINT_LINT_USINT_UINT_UDINT_ULINT_STRING_WSTRING                                  = 111
   | BOOL_BYTE_DWORD_LWORD_SINT_INT_DINT_LINT_USINT_UINT_UDINT_ULINT_DATE_STRING_WSTRING                             = 112
   | BOOL_BYTE_WORD_LWORD_SINT_INT_DINT_LINT_USINT_UINT_UDINT_ULINT_REAL_TIME_TOD_STRING_WSTRING                     = 113
   | BOOL_BYTE_WORD_DWORD_SINT_INT_DINT_LINT_USINT_UINT_UDINT_ULINT_LREAL_DT_STRING_WSTRING                          = 114
   | BOOL_BYTE_WORD_DWORD_LWORD_INT_DINT_LINT_USINT_UINT_UDINT_ULINT_REAL_LREAL_STRING_WSTRING                       = 115
   | BOOL_BYTE_WORD_DWORD_LWORD_SINT_DINT_LINT_USINT_UINT_UDINT_ULINT_REAL_LREAL_STRING_WSTRING                      = 116
   | BOOL_BYTE_WORD_DWORD_LWORD_SINT_INT_LINT_USINT_UINT_UDINT_ULINT_REAL_LREAL_STRING_WSTRING                       = 117
   | BOOL_BYTE_WORD_DWORD_LWORD_SINT_INT_DINT_USINT_UINT_UDINT_ULINT_REAL_LREAL_STRING_WSTRING                       = 118
   | BOOL_BYTE_WORD_DWORD_LWORD_SINT_INT_DINT_LINT_UINT_UDINT_ULINT_REAL_LREAL_STRING_WSTRING                        = 119
   | BOOL_BYTE_WORD_DWORD_LWORD_SINT_INT_DINT_LINT_USINT_UDINT_ULINT_REAL_LREAL_DATE_STRING_WSTRING                  = 120
   | BOOL_BYTE_WORD_DWORD_LWORD_SINT_INT_DINT_LINT_USINT_UINT_ULINT_REAL_LREAL_TIME_TOD_STRING_WSTRING               = 121
   | BOOL_BYTE_WORD_DWORD_LWORD_SINT_INT_DINT_LINT_USINT_UINT_UDINT_REAL_LREAL_STRING_WSTRING                        = 122
   | BOOL_BYTE_WORD_DWORD_LWORD_SINT_INT_DINT_LINT_USINT_UINT_UDINT_ULINT_REAL_LREAL_TIME_DATE_TOD_DT                = 123
   | BOOL_BYTE_WORD_DWORD_LWORD_SINT_INT_DINT_LINT_USINT_UINT_UDINT_ULINT_REAL_LREAL_TIME_DATE_TOD_DT_WSTRING        = 124
   | BOOL_BYTE_WORD_DWORD_LWORD_SINT_INT_DINT_LINT_USINT_UINT_UDINT_ULINT_REAL_LREAL_TIME_DATE_TOD_DT_STRING         = 125
   | BOOL_BYTE_WORD_DWORD_LWORD_SINT_INT_DINT_LINT_USINT_UINT_UDINT_ULINT_REAL_LREAL_TIME_DATE_TOD_DT_STRING_WSTRING = 126
   | DWORD_SINT_INT_DINT_LINT_USINT_UINT_UDINT_ULINT_LREAL_STRING_WSTRING                                            = 127
   | LWORD_SINT_INT_DINT_LINT_USINT_UINT_UDINT_ULINT_REAL_STRING_WSTRING                                             = 128
   | LWORD_DATE_TOD_STRING_WSTRING                                                                                   = 129
   | TIME_TOD_DT                                                                                                     = 130
   | DINT_LINT                                                                                                       = 131
   | DWORD_REAL                                                                                                      = 132
   | INT_REAL                                                                                                        = 133
   | UINT_REAL                                                                                                       = 134
   | BYTE_WORD_DWORD_LWORD_SINT_INT_DINT_LINT_USINT_UINT_UDINT_ULINT_REAL_LREAL_TIME_DATE_TOD_DT                     = 135
   | BOOL_BYTE_WORD_DWORD_LWORD_SINT_INT_DINT_LINT_USINT_UINT_UDINT_ULINT_REAL_LREAL                                 = 136
   | BOOL_SAFEBOOL                                                                                                   = 137
   | TIMER_CONSTANT                                                                                                  = 138
   | UINT_CONSTANT                                                                                                   = 139
   | INT_CONSTANT                                                                                                    = 140
   | BOOL_ONLY                                                                                                       = 141
   | BYTE_WORD_DWORD_LWORD_SINT_INT_DINT_LINT_USINT_UINT_UDINT_ULINT_REAL_LREAL_TIME_DATE_TOD_DT_STRING_WSTRING      = 142
   | BOOL_BYTE_WORD_DWORD_LWORD_SINT_INT_DINT_LINT_USINT_UINT_UDINT_ULINT_REAL_LREAL_TIME_DATE_TOD_DT_STRING_PTR     = 143
   | SINT_INT_DINT_LINT_USINT_UINT_UDINT_ULINT_REAL_LREAL_TIME_DATE_TOD_DT_STRING                                    = 144



/// XG5000내에서 컨피그레이션(Configuration)은 PLC, HMI, DRIVE 등 기기 단위의 구성을 의미한다
[<AutoOpen>]
module Config =
    /// 현재는 0x103(=259)으로 고정
    let version = 0x103

    /// 컴파일러에게 전달하기 위한 변수 타입 (XGI 변수 타입과 기본타입 인덱스 동일함)
    // RecordType.h : LS 산전에서 전달받은 header file
    // #define BOOL_VARTYPE	1      // 1 비트
    // ....
    [<Flags>]
    type VarType =
        | NONE          = 0
        | BOOL          = 1
        | BYTE          = 2
        /// 2 bytes
        | WORD          = 3
        /// 4 bytes
        | DWORD         = 4
        /// 8 bytes
        | LWORD         = 5
        /// Short Integer.  1 bytes
        | SINT          = 6
        /// 2 bytes
        | INT           = 7
        /// 4 bytes
        | DINT          = 8
        /// 8 bytes
        | LINT          = 9
        /// Unsigned Short Integer.  1 bytes
        | USINT         = 10
        | UINT          = 11
        | UDINT         = 12
        | ULINT         = 13
        /// 4 bytes
        | REAL          = 14
        /// 8 bytes
        | LREAL         = 15
        | TIME          = 16
        | DATE          = 17
        | TIME_OF_DAY   = 18
        | DATE_AND_TIME = 19
        | STRING        = 20
        | WSTRING       = 21
        | ARRAY         = 22
        | STRUCT        = 23
        | FB_INST       = 24
        | SAFEBOOL      = 25
          //추가 항목 개발 중  :1000번 부터 인덱스는 사용자 영역으로 의미없음
        | TON           = 1000 //일반 타이머 Melsec : T
        | TON_UINT      = 1001 //1msec 타이머 Melsec : T
        | TMR           = 1002 //적산 타이머 Melsec : ST
        | CTU_INT       = 1003 //가산 카운터 int 타입

        | TOFF          = 1010
        | CTD_INT       = 1011
        | CTUD_INT      = 1012
        | CTR           = 1013

    /// PLC에 특정 기능이 있는지에 대한 속성값으로, 다음의 값의 OR된 값이 저장된다.
    // Bitmask OR'ing --> |||
    [<Flags>]
    type AttributeFlag =
        | HAS_CONFIG_GLOBAL_VAR     = 0x00000001
        | HAS_CONFIG_ACCESS_VAR     = 0x00000002
        | HAS_CONFIG_DIRECT_COMMENT = 0x00000004

        | HAS_RESOURCE_GLOBAL_VAR   = 0x00000010
        | HAS_USER_FUN_FB           = 0x00000020
        | HAS_USER_TASK             = 0x00000040

        | HAS_USER_LIBRARY          = 0x00000100
        | HAS_USER_DATA_TYPE        = 0x00000200
        | HAS_LOCAL_VAR             = 0x00000400

        | HAS_BASIC_PARA            = 0x00001000
        | HAS_IO_PARA               = 0x00002000
        | HAS_INTERNAL_PARA         = 0x00004000 // 2005.7 XGB 추가
        | HAS_LOCAL_ETH_PARA        = 0x00008000 // NGP2000 Ethernet

        | HAS_REDUNDANCY_PARA       = 0x00010000 // 2007.8.7 이중화 para
        | HAS_PB_POOL               = 0x00020000 // 2011.5.26
        | HAS_NETWORK_PARA          = 0x00020000 // 2010.10.27 CANopen 추가
        | HAS_EXTRA_INFO            = 0x00080000 // serialize extension

        | IEC_CONFIG                = 0x10000000 // IEC 형
        | SAFETY_CONFIG             = 0x20000000 //SAFETY 형

    /// 컨피그레이션에 대한 유형 값
    type Kind =
        | SYSCON_TYPE_UNKNOWN     = 0
        | SYSCON_TYPE_PLC         = 1
        | SYSCON_TYPE_HMI         = 2
        | SYSCON_TYPE_INV         = 3
        | SYSCON_TYPE_NETWORK     = 4
        | SYSCON_TYPE_MOTION      = 5
        | SYSCON_TYPE_EXT_ADAPTER = 6
        | SYSCON_TYPE_FAKE        = 100
    /// 변수 유형 (LocalVar 및 GlobalVariable 둘다 동일한 유형을 사용
    module Variable =
        type Kind =
            | VAR_NONE              = 0
            | VAR                   = 1
            | VAR_CONSTANT          = 2
            | VAR_INPUT             = 3
            | VAR_OUTPUT            = 4
            | VAR_IN_OUT            = 5
            | VAR_GLOBAL            = 6
            | VAR_GLOBAL_CONSTANT   = 7
            /// Global 에 선언된 변수를 local 에서 사용하고자 할 때 추가되는 type
            /// 특정 변수(g)를 global 화 하면 Global block 에 VAR_GLOBAL 로 g 가 추가되고,
            /// 해당 변수 g 를 사용하는 local block 에 VAR_EXTERNAL 로 g 가 추가된다.
            | VAR_EXTERNAL          = 8
            | VAR_EXTERNAL_CONSTANT = 9
            | VAR_RETURN            = 10
            | VAR_GOTO_S1           = 11
            | VAR_TRANS             = 12
            ///// NEVER change the enum value of NULL1 !!!
            //| NULL1               = 0
            ///// It influences GlobalVarMem allocation!
            //| TYPE                = 1
            //| VAR_IN_OUT          = 2
            //| VAR_IN              = 3
            //| VAR_OUT             = 4
            //| VAR_OUT_RETAIN      = 5
            //| VAR                 = 6
            //| VAR_CONSTANT        = 7
            //| VAR_CONSTANT_RETAIN = 8
            //| VAR_RETAIN          = 9
            ///// if PLC_TYPE != 1
            //| VAR_EXTERN          = 10
            //| VAR_GLOBAL          = 11
            //| VAR_GLOBAL_CONSTANT = 12
            ///// used only in BODY compile
            //| I_DIRECT_ADDRESS    = 13
            ///// used only in BODY compile
            //| Q_DIRECT_ADDRESS    = 14
            ///// used only in BODY compile
            //| M_DIRECT_ADDRESS    = 15
            ///// used only in BODY compile
            //| FUNCTION_KIND       = 16
            ///// used as Function block name & output of FUN
            //| FUNCNAME            = 17
            //| SFC_TRANS           = 18
            ///// when string constant is used in FUN
            //| FUN_STRCONST        = 19
            ///// following are for GM1 extern var!!
            //| VAR_EXT_IN          = 20
            //| VAR_EXT_OUT         = 21
            //| VAR_EXT_IN_OUT      = 22
            //| ARY_EXT_IN          = 23
            //| ARY_EXT_OUT         = 24
            //| ARY_EXT_IN_OUT      = 25
            ///// 98/2/12 초기스텝으로 돌아가는 GOTO_S1 변수에만 쓰이는 타입 .
            //| SFC_STEP1           = 26
        //type Type =
        //    FB명, UDT명, ARRAY[1..2,1..3] OF BOOL, WORD, DWORD,...

        ///변수의 내부 속성
        [<Flags>]
        type State =
            | STATE_RETAIN   = 1
            | STATE_USEDIT   = 2 // 사용 유무는 PLC 에 다운로드하지 않는다 . ( 체크섬이 변경되는 현상발생 )(2016.12.08)
            | STATE_READONLY = 4
            | STATE_SPECIAL  = 8
    module POU =
        module Program =
            /// 프로그램에 대한 버전으로 현재는 0x100
            let version = 0x100
            /// 프로그램 유형
            type Kind =
                | LD_EDITOR          = 0
                | IL_EDITOR          = 1
                | SFC_EDITOR         = 2
                | SFC_MANAGER_EDITOR = 3
                | ST_EDITOR          = 4
                | FBD_EDITOR         = 5
                | PD_EDITOR          = 6
                | GCODE_EDITOR       = 7
                | LIB_EDITOR         = 8
                | ILT_EDITOR         = 9 // IL Text editor
            // Program / Body / LDRoutine
            module LDRoutine =
                /// LD 프로그램 상에서의 구성요소에 대한 식별자
                module ElementType =
                    let [<Literal>] LDElementMode_Start    = 0
                    let [<Literal>] LineType_Start         = 0
                    let [<Literal>] VertLineMode           = 0 // LineType_Start '|'
                    let [<Literal>] HorzLineMode           = 1 // '-'
                    let [<Literal>] MultiHorzLineMode      = 2 // '-->>'
                    ///addonly hereadditional line type device.
                    let [<Literal>] LineType_End           = 5

                    let [<Literal>] ContactType_Start      = 6
                    let [<Literal>] ContactMode            = 6 // ContactType_Start // '-| |-'
                    let [<Literal>] ClosedContactMode      = 7 // '-|/|-'
                    let [<Literal>] PulseContactMode       = 8 // '-|P|-'
                    let [<Literal>] NPulseContactMode      = 9// '-|N|-'

                    let [<Literal>] PulseClosedContactMode  = 10// '-|/P|-'
                    let [<Literal>] NPulseClosedContactMode = 11// '-|/N|-'


                    ///addonly hereadditional contact type device.
                    let [<Literal>] ContactType_End        = 13

                    let [<Literal>] CoilType_Start         = 14
                    let [<Literal>] CoilMode               = 14 // CoilType_Start // '-( )-'
                    let [<Literal>] ClosedCoilMode         = 15 // '-(/)-'
                    let [<Literal>] SetCoilMode            = 16 // '-(S)-'
                    let [<Literal>] ResetCoilMode          = 17 // '-(R)-'
                    let [<Literal>] PulseCoilMode          = 18 // '-(P)-'
                    let [<Literal>] NPulseCoilMode         = 19 // '-(N)-'
                    ///addonly hereadditional coil type device.
                    let [<Literal>] CoilType_End           = 30

                    let [<Literal>] FunctionType_Start     = 31
                    let [<Literal>] FuncMode               = 32
                    let [<Literal>] FBMode                 = 33 // '-[F]-'
                    let [<Literal>] FBHeaderMode           = 34 // '-[F]-' : Header
                    let [<Literal>] FBBodyMode             = 35 // '-[F]-' : Body
                    let [<Literal>] FBTailMode             = 36 // '-[F]-' : Tail
                    let [<Literal>] FBInputMode            = 37
                    let [<Literal>] FBOutputMode           = 38
                    ///addonly hereadditional function type device.
                    let [<Literal>] FunctionType_End       = 45

                    let [<Literal>] BranchType_Start       = 51
                    let [<Literal>] SCALLMode              = 52
                    let [<Literal>] JMPMode                = 53
                    let [<Literal>] RetMode                = 54
                    let [<Literal>] SubroutineMode         = 55
                    let [<Literal>] BreakMode              = 56
                    let [<Literal>] ForMode                = 57
                    let [<Literal>] NextMode               = 58
                    ///addonly hereadditional branch type device.
                    let [<Literal>] BranchType_End         = 60

                    let [<Literal>] CommentType_Start      = 61
                    let [<Literal>] InverterMode           = 62 // '-*-'
                    let [<Literal>] RungCommentMode        = 63 // 'rung comment'
                    let [<Literal>] OutputCommentMode      = 64 // 'output comment'
                    let [<Literal>] LabelMode              = 65
                    let [<Literal>] EndOfPrgMode           = 66
                    let [<Literal>] RowCompositeMode       = 67 // 'row'
                    let [<Literal>] ErrorComponentMode     = 68
                    let [<Literal>] NullType               = 69
                    let [<Literal>] VariableMode           = 70
                    let [<Literal>] CellActionMode         = 71
                    let [<Literal>] RisingContact          = 72 //add dual    xg5000 4.52
                    let [<Literal>] FallingContact         = 73 //add dual    xg5000 4.52
                    ///addonly hereadditional comment type device.
                    let [<Literal>] CommentType_End        = 90

                    /// vertical function(function & function block) related
                    let [<Literal>] VertFunctionType_Start = 100
                    let [<Literal>] VertFuncMode           = 101
                    let [<Literal>] VertFBMode             = 102
                    let [<Literal>] VertFBHeaderMode       = 103
                    let [<Literal>] VertFBBodyMode         = 104
                    let [<Literal>] VertFBTailMode         = 105
                    /// add additional vertical function type device here
                    let [<Literal>] VertFunctionType_End   = 109
                    let [<Literal>] LDElementMode_End      = 110

                    let [<Literal>] Misc_Start             = 120
                    let [<Literal>] ArrowMode              = 121
                    let [<Literal>] Misc_End               = 122

                type ElementType = int


[<RequireQualifiedAccess>]
module XgiConstants =
    let [<Literal>] FunctionNameMove = "MOVE"

module internal FB =
    open FBText

    /// Hexadecimal encoding type 을 decode
    let decodeVarType (hex:string) : CheckType =    // hex = 0x00000001
        Convert.ToInt32(hex, 16) |> enum<CheckType>

    let xgiFunctionInfoDic =
        let dic =
            FBtext.Split('\n')
            |> Seq.map (fun s -> s.Trim())
            |> Seq.filter (fun s -> not <| s.StartsWith("#"))
            |> Seq.splitOn (fun s1 s2 -> s1.IsEmpty() && not <| s2.IsEmpty())
            |> Seq.map (Seq.filter (String.IsNullOrEmpty >> not) >> Array.ofSeq)
            |> Seq.map (fun fb ->
                let fName =
                    let fnameLine = fb |> Array.find (fun l -> l.StartsWith("FNAME:"))
                    match fnameLine with
                    | RegexPattern @"FNAME: (\w+)$" [fn] ->
                        fn
                    | _ -> failwithlog "ERROR"
                fName, fb )
            |> dict |> Dictionary
        dic

    let isValidFunctionName = xgiFunctionInfoDic.ContainsKey
    let getFunctionDeails functionName = xgiFunctionInfoDic[functionName]

    type FunctionParameterSpec = {
        Name:string
        IsInput:bool
        CheckType:CheckType
    }
    /// e.g ["CD, 0x00200001, , 0"; "LD, 0x00200001, , 0"; "PV, 0x00200040, , 0"]
    let getFunctionParameterSpecs functionName =
        [|
            let details = getFunctionDeails functionName
            for d in details do
                match d with
                | RegexPattern @"VAR_IN: ([^,]+), ([^,]+)" [name; hex] ->
                    { IsInput=true; Name=name; CheckType=decodeVarType hex }
                | RegexPattern @"VAR_OUT: ([^,]+), ([^,]+)" [name; hex] ->
                    { IsInput=false; Name=name; CheckType=decodeVarType hex }
                | _ -> ()
        |]

    let getFunctionInputSpecs  functionName = getFunctionParameterSpecs functionName |> filter (fun p -> p.IsInput)
    let getFunctionOutputSpecs functionName = getFunctionParameterSpecs functionName |> filter (fun p -> not p.IsInput)

    let getFunctionInputArity functionName = (getFunctionInputSpecs functionName).Count()
    let getFunctionOutputArity functionName = (getFunctionOutputSpecs functionName).Count()
    let getFunctionHeight functionName = max (getFunctionInputArity functionName) (getFunctionOutputArity functionName)

    /// getFBXML FB 이름 기준으로 XML 저장 파라메터를 읽음
    ///
    /// return sample =
    /// "#BEGIN_FUNC: ADD2_INT&#xA;FNAME: ADD&#xA;TYPE: function&#xA;INSTANCE: ,&#xA;INDEX: 71&#xA;COL_PROP: 1&#xA;SAFETY: 0&#xA;VAR_IN: EN, 0x00200001, , 0&#xA;VAR_IN: IN1, 0x00200040, , 0&#xA;VAR_IN: IN2, 0x00200040, , 0&#xA;VAR_OUT: ENO, 0x00000001,&#xA;VAR_OUT: OUT, 0x00000040,&#xA;#END_FUNC &#xA;"
    let getFBXmlParam (functionName, functionNameTarget) instance index =
        let xmlLineFeed = "&#xA"
        let fbXml =
            xgiFunctionInfoDic[functionName]
            |> Array.filter(fun x -> not <| x.StartsWith("#"))
            |> Array.map (function
                | StartsWith "FNAME: "    -> $"FNAME: {functionNameTarget}"
                | StartsWith "INSTANCE: " -> $"INSTANCE: {instance}"
                | StartsWith "INDEX: "    -> $"INDEX: {index}"
                | str -> str)
            |> String.concat $"{xmlLineFeed};"
        fbXml + $" {xmlLineFeed};"

    /// getFBXML FB 이름 기준으로 Input para 갯수를 읽어옴
    let getFBInCount (functionName:string) =
        let lstIn =
            xgiFunctionInfoDic[functionName]
            |> Array.filter (fun f -> not (f.StartsWith("VAR_IN: EN")))
            |> Array.filter (fun f -> f.StartsWith("VAR_IN: ") || f.StartsWith("VAR_IN_OUT: "))
        let onlyIn =
            lstIn
            |> Array.filter (fun f -> f.StartsWith("VAR_IN: "))

        onlyIn.Length, lstIn.Length

    /// getFBXML FB 이름 기준으로 index 가져옴
    let getFBIndex (functionName:string) =
        xgiFunctionInfoDic[functionName]
        |> Array.find(fun f -> f.StartsWith("INDEX: "))
        |> fun f -> f.Replace("INDEX: ", "") |> int

