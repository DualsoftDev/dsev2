namespace Ev2.Cpu.Runtime

open Ev2.Cpu.Core

// ─────────────────────────────────────────────────────────────────────
// PLC Functions
// ─────────────────────────────────────────────────────────────────────
// PLC 전용 함수: mov, ton, tof, ctu, ctd
// ─────────────────────────────────────────────────────────────────────

module PLCFunctions =

    let mov (args: obj list) (ctx: ExecutionContext option) =
        match args, ctx with
        | [src; :? string as dst], Some c ->
            c.Memory.Set(dst, src)
            src
        | [src], _ -> src
        | _ ->
            match ctx with
            | Some c -> Context.warning c "MOV received invalid arguments"
            | None -> ()
            box null

    let ton (args: obj list) (ctx: ExecutionContext option) =
        match ctx with
        | None -> box false
        | Some c ->
            match args with
            | [ enable; (:? string as name); preset ] ->
                // Always require explicit enable signal for proper rung control
                let en = TypeConverter.toBool enable
                let p =
                    if en then
                        TypeConverter.toInt preset
                    else
                        // enable=false: preserve preset but don't parse if not needed
                        match preset with
                        | :? int as value -> value
                        | _ -> TypeConverter.toInt preset
                Context.updateTimerOn c name en p |> box
            | _ ->
                Context.warning c "TON requires [enable, name, preset] - 2-arg form is deprecated"
                box false

    let tof (args: obj list) (ctx: ExecutionContext option) =
        match ctx with
        | None -> box false
        | Some c ->
            match args with
            | [ (:? string as name); preset ] ->
                let p = TypeConverter.toInt preset
                Context.updateTimerOff c name true p |> box
            | [ enable; (:? string as name); preset ] ->
                let en = TypeConverter.toBool enable
                let p  = TypeConverter.toInt  preset
                Context.updateTimerOff c name en p |> box
            | _ ->
                Context.warning c "TOF received invalid arguments"
                box false

    let tp (args: obj list) (ctx: ExecutionContext option) =
        match ctx with
        | None -> box false
        | Some c ->
            match args with
            | [ (:? string as name); preset ] ->
                // 2-arg form: implicit trigger always true, use edge detection
                let edgeName = name + "_tp_last_trigger"
                let lastValue = c.Memory.Get(edgeName)
                let lastTrigger = if isNull lastValue then false else TypeConverter.toBool lastValue
                let currentTrigger = true
                // CRITICAL FIX (DEFECT-022-2): Must update edge flag AFTER reading it
                // Previous code wrote true before reading, making lastTrigger always true after first scan
                // Edge detection requires: read old → detect edge → write new

                let p = TypeConverter.toInt preset
                // MAJOR FIX: Use ITimeProvider instead of Timebase for testability (GAP-007)
                let now = c.TimeProvider.GetTimestamp()
                let timer = c.Timers.GetOrAdd(name, fun _ -> TimerState.create p now)

                lock timer.Lock (fun () ->
                    timer.Preset <- max 0 p
                    // Rising edge detection
                    let risingEdge = currentTrigger && not lastTrigger

                    if risingEdge && not timer.Timing then
                        // Start pulse on rising edge
                        timer.Timing <- true
                        timer.Done <- true
                        timer.Accumulated <- 0
                        timer.LastTimestamp <- now
                    elif timer.Timing then
                        // Pulse is running - check if time expired
                        let elapsed = Timebase.elapsedMilliseconds timer.LastTimestamp now
                        if elapsed > 0 then
                            timer.Accumulated <- min timer.Preset (timer.Accumulated + elapsed)
                            timer.LastTimestamp <- now

                        // Self-reset when pulse duration reached
                        if timer.Accumulated >= timer.Preset then
                            timer.Timing <- false
                            timer.Done <- false
                            timer.Accumulated <- timer.Preset

                    // CRITICAL FIX (DEFECT-022-2): Write edge flag AFTER processing
                    // Previous code wrote before reading, breaking edge detection
                    c.Memory.SetForced(edgeName, box currentTrigger)

                    timer.Done
                ) |> box
            | [ trigger; (:? string as name); preset ] ->
                // 3-arg form: explicit trigger with edge detection
                let tr = TypeConverter.toBool trigger
                let edgeName = name + "_tp_last_trigger"
                let lastValue = c.Memory.Get(edgeName)
                let lastTrigger = if isNull lastValue then false else TypeConverter.toBool lastValue
                // CRITICAL FIX (DEFECT-022-3): Use SetForced for undeclared edge flags
                // Previous code used Set(), raising VariableNotDeclared on first invocation
                // SetForced auto-declares Internal variables (matching 2-arg form)

                let p = TypeConverter.toInt preset
                // MAJOR FIX: Use ITimeProvider instead of Timebase for testability (GAP-007)
                let now = c.TimeProvider.GetTimestamp()
                let timer = c.Timers.GetOrAdd(name, fun _ -> TimerState.create p now)

                lock timer.Lock (fun () ->
                    timer.Preset <- max 0 p
                    // Rising edge detection
                    let risingEdge = tr && not lastTrigger

                    if risingEdge && not timer.Timing then
                        // Start pulse on rising edge
                        timer.Timing <- true
                        timer.Done <- true
                        timer.Accumulated <- 0
                        timer.LastTimestamp <- now
                    elif timer.Timing then
                        // Pulse is running - check if time expired
                        let elapsed = Timebase.elapsedMilliseconds timer.LastTimestamp now
                        if elapsed > 0 then
                            timer.Accumulated <- min timer.Preset (timer.Accumulated + elapsed)
                            timer.LastTimestamp <- now

                        // Self-reset when pulse duration reached
                        if timer.Accumulated >= timer.Preset then
                            timer.Timing <- false
                            timer.Done <- false
                            timer.Accumulated <- timer.Preset

                    // CRITICAL FIX (DEFECT-022-3): Use SetForced and write AFTER processing
                    // Previous code used Set() (crashed) and wrote before processing (broke edge detection)
                    c.Memory.SetForced(edgeName, box tr)

                    timer.Done
                ) |> box
            | _ ->
                Context.warning c "TP received invalid arguments"
                box false

    let ctu (args: obj list) (ctx: ExecutionContext option) =
        match ctx with
        | None -> box 0
        | Some c ->
            match args with
            | [ :? string as n; preset ] ->
                // 2-arg form: each call is a count event (pulse the enable signal)
                // Call with true to trigger count, then immediately set last state to false
                // so next call will be a rising edge
                let p = TypeConverter.toInt preset
                let result = Context.updateCounterUp c n true false p
                // Reset the counter's LastCountInput to false so next call triggers another count
                match c.Counters.TryGetValue(n) with
                | true, counter -> lock counter.Lock (fun () -> counter.LastCountInput <- false)
                | false, _ -> ()
                result |> box
            | [ :? string as n; enable; preset ] ->
                // CRITICAL FIX (DEFECT-021-7): This is the 3-arg legacy form - reset still hardcoded
                // IEC 61131-3 requires 4-arg form: CTU(name, countUp, reset, preset)
                let en = TypeConverter.toBool enable
                let p = TypeConverter.toInt preset
                Context.updateCounterUp c n en false p |> box
            | [ :? string as n; countUp; reset; preset ] ->
                // CRITICAL FIX (DEFECT-021-7): Full IEC 61131-3 CTU signature
                // CTU(CU, R, PV) → CTU(name, countUp, reset, preset)
                // Previous code hardcoded reset to false, breaking state machines
                let cu = TypeConverter.toBool countUp
                let r = TypeConverter.toBool reset
                let p = TypeConverter.toInt preset
                Context.updateCounterUp c n cu r p |> box
            | _ ->
                Context.warning c "CTU requires [name; preset] or [name; countUp; reset; preset]"
                box 0

    let ctd (args: obj list) (ctx: ExecutionContext option) =
        match ctx with
        | None -> box 0
        | Some c ->
            match args with
            | [ :? string as n; preset ] ->
                // 2-arg form: each call is a decrement event (pulse the down signal)
                // Call with down=true to trigger decrement, then reset last state to false
                let p = TypeConverter.toInt preset
                let result = Context.updateCounterDown c n true false p
                // Reset the counter's LastCountInput to false so next call triggers another decrement
                match c.Counters.TryGetValue(n) with
                | true, counter -> lock counter.Lock (fun () -> counter.LastCountInput <- false)
                | false, _ -> ()
                result |> box
            | [ :? string as n; down; load; preset ] ->
                let downEdge = TypeConverter.toBool down
                let loadSignal = TypeConverter.toBool load
                let p = TypeConverter.toInt preset
                Context.updateCounterDown c n downEdge loadSignal p |> box
            | _ ->
                Context.warning c "CTD received invalid arguments"
                box 0

    /// CTUD (Count Up/Down) - 양방향 카운터
    /// 인자: [name; countUp; countDown; reset; preset]
    let ctud (args: obj list) (ctx: ExecutionContext option) =
        match ctx with
        | None -> box 0
        | Some c ->
            match args with
            | [ :? string as name; countUp; countDown; reset; preset ] ->
                let cu = TypeConverter.toBool countUp
                let cd = TypeConverter.toBool countDown
                let r = TypeConverter.toBool reset
                let p = TypeConverter.toInt preset

                // Use unique naming for edge tracking: {name}_ctud_up and {name}_ctud_down
                let upEdgeName = name + "_ctud_up"
                let downEdgeName = name + "_ctud_down"

                // Get or create counter state directly
                let counter = c.Counters.GetOrAdd(name, fun _ ->
                    { Preset = p
                      Count = 0
                      Done = false
                      Up = true  // CTUD can go both ways
                      LastCountInput = false
                      Lock = obj () })

                lock counter.Lock (fun () ->
                    // Get last edge states from memory
                    let lastUpValue = c.Memory.Get(upEdgeName)
                    let lastDownValue = c.Memory.Get(downEdgeName)
                    let lastUp = if isNull lastUpValue then false else TypeConverter.toBool lastUpValue
                    let lastDown = if isNull lastDownValue then false else TypeConverter.toBool lastDownValue

                    counter.Preset <- max 0 p

                    if r then
                        // Reset takes priority
                        counter.Count <- 0
                        counter.Done <- false
                    else
                        // Process count up edge (rising edge)
                        if cu && not lastUp then
                            counter.Count <- counter.Count + 1

                        // Process count down edge (rising edge)
                        if cd && not lastDown then
                            counter.Count <- max 0 (counter.Count - 1)

                        // Done bit: TRUE when count >= preset (IEC 61131-3 standard)
                        counter.Done <- (counter.Count >= counter.Preset)

                    // CRITICAL FIX (DEFECT-020-7): Use SetForced for undeclared edge flags
                    // Previous code used Set(), raising VariableNotDeclared on first scan
                    // SetForced auto-declares Internal variables and bypasses writability check
                    c.Memory.SetForced(upEdgeName, box cu)
                    c.Memory.SetForced(downEdgeName, box cd)

                    box counter.Count)
            | _ ->
                Context.warning c "CTUD received invalid arguments"
                box 0
