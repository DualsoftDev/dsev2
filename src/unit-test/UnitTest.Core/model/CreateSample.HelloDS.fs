namespace T

open Dual.Common.Base
open Dual.Common.Core.FS

open Ev2.Core.FS

[<AutoOpen>]
module CreateSampleWithHelloDsModule =
    let createHelloDS(): Project =
        let hdsProject:Project = Project.Create(Name = "HelloDS")
        let hdsSystem: DsSystem =
            DsSystem.Create()
            |> tee (fun z ->
                z.Name <- "HelloDS_System"
                z.IRI <- "http://example.com/ev2/system/main")

        hdsProject.AddActiveSystem hdsSystem

        let hdsFlow =
            Flow.Create(Name = "STN1")

        // UI 요소들 생성
        let buttons = [
            DsButton.Create(Name="AutoSelect")
            DsButton.Create(Name="ManualSelect")
            DsButton.Create(Name="DrivePushBtn")
            DsButton.Create(Name="EmergencyBtn")
        ]
        let lamps = [ Lamp.Create(Name="MyLamp1") ]
        let conditions = [ DsCondition.Create(Name="MyCondition1") ]
        let actions = [ DsAction.Create(Name="MyAction1") ]

        // UI 요소들의 Flow 설정
        buttons |> iter (fun b -> b.FlowGuid <- Some hdsFlow.Guid)
        lamps |> iter (fun l -> l.FlowGuid <- Some hdsFlow.Guid)
        conditions |> iter (fun c -> c.FlowGuid <- Some hdsFlow.Guid)
        actions |> iter (fun a -> a.FlowGuid <- Some hdsFlow.Guid)

        // System에 UI 요소들 추가
        buttons |> hdsSystem.AddButtons
        lamps |> hdsSystem.AddLamps
        conditions |> hdsSystem.AddConditions
        actions |> hdsSystem.AddActions

        let createWork name =
            Work.Create()
            |> tee (fun z ->
                z.Name      <- name
                z.Status4   <- Some DbStatus4.Ready)

        let hdsWork1 = createWork "Work1"
        let hdsWork2 = createWork "Work2"
        let hdsWork3 = createWork "Work3"

        [hdsWork1; hdsWork2; hdsWork3] |> hdsFlow.AddWorks
        [hdsWork1; hdsWork2; hdsWork3] |> hdsSystem.AddWorks
        [hdsFlow] |> hdsSystem.AddFlows

        let dev1 = createCylinder "Device1"
        let dev2 = createCylinder "Device2"
        let dev3 = createCylinder "Device3"
        let dev4 = createCylinder "Device4"
        [ dev1; dev2; dev3; dev4 ] |> iter hdsProject.AddPassiveSystem

        let createApiCalls() =
            let apiCallDev1Adv =
                ApiCall.Create()
                |> tee (fun z ->
                    z.ApiDefGuid <- dev1.ApiDefs.Find(fun x -> x.Name = "ApiDefADV").Guid
                    z.Name      <- "apiCallDev1ADV"
                    z.InAddress <- "P00000"
                    z.OutAddress<- "P00040")

            let apiCallDev1Ret =
                ApiCall.Create()
                |> tee (fun z ->
                    z.ApiDefGuid <- dev1.ApiDefs.Find(fun x -> x.Name = "ApiDefRET").Guid
                    z.Name      <- "apiCallDev1RET"
                    z.InAddress <- "P00001"
                    z.OutAddress<- "P00041")

            //
            let apiCallDev2Adv =
                ApiCall.Create()
                |> tee (fun z ->
                    z.ApiDefGuid <- dev2.ApiDefs.Find(fun x -> x.Name = "ApiDefADV").Guid
                    z.Name      <- "apiCallDev2ADV"
                    z.InAddress <- "P00000"
                    z.OutAddress<- "P00040")

            let apiCallDev2Ret =
                ApiCall.Create()
                |> tee (fun z ->
                    z.ApiDefGuid <- dev2.ApiDefs.Find(fun x -> x.Name = "ApiDefRET").Guid
                    z.Name      <- "apiCallDev2RET"
                    z.InAddress <- "P00001"
                    z.OutAddress<- "P00041")


            //
            let apiCallDev3Adv =
                ApiCall.Create()
                |> tee (fun z ->
                    z.ApiDefGuid <- dev3.ApiDefs.Find(fun x -> x.Name = "ApiDefADV").Guid
                    z.Name      <- "apiCallDev3ADV"
                    z.InAddress <- "P00000"
                    z.OutAddress<- "P00040")

            let apiCallDev3Ret =
                ApiCall.Create()
                |> tee (fun z ->
                    z.ApiDefGuid <- dev3.ApiDefs.Find(fun x -> x.Name = "ApiDefRET").Guid
                    z.Name      <- "apiCallDev3RET"
                    z.InAddress <- "P00001"
                    z.OutAddress<- "P00041")


            //
            let apiCallDev4Adv =
                ApiCall.Create()
                |> tee (fun z ->
                    z.ApiDefGuid <- dev4.ApiDefs.Find(fun x -> x.Name = "ApiDefADV").Guid
                    z.Name      <- "apiCallDev4ADV"
                    z.InAddress <- "P00000"
                    z.OutAddress<- "P00040")

            let apiCallDev4Ret =
                ApiCall.Create()
                |> tee (fun z ->
                    z.ApiDefGuid <- dev4.ApiDefs.Find(fun x -> x.Name = "ApiDefRET").Guid
                    z.Name      <- "apiCallDev4RET"
                    z.InAddress <- "P00001"
                    z.OutAddress<- "P00041")

            [
                apiCallDev1Adv; apiCallDev1Ret
                apiCallDev2Adv; apiCallDev2Ret
                apiCallDev3Adv; apiCallDev3Ret
                apiCallDev4Adv; apiCallDev4Ret
            ] |> hdsSystem.AddApiCalls

        let createCalls() =
            let findApiCall name = hdsSystem.ApiCalls.Find(fun x -> x.Name = name)
            let createCall dev name =
                Call.Create()
                |> tee(fun z ->
                    z.Name     <- $"Device{dev}_{name}"
                    z.CallType <- DbCallType.Parallel
                    z.Timeout  <- Some 30
                    z.ApiCallGuids.AddRange [findApiCall $"apiCallDev{dev}{name}" |> _.Guid] )
            let call1Adv = createCall 1 "ADV"
            let call1Ret = createCall 1 "RET"
            let call2Adv = createCall 2 "ADV"
            let call2Ret = createCall 2 "RET"
            let call3Adv = createCall 3 "ADV"
            let call3Ret = createCall 3 "RET"
            let call4Adv = createCall 4 "ADV"
            let call4Ret = createCall 4 "RET"

            [ call1Adv; call1Ret
              call2Adv; call2Ret
              call3Adv; call3Ret
              call4Adv; call4Ret
            ] |> hdsWork1.AddCalls

        let createArrowsInWork1() =
            let findCall name = hdsWork1.Calls.Find(fun x -> x.Name = name)
            [
                ArrowBetweenCalls.Create(findCall $"Device1_ADV", findCall $"Device2_ADV", DbArrowType.Start)
                ArrowBetweenCalls.Create(findCall $"Device2_ADV", findCall $"Device3_ADV", DbArrowType.Start)
                ArrowBetweenCalls.Create(findCall $"Device3_ADV", findCall $"Device4_ADV", DbArrowType.Start)

                ArrowBetweenCalls.Create(findCall $"Device4_ADV", findCall $"Device1_RET", DbArrowType.Start)
                ArrowBetweenCalls.Create(findCall $"Device4_ADV", findCall $"Device2_RET", DbArrowType.Start)
                ArrowBetweenCalls.Create(findCall $"Device4_ADV", findCall $"Device3_RET", DbArrowType.Start)

                ArrowBetweenCalls.Create(findCall $"Device3_RET", findCall $"Device4_RET", DbArrowType.Start)
            ] |> hdsWork1.AddArrows

        let createArrowsInSystem() =
            [
                ArrowBetweenWorks.Create(hdsWork1, hdsWork2, DbArrowType.StartReset)
                ArrowBetweenWorks.Create(hdsWork2, hdsWork3, DbArrowType.StartReset)
            ] |> hdsSystem.AddArrows

        createApiCalls()
        createCalls()
        createArrowsInWork1()
        hdsProject
