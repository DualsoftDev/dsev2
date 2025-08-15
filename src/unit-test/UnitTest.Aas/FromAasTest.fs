namespace T.Core

open Dual.Ev2
open Dual.Ev2.Aas
open NUnit.Framework
open Dual.Common.UnitTest.FS
open Dual.Ev2.Aas.AasXModule2
open Dual.Ev2.Aas
open T.TestHelpers  // 테스트 헬퍼 함수 추가
open T  // createHelloDS 사용을 위해
open Ev2.Core.FS

module FromAasTest =
    let dsJson = DsJson.dsJson
    let aasJson0 = Aas.aasJson0
    let aasXml0 = Aas.aasXml0
    ()
    /// Json Test
    type T() =     // ``AasShell: Json -> JObj -> {Xml, Json} conversion test``, ``Aasx xml submodel xml fetch test``
        [<Test>]
        member _.``AasShell: Json -> JObj -> {Xml, Json} conversion test`` () =
            let env = J.CreateIClassFromXml<Aas.Environment>(aasXml0)
            let xml = env.ToXml()
            xml =~= aasXml0

            env.ToJson() =~= aasJson0


            //let dsSystem = DsSystem.FromAasJsonENV(aasJson0)
            ()

        [<Test>]
        member _.``Aasx xml submodel xml fetch test`` () =
            // 1. 테스트용 프로젝트 생성
            let project = createHelloDS()

            // 2. AASX 파일로 내보내기
            let aasxPath = getUniqueAasxPath()
            project.ExportToAasxFile(aasxPath)

            try
                // 3. AASX 파일에서 XML 읽기
                let submodelXml = getAasXmlFromAasxFile aasxPath

                // 4. XML이 비어있지 않은지 확인
                submodelXml.Length > 0 === true

                // 5. AAS XML 네임스페이스 포함 확인
                submodelXml.Contains("admin-shell.io") === true
            finally
                // 6. 테스트 파일 정리
                cleanupTestFile aasxPath
