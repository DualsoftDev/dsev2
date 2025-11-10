namespace Ev2.Cpu.Runtime

open System
open Ev2.Cpu.Core

// ═════════════════════════════════════════════════════════════════════
// 메모리 제네릭 확장 (Generic Memory Extensions)
// ═════════════════════════════════════════════════════════════════════
// OptimizedMemory에 타입 안전 접근자 추가
// 기존 obj 기반 Get/Set은 유지하고 제네릭 버전을 추가
// ═════════════════════════════════════════════════════════════════════

/// <summary>OptimizedMemory 제네릭 확장 메서드</summary>
[<AutoOpen>]
module MemoryExtensions =

    type OptimizedMemory with
        /// <summary>타입 안전 변수 값 조회</summary>
        /// <param name="name">변수 이름</param>
        /// <returns>타입이 지정된 변수 값 (없으면 기본값)</returns>
        /// <remarks>
        /// 예: memory.GetTyped&lt;int&gt;("Counter")
        /// 타입 불일치 시 InvalidCastException 발생
        /// </remarks>
        member this.GetTyped<'T>(name: string) : 'T =
            let value = this.Get(name)
            if isNull value then
                Unchecked.defaultof<'T>
            else
                try
                    value :?> 'T
                with
                | :? InvalidCastException ->
                    let expectedType = typeof<'T>.Name
                    let actualType = value.GetType().Name
                    failwithf "Type mismatch for variable '%s': expected %s but got %s"
                        name expectedType actualType

        /// <summary>타입 안전 변수 값 설정</summary>
        /// <param name="name">변수 이름</param>
        /// <param name="value">설정할 값</param>
        /// <remarks>
        /// 예: memory.SetTyped("Counter", 42)
        /// 변수가 없으면 예외 발생 (명시적 선언 필요)
        /// </remarks>
        member this.SetTyped<'T>(name: string, value: 'T) =
            this.Set(name, box value)

        /// <summary>타입 안전 변수 값 강제 설정</summary>
        /// <param name="name">변수 이름</param>
        /// <param name="value">설정할 값</param>
        /// <remarks>
        /// 예: memory.SetForcedTyped("_EdgeFlag", true)
        /// 쓰기 보호 무시, Internal 자동 선언
        /// </remarks>
        member this.SetForcedTyped<'T>(name: string, value: 'T) =
            this.SetForced(name, box value)

        /// <summary>Tag를 사용한 변수 값 조회</summary>
        /// <param name="tag">제네릭 Tag</param>
        /// <returns>타입이 지정된 변수 값</returns>
        /// <remarks>
        /// 예: memory.GetByTag(Tag&lt;int&gt;.Int("Counter"))
        /// Tag의 타입과 변수 타입이 일치해야 함
        /// </remarks>
        member this.GetByTag<'T>(tag: Tag<'T>) : 'T =
            this.GetTyped<'T>(tag.Name)

        /// <summary>Tag를 사용한 변수 값 설정</summary>
        /// <param name="tag">제네릭 Tag</param>
        /// <param name="value">설정할 값</param>
        member this.SetByTag<'T>(tag: Tag<'T>, value: 'T) =
            this.SetTyped<'T>(tag.Name, value)

        /// <summary>Variable을 사용한 메모리 조회</summary>
        /// <param name="variable">제네릭 Variable</param>
        /// <returns>타입이 지정된 변수 값</returns>
        /// <remarks>
        /// 예: let counter = Variable&lt;int&gt;("Counter")
        ///     memory.GetByVariable(counter)
        /// </remarks>
        member this.GetByVariable<'T>(variable: Variable<'T>) : 'T =
            this.GetTyped<'T>(variable.Name)

        /// <summary>Variable을 사용한 메모리 설정</summary>
        /// <param name="variable">제네릭 Variable</param>
        /// <param name="value">설정할 값</param>
        member this.SetByVariable<'T>(variable: Variable<'T>, value: 'T) =
            this.SetTyped<'T>(variable.Name, value)

        /// <summary>제네릭 변수 선언 및 초기화</summary>
        /// <param name="name">변수 이름</param>
        /// <param name="area">메모리 영역</param>
        /// <param name="initialValue">초기값 (옵션)</param>
        /// <returns>할당된 인덱스</returns>
        /// <remarks>
        /// 예: memory.DeclareTyped&lt;int&gt;("Counter", MemoryArea.Local, 0)
        /// 타입은 typeof&lt;'T&gt;로부터 자동 추론
        /// </remarks>
        member this.DeclareTyped<'T>(name: string, area: MemoryArea, ?initialValue: 'T) =
            let dataType = typeof<'T>
            let index = this.DeclareVariable(name, dataType, area)

            match initialValue with
            | Some value -> this.SetTyped<'T>(name, value)
            | None -> ()

            index

        /// <summary>Variable 객체를 사용한 변수 선언</summary>
        /// <param name="variable">Variable 객체</param>
        /// <param name="area">메모리 영역</param>
        /// <returns>할당된 인덱스</returns>
        /// <remarks>
        /// 예: let counter = Variable&lt;int&gt;("Counter", 0)
        ///     memory.DeclareFromVariable(counter, MemoryArea.Local)
        /// </remarks>
        member this.DeclareFromVariable<'T>(variable: Variable<'T>, area: MemoryArea) =
            this.DeclareTyped<'T>(variable.Name, area, variable.Value)

        /// <summary>타입 안전 변수 값 조회 시도</summary>
        /// <param name="name">변수 이름</param>
        /// <returns>Some value (성공) 또는 None (실패/타입 불일치)</returns>
        member this.TryGetTyped<'T>(name: string) : 'T option =
            try
                let value = this.Get(name)
                if isNull value then
                    None
                else
                    match box value with
                    | :? 'T as typedValue -> Some typedValue
                    | _ -> None
            with
            | _ -> None

// Storage 확장 메서드는 Core/Variables.fs에서 정의해야 합니다.
// (Storage가 Core에 정의되어 있으므로)
