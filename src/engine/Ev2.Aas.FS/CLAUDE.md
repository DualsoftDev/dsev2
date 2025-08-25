# EV2.AAS.FS
- Ev2.Core.FS project 의 AAS package explorer format 파일에 대한 serialize / deserialize 역할을 담당
- AasCore.Aas3_0 nuget package 를 이용
- Runtime type <-> NJXXX type <-> AASX
- 이 project 의 기능 테스트는 ../unit-test/UnitTest.Aas/UnitTest.Aas.fsproj 에서 수행
- NjProject 가 aasx file 의 xml 의 하나의 submodule 로 구성되어 serialize / deserialize 됨

