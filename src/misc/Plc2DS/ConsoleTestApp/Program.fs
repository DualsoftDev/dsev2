open Dual.Common.AppSettings
open Dual.Common.Core.FS
open Dual.Plc2DS
open System
open System.IO
open Dual.Common.Base.FS

// For more information see https://aka.ms/fsharp-console-apps
printfn "Hello from F#"

let appSettingsPath =
    let baseDir = AppDomain.CurrentDomain.BaseDirectory;
    Path.Combine(baseDir, "appsettings.json")
//let appSettings = JsonSetting.GetSectionEx<AppSettings>(appSettingsPath, "AppSettings");
let appSettings = EmJson.FromJson<AppSettings>(File.ReadAllText(appSettingsPath));
noop()