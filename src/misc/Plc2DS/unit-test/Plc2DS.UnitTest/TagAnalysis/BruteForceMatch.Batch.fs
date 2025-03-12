namespace T


open System.IO

open NUnit.Framework

open Dual.Common.UnitTest.FS

open Dual.Plc2DS
open Dual.Common.Core.FS

module BruteForceMatchBatch =
    let dataDir = "Z:/dsev2/src/misc/Plc2DS/unit-test/Plc2DS.UnitTest/Samples/LS/Autoland광명2"
    let sm = Semantic()

    let nameIt(prefix: string, start: int, ``end``: int): string[] =
        Array.init (``end`` - start + 1) (fun i -> sprintf "%s%d" prefix (start + i))

    let flows = [|
        "BB"; "BR"; "CRP"; "DNDL"; "EMS";
        yield! nameIt ("HOP", 1, 9)
        "MCP"; "ROBOT";
        for i in 1..9 do
        for j in 0..9 do
            yield! nameIt ($"S{i}{j}", 0, 9)
        //yield! nameIt ("S30", 1, 9)
        //yield! nameIt ("S35", 1, 9)
        "SKD"; "SKID";
        "ROOF"
        "UPDL"
    |]

    let devices = [|
        "BUZZ";
        "CARR"; "COM"; "CYL"; "EPB"; "GATE";
        "INV"
        "LASER"
        yield! nameIt ("RB", 1, 9)
        yield! nameIt ("RBT", 1, 9)
        yield! nameIt ("PB", 1, 9)
        yield! nameIt ("PBLI", 1, 9)
        yield! nameIt ("SPB", 1, 9)
        yield! nameIt ("SSP", 1, 9)

        "PRS";
        "ROBOT";
        "SKD";
        "WPRS"; "WRS";
    |]

    let actions = [|
        "AUTO_START"; "BRK_ON"; "BUZZ_STOP";
        "CHECK_BYPASS"; "CHECK_ERROR";
        "CLOSE"; "EM_STOP";
        "DEGRADE"
        "EM_STOP"
        "INPUT"; "INPUT_ADDRESS";
        "LAMP_CHECK"
        "READY_ON";
        "TOTAL_ERROR";
        "WELD_COMP"; "WORK_COMP"
    |]
    let defaultSeparators = [| "_"|]
    let defaultDiscards = [| "I"; "Q"|]
    do
        sm.Flows   <- WordSet(flows, ic)
        sm.Devices <- WordSet(devices, ic)
        sm.Actions <- WordSet(actions, ic)

    type T =
        static member MatchRawFDA(name:string): MatchSet[] = StringSearch.MatchRawFDA(name, flows, devices, actions)

        static member ComputeUnmatched(name:string, ?separators:string[], ?discards:string[]) =
            let discards = discards |? defaultDiscards
            let separators = separators |? defaultSeparators
            let rs = T.MatchRawFDA(name)
            if rs.isEmpty() then
                tracefn $"    {name}: Failed to match"
                [||]
            else
                PartialMatch.ComputeUnmatched(name, rs[0].Matches, separators=separators, discards=discards)

    type B() =
        [<Test>]
        member _.``Minimal`` () =
            let inputTags:IPlcTag[] =
                let csv = Path.Combine(dataDir, "BB 메인제어반.csv")
                CsvReader.Read(Vendor.LS, csv, addressFilter = fun addr -> addr.StartsWith("%I"))
            let inputTagNames = inputTags |> map _.GetName()

            let xxx = T.MatchRawFDA("CRP_I_S351_ROBOT_IN_OK")
            noop()



            T.ComputeUnmatched("BR_I_EM_STOP_X") === [|
                //{ Text = "BR"; Start = 0;  Category = DuUnmatched }
                { Text = "X"; Start = 13; Category = DuUnmatched }
            |]
            noop()

            for name in inputTagNames do
                let mss = T.MatchRawFDA(name)
                let unmatched = T.ComputeUnmatched(name, separators=[|"_";|], discards=defaultDiscards)
                let m =
                    if mss.Length > 0 then
                        let m = mss[0].Matches |> map _.Text |> String.concat ","
                        $"[{m}]"
                    else
                        "[]"
                let u =
                    if unmatched.Length > 0 then
                        let u = unmatched |> map (fun pm -> pm.Text) |> String.concat ","
                        $"[{u}]"
                    else
                        "[]"
                tracefn $"{name}: {m}, {u}"
            noop()


