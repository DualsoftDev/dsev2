namespace Dual.Plc2DS

open System.Text.RegularExpressions
open Dual.Common.Core.FS
open Dual.Plc2DS

type XmlReader =
    static member ReadLs(filePath:string): LS.PlcTagInfo[] = LS.XmlReader.ReadTags(filePath)
