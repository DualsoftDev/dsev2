/// Helper module to create RawLogic instances for testing
module CreateRawLogicForTests

open Ev2.PLC.Mapper.Core.Types

/// Create a RawLogic instance for testing
let createRawLogic id name number content logicType flowType = {
    Id = Some id
    Name = Some name
    Number = number
    Content = content
    RawContent = Some content
    LogicType = logicType
    Type = Some flowType
    Variables = []
    Comments = []
    LineNumber = Some number
    Properties = Map.empty
    Comment = None
}

/// Create a simple RawLogic for testing
let createSimpleRawLogic id name content logicType =
    createRawLogic id name 1 content logicType LogicFlowType.Simple