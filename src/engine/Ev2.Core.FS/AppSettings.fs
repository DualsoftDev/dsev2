namespace Ev2.Core.FS

open Dual.Common.Base
open Dual.Common.Db.FS
open Newtonsoft.Json
open System.Runtime.Serialization

[<AutoOpen>]
module AppSettingsModule =
    type AppSettings() as this =
        do
            AppSettings.TheAppSettings <- this

        static member val TheAppSettings = getNull<AppSettings>() with get, set

        [<JsonProperty(PropertyName = "Database")>] member val internal NjDbProvider = getNull<NjDbProvider>() with get, set
        [<JsonIgnore>] member val DbProvider = getNull<DbProvider>() with get, set

        [<OnSerializing>]
        member x.OnSerializing(_context:StreamingContext) =
            x.NjDbProvider <- NjDbProvider.FromDbProvider x.DbProvider
        [<OnSerialized>]
        member x.OnSerialized(_context:StreamingContext) =
            x.DbProvider <- x.NjDbProvider.ToDbProvider()

        member val DatabaseWatchdogIntervalSec = 5 with get, set
        member val UseUtcTime = false with get, set


