namespace rec Dual.Ev2.Aas

(* Core 를 AAS Json/Xml 로 변환하기 위한 실제 코드 *)

[<AutoOpen>]
module internal PreludeModule =

    let private q = "'"
    let private qq = "\""

    let surround prefix suffix s = sprintf "%s%s%s" prefix s suffix
    let escapeQuote(s:string) = s.Replace("'", @"\'")
    //let quote q = surround q q
    let singleQuote = surround q q
    let doubleQuote = surround qq qq
