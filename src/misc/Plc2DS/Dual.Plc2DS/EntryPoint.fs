namespace Dual.Plc2DS

module ModuleInitializer =
    let Initialize() =
        Dual.Common.Base.ModuleInitializer.Initialize();
        ReaderModule.initialize()
