namespace T

open System

open NUnit.Framework
open Newtonsoft.Json
open Dual.Common.Base
open Dual.Common.Core.FS
open Dual.Common.UnitTest.FS



[<AutoOpen>]
module DataTypeSerializeTestModule =
    do
        DcLogger.EnableTrace <- true

    type TOptGuid(?guid:Guid) =
        member val Guid = guid with get, set

    let guid = "de41672c-7da5-451a-b086-0f2a4ec81438"
    [<Test>]
    let ``testOptGuid()``() =
        let t = TOptGuid()
        let json = EmJson.ToJson(t)
        let jsonAnswer = "{}"
        json === jsonAnswer

        let t = TOptGuid(guid = Guid.Parse(guid))
        let json = EmJson.ToJson(t)
        (*
{
  "Guid": {
    "Case": "Some",
    "Fields": "de41672c-7da5-451a-b086-0f2a4ec81438"
  }
}        *)
        let jsonAnswer =
            {| Guid =
                {| Case = "Some"
                   Fields = [ guid ] |}
            |} |> EmJson.ToJson

        tracefn $"Json:\r\n{json}"
        json === jsonAnswer


    type TNullGuid(guid:Nullable<Guid>) =
        member val Guid = guid with get, set

    [<Test>]
    let ``testNullGuid()``() =
        let t = TNullGuid(Nullable())
        let json = EmJson.ToJson(t)
        let jsonAnswer = "{}"

        json === jsonAnswer
        let t = TNullGuid(Guid.Parse(guid))
        let json = EmJson.ToJson(t)
        tracefn $"Json:\r\n{json}"
        let jsonAnswer = """{
  "Guid": "de41672c-7da5-451a-b086-0f2a4ec81438"
}"""
        json === jsonAnswer
