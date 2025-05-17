namespace Dual.Plc2DS

open log4net

module ModuleInitializer =
    let Initialize(logger:ILog) =
        Dual.Common.Base.FS.ModuleInitializer.Initialize(logger);
        ReaderModule.initialize()
