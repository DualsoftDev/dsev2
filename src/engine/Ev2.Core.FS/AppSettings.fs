namespace Tia.Core.FS

open Newtonsoft.Json
open Dual.Common.Base
open Dual.Common.Core.FS
open System.Runtime.Serialization

[<AutoOpen>]
module AppSettingsModule =
    type AppSettings() as this =
        do
            AppSettings.TheAppSettings <- this

        static member val TheAppSettings = getNull<AppSettings>() with get, set
        member val ConnectionString:string = null with get, set
        member val DatabaseWatchdogIntervalSec = 5 with get, set

