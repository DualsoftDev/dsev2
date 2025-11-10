namespace PLC.Convert.MX

open System
open System.Collections.Generic

[<AutoOpen>]
module CSVTypes =

    // 프로그램의 한 라인을 표현하는 타입
    type ProgramCSVLine = {
        StepNo: int option           // 실행 단계 번호 (옵션)
        LineStatement: string        // 실행 명령문
        Instruction: string          // 명령어
        Arguments: Argument array    // 인수 배열, 각 인수는 `Argument` 타입
    }

    // 주소(Device) 정보를 저장하는 타입
    and Device = {
        Name: string                 // 주소 이름
        Comment: string              // 주소에 대한 설명 또는 주석
    }    

    // 다양한 인수(Argument) 타입을 표현하는 타입
    and Argument =
        | Contact of Device           // 주소 정보를 담고 있는 Contact 타입
        | String of string            // 문자열 형태의 인수
        | Integer of int              // 정수 형태의 인수
        with
            // 각 인수를 텍스트로 변환하는 메서드
            member this.ToText() =
                match this with
                | Contact dev -> dev.Name
                | String str -> str
                | Integer n -> string n
            member this.IsMemory() =
                match this with
                | Contact _ -> true
                | _ -> false

    // IO 장치(DeviceIO) 정보를 저장하는 타입
    type DeviceIO = {
        Slot: string                  // 주소가 속한 슬롯
        Type: string                  // 주소 유형
        Points: int                   // 주소 포인트 수
        StartXY: int                  // 주소의 시작 좌표
    }

    // 원격 IO 장치(DeviceRemoteIO) 정보를 저장하는 타입
    type DeviceRemoteIO = {
        RemoteType: string            // 원격 장치 유형
        StartX: string                // X 좌표의 시작 위치
        StartY: string                // Y 좌표의 시작 위치
    }

    // 주석(Comment)을 저장하는 딕셔너리 타입
    type CommentDictionary = Dictionary<string, string>  // 키는 장치 주소, 값은 코멘트
    type GlobalLabelDictionary = Dictionary<string, string>  // 키는 장치 라벨, 값은 타입

    // Rung: 프로그램 내의 한 단계 혹은 라인을 나타내는 배열 타입
    type Rung = ProgramCSVLine array

    // 함수 블록(POU)의 분석 결과를 저장하는 타입
    type POUParseResult = {
        Name: string                  // 함수 블록 이름
        Rungs: Rung array             // 함수 블록 내의 Rung 배열
    }
    
    // 예외 명령어 리스트: 특정 명령어 집합을 정의
    let ListExCMD = [|"FOR"; "NEXT"; "BREAK"; "CALL"; "INIT_DONE"; "JMP"; "RET"; "SBRT"; "END"|] |> HashSet
    // 인수가 필요한  명령어 리스트
    let ListExCMDHasPara = [|"FOR"; "CALL";  "JMP"; "SBRT"  |] |> HashSet
    // 단일 명령어로만 사용되는 예외 명령어 리스트
    let ListExCMDSingleLine = [|"FOR"; "NEXT"; "RET"; "END" ; "SBRT"|] |> HashSet

    // 명령어가 종료되지 않는(연속되는) 명령어 리스트
    let ListNotFinishs = [|
                     "LD"    ;"LD="     ;"LDD>="    ;"LD$="     ;"LDD<>"
                    ;"LDI"   ;"AND="    ;"ANDD>="   ;"AND$=>"   ;"ANDD<>"
                    ;"AND"   ;"OR="     ;"ORD>="    ;"OR$="     ;"ORD<>"
                    ;"ANI"   ;"LD<>"    ;"LDE="     ;"LD$<>"    ;"LDD>"
                    ;"OR"    ;"AND<>"   ;"ANDE="    ;"AND$<>"   ;"ANDD>"
                    ;"ORI"   ;"OR<>"    ;"ORE="     ;"OR$<>"    ;"ORD>"
                    ;"LDP"   ;"LD>"     ;"LDE<>"    ;"LD$>"     ;"LDD<="
                    ;"LDF"   ;"AND>"    ;"ANDE<>"   ;"AND$>="   ;"ANDD<="
                    ;"ANDP"  ;"OR>"     ;"ORE<>"    ;"OR$>"     ;"ORD<="
                    ;"ANDF"  ;"LD<="    ;"LDE>"     ;"LD$<="    ;"LDD<"
                    ;"LDFI"  ;"LDPI"    ;"ANDFI"    ;"ANDPI"    ;"ORFI"     ;"ORPI"
                    ;"ORP"   ;"AND<="   ;"ANDE>"    ;"AND$<="   ;"ANDD<"
                    ;"ORF"   ;"OR<="    ;"ORE>"     ;"OR$<="    ;"ORD<"
                    ;"ANB"   ;"LD<"     ;"LDE<="    ;"LD$<"
                    ;"ORB"   ;"AND<"    ;"ANDE<="   ;"AND$<"
                    ;"MPS"   ;"OR<"     ;"ORE<="    ;"OR$<"
                    ;"MRD"   ;"LD>="    ;"LDE<"     ;"LD$>="
                    ;"MPP"   ;"AND>="   ;"ANDE<"    ;"AND$>="
                    ;"INV"   ;"OR>="    ;"ORE<"     ;"OR$>="
                    ;"MEP"   ;"LDD="    ;"LDE>="
                    ;"MEF"   ;"ANDD="   ;"ANDE>="
                    ;"EGP"   ;"ORD="    ;"ORE>="
                    ;"EGF"
    |] 
