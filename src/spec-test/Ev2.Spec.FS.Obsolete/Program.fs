open System
open System.IO
open Dual.EV2.Core
open Dual.EV2.JsonIO
open Dual.EV2.AasxIO
open Dual.EV2.RuntimeDB.RuntimeDB
open System.Collections.Generic

// Manual construction of sys-003 (same as before)
let createSystem003 (project: Project) (systemDic: Dictionary<string, System>) =
    let system = System("sys-003", project)

    let flow = Flow("flow-001", system)
    system.Flows.Add(flow)

    let workA = Work("WorkA", system, flow)
    let workB = Work("WorkB", system, flow)
    system.Works.Add(workA)
    system.Works.Add(workB)
    system.WorkArrows.Add((workA, workB))

    let callA = Call("CallA", workA)
    let callB = Call("CallB", workA)
    workA.Calls.Add(callA)
    workA.Calls.Add(callB)
    workA.CallGraph.Add((callA, callB))

    let sysD = systemDic.["sysD"]
    let def1 = sysD.ApiDefs.[0] 
    let def2 = sysD.ApiDefs.[1] 

    callA.ApiCalls.Add(ApiCall("ApiCallA", callA, def1))
    callB.ApiCalls.Add(ApiCall("ApiCallB", callB, def2))

    system


      
// Table-based data
let projectDefs = [ ("proj-001", "projA"); ("proj-002", "projB") ]
let systemDefs =
    [ ("sys-001", "sysA"); ("sys-002", "sysB"); ("sys-003", "sysC")
      ("sys-004", "sysD"); ("sys-005", "sysE"); ("sys-006", "sysF") ]
let projectSystemMap =
    [ ("projA", "sys-001", true)
      ("projA", "sys-002", false)
      ("projB", "sys-002", false)
      ("projB", "sys-003", true)
      ("projB", "sys-004", false)
      ("projB", "sys-005", false)
      ("projB", "sys-006", false) ]

// Helper: Assign cloned system to project
let assignSystemToProject (project: Project) (sysId: string) (originalSystem: System) (isActive: bool) =
    let system = originalSystem

    project.Systems.Add(system)

    let usage = ProjectSystemUsage(project, system, isActive)
    project.SystemUsages.Add(usage)

    if isActive then
        project.TargetSystemIds.Add(sysId)


// Main builder
let createProjectsFromMapping () : Project[] =
    let templatePath = Path.Combine(__SOURCE_DIRECTORY__, "doubleCylinderTemplate.json")

    // Step 1: Create projects
    let projectMap =
        projectDefs
        |> List.map (fun (_, name) -> name, Project(name))
        |> Map.ofList

    // Step 2: Map of system names
    let systemMap =
        systemDefs
        |> List.map (fun (id, name) -> id, name)
        |> Map.ofList

    // Step 3 + 4: For each project, assign systems (false first, then true), using shared system cache
    let systemCache = System.Collections.Generic.Dictionary<string, System>()
    let dummySysMap = new Dictionary<string, System>()

    for projName, group in projectSystemMap |> List.groupBy (fun (p, _, _) -> p) do
        let project = projectMap.[projName]

        let inactive, active =
            group
            |> List.partition (fun (_, _, isActive) -> not isActive)

        let orderedSystems = inactive @ active

        for (projectId, sysId, isActive) in orderedSystems do
            // Reuse or create original system
            let sysName = systemMap[sysId]
            let originalSystem =
                if systemCache.ContainsKey(sysId) then
                    systemCache.[sysId]
                else
                    let prj = projectMap[projectId]
                      
                    let sys =
                        if sysId = "sys-003" then
                            createSystem003 prj dummySysMap 
                        else
                            JsonIO.loadSystemFromJson templatePath sysName prj dummySysMap

                    systemCache.[sysId] <- sys
                    sys

            assignSystemToProject project sysId originalSystem isActive

    // Return all constructed projects
    projectMap |> Map.toArray |> Array.map snd


[<EntryPoint>]
let main _ =
    let projects = createProjectsFromMapping()

    let root = { Projects = projects }
    let myPrj = projects[1]
    JsonIO.saveToFile (myPrj.Name + ".json") root
    AASX.exportAllAASX myPrj ("./aasx_" + myPrj.Name)
        
    let dbPath = myPrj.Name + "_runtime.db"
    initializeSchema dbPath
    let db = fromProject myPrj
    saveToSqlite db dbPath

    printfn $"Project {myPrj.Name} exported."

    0
