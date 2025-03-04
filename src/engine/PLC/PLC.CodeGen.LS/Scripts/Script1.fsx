#I @"..\..\bin"
#I @"..\..\bin\Debug\netcoreapp3.1"
#I @"..\..\bin\netcoreapp3.1"
#I @"..\..\packages\FsUnit.xUnit.3.4.0\lib\net46"
#I @"..\..\Dual.Common.xUnit.FS\bin\Debug"
#I @"..\..\packages\NHamcrest.2.0.1\lib\net451"

#r "Dual.Core.FS.dll"
#r "Dual.Common.FS.dll"
#r "PLC.CodeGen.LS.dll"

// #load @"F:\solutions\GammaTest\soft\Gamma\Ds.Beta.UnitTest.FS\OnlyOnce.fs"
// #load loads script file
// #I __SOURCE_DIRECTORY__

open System
open System.IO
open PLC.CodeGen.LS
open Dual.Core.Prelude
open IEC61131

//let (===) x y = y |> should equal x


let alreadyAllocatedAddresses: Set<string> = Set.empty


let gen =
    XGITag.AddressGenerator "I" (0, 4096) (256, 512) (512, 1024) alreadyAllocatedAddresses

[ for i in [ 1..1000 ] do
      gen () ]

let gen2 =
    XGITag.AddressGenerator "IW" (0, 4096) (256, 512) (512, 1024) alreadyAllocatedAddresses

[ for i in [ 1..1000 ] do
      gen2 () ]


let manager =
    //PLCStorageManager.CreateFullBlown()
    //let lv2 = int StorageConstants.MaxIQLevel2  // 32
    //let lv1 = int StorageConstants.MaxIQLevel1  // 16
    //let mxMax = int StorageConstants.MaxMBits   // 262144

    let lv2 = 1
    let lv1 = 1
    let mxMax = 4

    let input =
        PLCStorage3(I, StorageRange(lv2, [ 0 .. lv2 - 1 ]), StorageRange(lv1, [ 0 .. lv1 - 1 ])) :> IPLCStorageSection

    let output =
        PLCStorage3(Q, StorageRange(lv2, [ 0 .. lv2 - 1 ]), StorageRange(lv1, [ 0 .. lv1 - 1 ])) :> IPLCStorageSection

    let memory = PLCStorage1(M, mxMax) :> IPLCStorageSection
    let storages = [ input; output; memory ]


    let manager = PLCStorageManager(storages)




    let descendants = storages |> Seq.collect (fun sp -> sp.Children)
    /// (type, size) 별로 sub manager 를 검색하기위한 사전
    let dic = descendants |> Seq.map (fun sp -> (sp.StorageType, sp.Size), sp) |> dict

    /// AddressPrefix (type+size 의 문자열. e.g "IX") 별로 sub manager 를 검색하기위한 사전
    let dicStr = descendants |> Seq.map (fun sp -> sp.AddressPrefix, sp) |> dict

    let ix = dic.[StorageType.I, Size.Bit]
    let flat = ix.GetAddressFromFlatIndex(100)
    let ix2 = manager.["IX"]
    ix2.GetAddressFromFlatIndex(100)
    ix2.Array

    ix2.GetComponentIndices(flat)

    manager

manager.["IX0.0.0"]
