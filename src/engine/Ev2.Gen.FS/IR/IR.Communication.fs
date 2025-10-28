namespace Ev2.Gen

open System

[<AutoOpen>]
module IRCommunication =

    /// Communication protocol type
    type CommProtocol =
        | OpcUa
        | ModbusTcp
        | ProfiNet
        | EtherNetIp
        | Custom of string

    /// OPC UA node exposure
    type OpcUaNode = {
        NodeId: string
        SourceVar: string  // Reference to variable
    }

    /// Modbus register mapping
    type ModbusMapping = {
        HoldingReg: int option
        InputReg: int option
        Coil: int option
        DiscreteInput: int option
        Var: string  // Reference to variable
    }

    /// Communication server
    type CommServer = {
        ServerType: CommProtocol
        Endpoint: string option
        NamespaceUri: string option  // For OPC UA
        Expose: OpcUaNode list  // For OPC UA
    }

    /// Communication client
    type CommClient = {
        ClientType: CommProtocol
        Server: string  // Server address
        Mappings: ModbusMapping list  // For Modbus
    }

    /// Communication configuration
    type Communication = {
        Servers: CommServer list
        Clients: CommClient list
    }
