namespace Ev2.Gen.IR.Unused

open System

[<AutoOpen>]
module IRDataTypes =

    /// Struct field definition
    type StructField = {
        Name: string
        FieldType: string  // Type은 F# 예약어이므로 FieldType 사용
        DefaultValue: obj option
    }

    /// User-Defined Type (UDT) - Struct
    type UDT = {
        Name: string
        StructType: string  // "STRUCT" (향후 "UNION" 등 확장 가능)
        Fields: StructField list
    }

    /// Enumeration definition
    type EnumDef = {
        Name: string
        BaseType: string  // "INT", "DINT", etc.
        Literals: string list
    }

    /// Type Alias definition
    type TypeAlias = {
        Name: string
        UnderlyingType: string
        Meta: Map<string, MetaValue> option
    }

    /// DataTypes collection
    type DataTypes = {
        UDT: UDT list
        Enums: EnumDef list
        Aliases: TypeAlias list
    }
