#r "nuget: CsvHelper"

open System
open System.IO
open CsvHelper
open CsvHelper.Configuration
open System.Globalization
open System.Linq.Expressions

// Define the DeviceComment type
type DeviceComment(device: string, comment: string, ?label: string) =
    member val Device = device with get, set
    member val Comment = comment with get, set
    member val Label = label |? null with get, set

// Custom CSV Configuration
let csvConfig = CsvConfiguration(CultureInfo.InvariantCulture)
csvConfig.HasHeaderRecord <- false // We will handle headers manually
csvConfig.Delimiter <- "    " // Default delimiter is tab, change if needed

// Function to read case 1 and case 3 (Skip first row, read header from second row, process data from third row)
let readCsvWithSkippedFirstRow (data: string) =
    use reader = new StringReader(data)
    use csv = new CsvReader(reader, csvConfig)

    csv.Read() |> ignore // Skip first row
    csv.Read() |> ignore // Move to header row
    csv.ReadHeader()

    let classMap = new DefaultClassMap<DeviceComment>()
    classMap.Map(Expression.Lambda<Func<DeviceComment, string>>(fun d -> d.Device, Expression.Parameter(typeof<DeviceComment>, "d"))).Name("Device Name", "Device") |> ignore
    classMap.Map(Expression.Lambda<Func<DeviceComment, string>>(fun d -> d.Comment, Expression.Parameter(typeof<DeviceComment>, "d"))).Name("Comment") |> ignore
    csv.Context.RegisterClassMap(classMap)

    csv.GetRecords<DeviceComment>() |> Seq.toList

// Function to read case 2 (First row is the header)
let readCsvWithHeader (data: string) =
    let config = CsvConfiguration(CultureInfo.InvariantCulture)
    config.Delimiter <- "," // Case 2 uses comma as delimiter

    use reader = new StringReader(data)
    use csv = new CsvReader(reader, config)

    let classMap = new DefaultClassMap<DeviceComment>()
    classMap.Map(Expression.Lambda<Func<DeviceComment, string>>(fun d -> d.Device, Expression.Parameter(typeof<DeviceComment>, "d"))).Name("Device") |> ignore
    classMap.Map(Expression.Lambda<Func<DeviceComment, string>>(fun d -> d.Comment, Expression.Parameter(typeof<DeviceComment>, "d"))).Name("Comment") |> ignore
    classMap.Map(Expression.Lambda<Func<DeviceComment, string>>(fun d -> d.Label, Expression.Parameter(typeof<DeviceComment>, "d"))).Name("Label").Optional() |> ignore
    csv.Context.RegisterClassMap(classMap)

    csv.GetRecords<DeviceComment>() |> Seq.toList

// Example CSV data
let case1Data = """P20_240109
"Device Name"    "Comment"
"X5C"    "FR:U4   Ethernet이상신호"
"X7C"    "FR:U6   Ethernet이상신호"
"X0A0"    "CC-LINK #1 Error"
"X0A1"    "CC-LINK #1 DATA LINK"
"X0AF"    "CC-LINK #1 Ready"
"X0C0"    "CC-LINK #2 Error"""

let case2Data = """Device,Label,Comment
M1000,,#312    차종    RB
M1001,,#312    차종    SP
M1002,,#312    차종    MC
M1004,,#312    차종    3DR"""

let case3Data = """4.2.1 Command PGM
Device Name    Comment
SD2037    로깅 설정 No.10 전송 기능 에러"""

// Process CSV data
let parsedCase1 = readCsvWithSkippedFirstRow case1Data
let parsedCase2 = readCsvWithHeader case2Data
let parsedCase3 = readCsvWithSkippedFirstRow case3Data

// Print results
parsedCase1 |> List.iter (fun d -> printfn "Device: %s, Comment: %s" d.Device d.Comment)
parsedCase2 |> List.iter (fun d -> printfn "Device: %s, Label: %s, Comment: %s" d.Device (d.Label |> Option.defaultValue "None") d.Comment)
parsedCase3 |> List.iter (fun d -> printfn "Device: %s, Comment: %s" d.Device d.Comment)