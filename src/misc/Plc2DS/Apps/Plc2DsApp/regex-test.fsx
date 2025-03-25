open System.Text.RegularExpressions

let reg = Regex("[_\\s]\\[D[IOio]\\d+\\]")
let m = reg.Match("RBT1_[DO004]_그리퍼_차종유니트_LOCK_지령")
printfn "%A" m.Success