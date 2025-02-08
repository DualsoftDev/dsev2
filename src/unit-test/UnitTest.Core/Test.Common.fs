[<AutoOpen>]
module TestCommon

open System
open System.Text.RegularExpressions
open Newtonsoft.Json.Linq


type String with
    member x.RemoveLineContaining(pattern:string) = Regex.Replace(x, @"^.*" + pattern + @".*$\r?\n?", "", RegexOptions.Multiline);
    member x.RemoveGuid() =
        let jsonText = x
        let rec removeGuid (token: JToken) : JToken =
            match token with
            | :? JObject as obj ->
                let newObj = JObject()
                for prop in obj.Properties() do
                    if prop.Name <> "Guid" then
                        newObj.Add(prop.Name, removeGuid prop.Value)
                newObj :> JToken
            | :? JArray as arr ->
                let newArr = JArray()
                for item in arr do
                    newArr.Add(removeGuid item)
                newArr :> JToken
            | _ -> token

        let parsedJson = JObject.Parse(jsonText)
        removeGuid parsedJson |> fun j -> j.ToString()


    member x.ZeroFillGuid() =
        let jsonText = x
        let isGuid (value: string) =
            let guidPattern = @"^[{(]?[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}[)}]?$"
            Regex.IsMatch(value, guidPattern)

        let rec replaceGuid (token: JToken) : JToken =
            match token with
            | :? JObject as obj ->
                let newObj = JObject()
                for prop in obj.Properties() do
                    if prop.Value.Type = JTokenType.String && isGuid (prop.Value.ToString()) then
                        newObj.Add(prop.Name, JValue("00000000-0000-0000-0000-000000000000"))
                    else
                        newObj.Add(prop.Name, replaceGuid prop.Value)
                newObj :> JToken
            | :? JArray as arr ->
                let newArr = JArray()
                for item in arr do
                    newArr.Add(replaceGuid item)
                newArr :> JToken
            | _ -> token

        let parsedJson = JObject.Parse(jsonText)
        replaceGuid parsedJson |> fun j -> j.ToString()

