namespace Dual.Plc2DS

module ModuleInitializer =
    let Initialize() =
        Dual.Common.Base.CS.ModuleInitializer.Initialize();
        ReaderModule.initialize()
