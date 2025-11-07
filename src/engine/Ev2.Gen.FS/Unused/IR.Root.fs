namespace Ev2.Gen.IR.Unused

open System

[<AutoOpen>]
module IRRoot =

    /// Top-level IR (Intermediate Representation) for PLC code generation
    type CodeGenIR = {
        IrVersion: string
        Project: Project
        Devices: Device list
        IO: IOConfig option
        DataTypes: DataTypes option
        Libraries: Library list
        Variables: Variables option
        Tasks: Task list
        Resources: Resource list
        POUs: POU list
        Motion: Motion option
        Safety: Safety option
        Communication: Communication option
        HMI: HMI option
        Graphics: Graphics option
        Constraints: Constraints option
        Localization: Localization option
        Units: Units option
        VendorExtensions: VendorExtensions option
    }

    /// Helper functions for creating empty IR
    module CodeGenIR =

        /// Create a minimal IR with required fields
        let create projectName version =
            {
                IrVersion = "1.0.0"
                Project = {
                    Name = projectName
                    Description = None
                    Version = version
                    CreatedAt = DateTime.UtcNow
                    ModifiedAt = DateTime.UtcNow
                    TimeBase = "ms"
                    TargetProfiles = []
                }
                Devices = []
                IO = None
                DataTypes = None
                Libraries = []
                Variables = None
                Tasks = []
                Resources = []
                POUs = []
                Motion = None
                Safety = None
                Communication = None
                HMI = None
                Graphics = None
                Constraints = None
                Localization = None
                Units = None
                VendorExtensions = None
            }

        /// Create an empty DataTypes
        let emptyDataTypes () =
            {
                UDT = []
                Enums = []
                Aliases = []
            }

        /// Create an empty Variables
        let emptyVariables () =
            {
                Global = []
                Constants = []
                Retain = []
            }

        /// Create an empty IOConfig
        let emptyIOConfig () =
            {
                Channels = []
                Mappings = []
            }

        /// Create an empty PouInterface
        let emptyPouInterface () =
            {
                InputVars = []
                OutputVars = []
                InOutVars = []
                LocalVars = []
            }
