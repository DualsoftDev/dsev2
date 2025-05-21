namespace Ev2.Core.FS

open Dual.Common.Base

[<AutoOpen>]
module AppSettingsModule =
    type AppSettings() as this =
        do
            AppSettings.TheAppSettings <- this

        static member val TheAppSettings = getNull<AppSettings>() with get, set
        member val ConnectionString:string = null with get, set
        member val DatabaseWatchdogIntervalSec = 5 with get, set
        member val UseUtcTime = false with get, set

