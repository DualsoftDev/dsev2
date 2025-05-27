module TestProject.UnitTest.AasxIO

open NUnit.Framework
open System.IO
open Dual.EV2.Core
open Dual.EV2.AasxIO

[<TestFixture>]
type ``AASX Export Tests`` () =

    let exportDir = "test_aasx"

    [<SetUp>]
    member _.Setup() =
        if Directory.Exists(exportDir) then
            Directory.Delete(exportDir, true)

    [<TearDown>]
    member _.Cleanup() =
        if Directory.Exists(exportDir) then
            Directory.Delete(exportDir, true)

    [<Test>]
    member _.``단일 시스템 AASX 파일이 정상적으로 생성됨`` () =
        let project = Project("projTest")
        let system = System("sysA", project)
        project.Systems.Add(system)

        let exportPath = Path.Combine(exportDir, "sysA.aasx")
        AASX.exportSystemAASX system exportPath

        Assert.IsTrue(File.Exists(exportPath))
        let content = File.ReadAllText(exportPath)
        Assert.IsTrue(content.Contains(system.Name))
        Assert.IsTrue(content.Contains(system.Guid.ToString()))

    [<Test>]
    member _.``여러 시스템이 있는 프로젝트 AASX 디렉터리로 내보내짐`` () =
        let project = Project("projMulti")
        let system1 = System("sys1", project)
        let system2 = System("sys2", project)
        project.Systems.Add(system1)
        project.Systems.Add(system2)

        AASX.exportAllAASX project exportDir

        let file1 = Path.Combine(exportDir, "sys1.aasx")
        let file2 = Path.Combine(exportDir, "sys2.aasx")

        Assert.IsTrue(File.Exists(file1))
        Assert.IsTrue(File.Exists(file2))

        let content1 = File.ReadAllText(file1)
        let content2 = File.ReadAllText(file2)

        Assert.IsTrue(content1.Contains(system1.Name))
        Assert.IsTrue(content2.Contains(system2.Name))
