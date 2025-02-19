namespace Dual.Ev2

//[<AutoOpen>]
//module InterfacesModule =

//    type INamedEv2 = interface end

//    type INamedEv2 with
//        member x.Name
//            with get() =
//                let objType = x.GetType()
//                let prop = objType.GetProperty("Name")
//                if prop = null then
//                    failwith "ERROR: No Name Property"

//                prop.GetValue(obj) |> string
//            and set v =
//                let objType = x.GetType()
//                let prop = objType.GetProperty("Name")
//                if prop = null then
//                    failwith "ERROR: No Name Property"
//                prop.SetValue(obj, v)
