namespace Ev2.Core.FS.Extension

open System
open System.Reflection
open Dual.Common.Base

/// 확장 시스템 헬퍼 함수들
[<AutoOpen>]
module ExtensionHelpers =

    /// Option 타입 변환 헬퍼
    let inline optionOfObj (obj: obj) : 'T option =
        if isItNull obj then None
        else Some (obj :?> 'T)

    /// 안전한 타입 캐스팅
    let inline tryCast<'T> (obj: obj) : 'T option =
        match obj with
        | :? 'T as t -> Some t
        | _ -> None

    /// 타입이 특정 인터페이스를 구현하는지 확인
    let implementsInterface (interfaceType: Type) (targetType: Type) =
        targetType.GetInterfaces() |> Array.exists (fun i -> i = interfaceType)

    /// 타입이 특정 기본 클래스를 상속하는지 확인
    let inheritsFrom (baseType: Type) (targetType: Type) =
        let rec checkBase (t: Type) =
            if isItNull t || t = typeof<obj> then false
            elif t = baseType then true
            else checkBase t.BaseType

        checkBase targetType

    /// 속성이 읽기/쓰기 가능한지 확인
    let isReadWriteProperty (prop: PropertyInfo) =
        prop.CanRead && prop.CanWrite

    /// 기본값 생성
    let getDefaultValue (targetType: Type) =
        if targetType.IsValueType then
            Activator.CreateInstance(targetType)
        else
            null

    /// 타입 이름 포맷팅
    let rec formatTypeName (t: Type) =
        if t.IsGenericType then
            let name = t.Name.Substring(0, t.Name.IndexOf('`'))
            let args = t.GetGenericArguments() |> Array.map formatTypeName |> String.concat ", "
            sprintf "%s<%s>" name args
        else
            t.Name

///// 확장 시스템 초기화 모듈
//module ExtensionSystem =

//    let mutable private isInitialized = false

//    /// 확장 시스템 리셋
//    let reset() =
//        TypeRegistryModule.reset()
//        PropertyMapper.clearCache()
//        isInitialized <- false
//        logDebug "Extension system reset"

//    /// 시스템 상태 가져오기
//    let getStatus() =
//        {|
//            IsInitialized = isInitialized
//            Registry = TypeRegistryModule.getStats()
//            PropertyMapperCache = PropertyMapper.getCacheStats()
//        |}