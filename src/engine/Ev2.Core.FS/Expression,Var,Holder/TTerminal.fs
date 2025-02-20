namespace Dual.Ev2

open System
open Newtonsoft.Json
open Newtonsoft.Json.Linq
open Dual.Common.Base.FS
open Dual.Common.Core.FS

[<AutoOpen>]
module TTerminalModule =

    type ITerminal = interface end
    type INonTerminal = interface end

    type ITerminal<'T> =
        inherit ITerminal
        inherit IExpressionEv2<'T>

    type INonTerminal<'T> =
        inherit INonTerminal
        inherit IExpressionEv2<'T>

    type Arguments = IExpressionEv2 list


    // 기존 Terminal<'T> 에 해당.
    type TTerminal<'T>(value:'T) =
        inherit THolder<'T>(value)
        interface ITerminal<'T>
        new() = TTerminal(Unchecked.defaultof<'T>)   // for Json

    // 기존 FunctionSpec<'T> 에 해당.
    type TNonTerminal<'T>(value:'T) =
        inherit THolder<'T>(value)
        interface INonTerminal<'T>

        new() = TNonTerminal(Unchecked.defaultof<'T>)   // for Json

        member val Arguments: IExpressionEv2[] = [||] with get, set

        override holder.ToJObject(serializer: JsonSerializer): JObject =
            let obj = base.ToJObject(serializer)

            let argumentsArray =
                holder.Arguments
                |> Array.map (fun (arg:IExpressionEv2) ->
                    noop()
                    let jToken = JToken.FromObject(arg, serializer)

                    match jToken with
                    | :? JObject as jObj when not (jObj.ContainsKey("$type")) ->
                        // $type을 JSON의 맨 앞에 추가
                        let newJObj = JObject()
                        let typeName = EmJson.GetTypeString(arg)
                        newJObj["$type"] <- JValue(typeName)
                        jObj.Properties() |> Seq.iter (fun prop -> newJObj.Add(prop.Name, prop.Value))
                        newJObj :> JToken
                    | _ -> jToken
                )
                |> JArray

            obj["Arguments"] <- argumentsArray
            obj


        override holder.FromJObject(jobj: JObject, serializer: JsonSerializer) =
            base.FromJObject(jobj, serializer)

            match jobj.TryGetValue("Arguments") with
            | (true, (:? JArray as argumentsArray)) ->
                holder.Arguments <-
                    argumentsArray
                    |> Seq.choose (fun token ->
                        try
                            match token with
                            | :? JObject as jObj ->
                                match jObj.TryGetValue("$type") with
                                | (true, (:? JValue as typeName)) ->
                                    let typeNameStr = string typeName.Value
                                    tracefn "Attempting to resolve type: %s" typeNameStr

                                    // 직접 타입을 가져옴
                                    let typ = EmJson.GetType(typeNameStr)

                                    if typ <> null && typeof<IExpressionEv2>.IsAssignableFrom(typ) then
                                        tracefn "Resolved type: %s" typ.FullName
                                        // 해당 타입으로 역직렬화
                                        jObj.ToObject(typ, serializer) :?> IExpressionEv2 |> Some
                                    else
                                        failwith $"Invalid type: {typeNameStr}"
                                | _ ->
                                    failwith $"$type information is missing in the argument: {jObj}"
                            | _ ->
                                None
                        with
                        | ex ->
                            failwith $"Failed to deserialize argument: {ex.Message}"
                    ) |> Seq.toArray
            | _ ->
                ()



    type INonTerminal<'T> with
        /// INonTerminal.FunctionBody
        member x.FunctionBody
            with get() = getPropertyValueDynmaically(x, "FunctionBody") :?> (Arguments -> 'T)
            and set (v:Arguments -> 'T) = setPropertyValueDynmaically(x, "FunctionBody", v)



