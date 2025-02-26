namespace Dual.Ev2


open Dual.Common.Base.FS
open Dual.Common.Core.FS

module ModuleInitializer =
    let Initialize() =
        CpusEvent.initialize()
