namespace Ev2.Cpu.Runtime

open System

type TimerState = {
    mutable Preset: int
    mutable Accumulated: int
    mutable Done: bool
    mutable Timing: bool
    mutable LastTimestamp: int64
    Lock: obj
}

module TimerState =
    let create preset nowTicks =
        { Preset = max 0 preset
          Accumulated = 0
          Done = false
          Timing = false
          LastTimestamp = nowTicks
          Lock = obj () }

    let reset timer nowTicks =
        timer.Accumulated <- 0
        timer.Done <- false
        timer.Timing <- false
        timer.LastTimestamp <- nowTicks

type CounterState = {
    mutable Preset: int
    mutable Count: int
    mutable Done: bool
    mutable Up: bool
    mutable LastCountInput: bool
    Lock: obj
}

module CounterState =
    let create preset =
        { Preset = max 0 preset
          Count = 0
          Done = false
          Up = true
          LastCountInput = false
          Lock = obj () }

    let reset counter =
        counter.Count <- if counter.Up then 0 else counter.Preset
        counter.Done <- false
        counter.LastCountInput <- false
