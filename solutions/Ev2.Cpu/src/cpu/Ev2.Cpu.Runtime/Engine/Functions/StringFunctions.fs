namespace Ev2.Cpu.Runtime

open System
open Ev2.Cpu.Core

// ─────────────────────────────────────────────────────────────────────
// String Functions
// ─────────────────────────────────────────────────────────────────────
// 문자열 함수: concat, length, substring, upper, lower, trim, replace
// ─────────────────────────────────────────────────────────────────────

module StringFunctions =

    let concat (args: obj list) =
        args |> List.map TypeConverter.toString |> String.concat "" |> box

    let length (v: obj) =
        (TypeConverter.toString v).Length |> box

    let substring (args: obj list) =
        match args with
        | [ :? string as s; :? int as i ] ->
            let startPos = max 0 (min i s.Length)
            box (s.Substring(startPos))
        | [ :? string as s; :? int as i; :? int as n ] ->
            let startPos = max 0 (min i s.Length)
            let maxLen = s.Length - startPos
            let actualLen = max 0 (min n maxLen)
            box (s.Substring(startPos, actualLen))
        | _ -> failwith "SUBSTR requires string and 1-2 integers"

    let upper (v: obj) = box ((cachedToString v).ToUpper())
    let lower (v: obj) = box ((cachedToString v).ToLower())
    let trim (v: obj) = box ((cachedToString v).Trim())

    let replace (args: obj list) =
        match args with
        | [ :? string as s; :? string as oldS; :? string as newS ] ->
            box (s.Replace(oldS, newS))
        | _ -> failwith "REPLACE requires 3 strings"

    let left (args: obj list) =
        match args with
        | [s; len] ->
            let str = TypeConverter.toString s
            let length = TypeConverter.toInt len
            let actualLen = min length str.Length
            box (str.Substring(0, max 0 actualLen))
        | _ -> failwith "LEFT requires string and int"

    let right (args: obj list) =
        match args with
        | [s; len] ->
            let str = TypeConverter.toString s
            let length = TypeConverter.toInt len
            // Clamp actualLen to [0, str.Length] to prevent negative length crash
            let actualLen = max 0 (min length str.Length)
            let startPos = str.Length - actualLen
            box (str.Substring(startPos, actualLen))
        | _ -> failwith "RIGHT requires string and int"

    let find (args: obj list) =
        match args with
        | [s; searchStr] ->
            let str = TypeConverter.toString s
            let search = TypeConverter.toString searchStr
            box (str.IndexOf(search))
        | _ -> failwith "FIND requires 2 strings"
