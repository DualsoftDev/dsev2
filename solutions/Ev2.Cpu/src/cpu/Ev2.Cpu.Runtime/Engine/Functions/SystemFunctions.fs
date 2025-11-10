namespace Ev2.Cpu.Runtime

open System

// ─────────────────────────────────────────────────────────────────────
// System Functions
// ─────────────────────────────────────────────────────────────────────
// 시스템 함수: print, now, random
// ─────────────────────────────────────────────────────────────────────

module SystemFunctions =

    let print (args: obj list) (ctx: ExecutionContext option) =
        let output = args |> List.map cachedToString |> String.concat " "
        match ctx with
        | Some c -> Context.trace c output; null
        | None   -> printfn "%s" output; null

    /// Returns current time in milliseconds (NOT seconds)
    /// IEC 61131-3 timer presets use milliseconds (TIME#100ms = 100)
    /// Uses ExecutionContext.TimeProvider for testability (DEFECT-003 fix)
    let now (ctx: ExecutionContext option) =
        match ctx with
        | Some c ->
            let currentTime = c.TimeProvider.UtcNow
            box (currentTime.Ticks / 10_000L)
        | None ->
            // Fallback to system time if no context
            box (DateTime.UtcNow.Ticks / 10_000L)

    let random (args: obj list) =
        let rng = getRng()  // Get thread-local Random instance (NEW-DEFECT-001 fix)
        match args with
        | []                                     -> box (rng.NextDouble())
        | [ :? int as hi ]                       -> box (rng.Next(hi))
        | [ :? int as lo; :? int as hi ]         -> box (rng.Next(lo, hi))
        | _ -> failwith "RANDOM requires 0-2 arguments"
