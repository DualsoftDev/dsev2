namespace PLC.CodeGen.Common

[<AutoOpen>]
module TagTypeModule =
    type TagType =
        | Bit
        | I1
        | I2
        | I4
        | I8
        | F4

    type DataLengthType =
        | Undefined
        | Bit
        | Byte
        | Word
        | DWord
        | LWord
