    let rung (x, y) (expr: FlatExpression option) (cmdExp: CommandTypes option) : CoordinatedXmlElement =



    let drawCommandXgi (x, y) (cmd: CommandTypes) : BlockSummarizedXmlElements =
        match cmd with
        | PredicateCmd(pc) -> drawPredicate (x, y) pc
        | FunctionCmd(fc) -> drawFunction (x, y) fc
        | ActionCmd(ac) -> drawAction (x, y) ac
        | FunctionBlockCmd(fbc) ->
            match fbc with
            | TimerMode(timerStatement) -> drawFunctionBlockTimer (x, y) timerStatement
            | CounterMode(counterStatement) -> drawFunctionBlockCounter (x, y) counterStatement
        | _ -> failwithlog "Unknown CommandType"
