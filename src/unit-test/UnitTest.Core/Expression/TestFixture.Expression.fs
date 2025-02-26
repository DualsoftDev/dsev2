namespace T

open System.IO
open Dual.Common.UnitTest.FS
open Dual.Common.Core.FS
open Dual.Ev2
open System.Reactive.PlatformServices


[<AutoOpen>]
module ExpressionFixtures =
    //let tryParseStatement4UnitTest (targetType:PlatformTarget) (storages: Storages) (text: string) : Statement option =
    //    try
    //        let parser = ParserUtilityModule.createExpressionParser (text)
    //        let ctx = parser.statement ()
    //        let parserData = new ParserData(targetType, storages, Some parser)

    //        tryCreateStatement parserData ctx
    //    with exn ->
    //        failwith $"Failed to parse Statement: {text}\r\n{exn}"


    let sys:ISystem = getNull<ISystem>()    // DsSystem.Create4Test("testSys")
    let mutable runtimeTarget = WINDOWS
    let setRuntimeTarget(target:PlatformTarget) =
            let runtimeTargetBackup = target
            RuntimeDS.System <- Some sys
            runtimeTarget <- target
            //ParserUtil.runtimeTarget <-target
            disposable { runtimeTarget <- runtimeTargetBackup }

    //let parseExpression4UnitTest (storages: Storages) (text: string) : IExpression =
    //    try
    //        let parser = createExpressionParser (text)
    //        let ctx = parser.expr ()
    //        let parserData = ParserData((WINDOWS), Storages(), None)

    //        createExpression parserData (defaultStorageFinder storages) ctx
    //    with exn ->
    //        failwith $"Failed to parse Expression: {text}\r\n{exn}" // Just warning.  하나의 이름에 '.' 을 포함하는 경우.  e.g "#seg.testMe!!!"


    [<AbstractClass>]
    type ExpressionTestBaseClass() =
        inherit TestClassWithLogger(Path.Combine($"{__SOURCE_DIRECTORY__}/App.config"), "UnitTestLogger")
        do
            //Engine.CodeGenCPU.ModuleInitializer.Initialize()
            setRuntimeTarget (WINDOWS)|> ignore




    let toTimer (timerStatement:Statement) :Timer =
        match timerStatement with
        | DuTimer t -> t.Timer
        | _ -> failwithlog "not a timer statement"

    let toCounter (counterStatement:Statement) :Counter =
        match counterStatement with
        | DuCounter t -> t.Counter
        | _ -> failwithlog "not a counter statement"
