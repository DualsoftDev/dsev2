namespace Ev2.Core.FS

open log4net

open Dual.Common.Base

module ModuleInitializer =
    let mutable private initailized = false
    let Initialize(logger: ILog) =
        if not initailized then
            initailized <- true

            Dual.Common.Base.FS.ModuleInitializer.Initialize(logger)

            fwdOnSerializing <- onSerializing
            fwdOnDeserialized <- onDeserialized
            ()
