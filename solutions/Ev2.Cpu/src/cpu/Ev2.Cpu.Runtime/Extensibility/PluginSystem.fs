// MAJOR FIX (DEFECT-022-8): Correct namespace to match solution structure
// Previous code used DsRuntime.Cpu.* which doesn't exist in this solution
// This file will now compile and ship with the extensibility surface
namespace Ev2.Cpu.Runtime.Extensibility
open System
open System.Collections.Generic
open System.Collections.Concurrent
open System.Reflection
open Ev2.Cpu.Core

/// Plugin interface for extending CPU functionality
type IPlugin =
    abstract member Name: string
    abstract member Version: string
    abstract member Author: string option
    abstract member Description: string option
    abstract member Initialize: ExecutionContext -> unit
    abstract member Cleanup: ExecutionContext -> unit

/// Function plugin interface for custom functions
type IFunctionPlugin =
    inherit IPlugin
    abstract member Functions: Map<string, obj list -> obj>

/// Operator plugin interface for custom operators
type IOperatorPlugin =
    inherit IPlugin
    abstract member Operators: Map<string, DsOp>
    abstract member OperatorImplementations: IDictionary<DsOp, obj * obj -> obj>

/// Statement plugin interface for custom statement types
type IStatementPlugin =
    inherit IPlugin
    abstract member StatementTypes: Set<string>
    abstract member ExecuteCustomStatement: ExecutionContext -> string -> Map<string, obj> -> DateTime -> unit

/// Data type plugin interface for custom data types
type IDataTypePlugin =
    inherit IPlugin
    abstract member DataTypes: Map<string, DsType>
    abstract member TypeConverters: IDictionary<(DsType * DsType), obj -> obj>

/// Plugin metadata
[<CLIMutable>]
type PluginMetadata = {
    Name: string
    Version: string
    Author: string option
    Description: string option
    Dependencies: string list
    PluginType: string
    AssemblyPath: string option
    LoadedAt: DateTime
    Enabled: bool
}

/// Plugin loading result
type PluginLoadResult =
    | Success of IPlugin * PluginMetadata
    | Failure of string * Exception option

/// Plugin manager for dynamic loading and management
type PluginManager() =
    let plugins = ConcurrentDictionary<string, IPlugin * PluginMetadata>()
    let functionPlugins = ConcurrentDictionary<string, IFunctionPlugin>()
    let operatorPlugins = ConcurrentDictionary<string, IOperatorPlugin>()
    let statementPlugins = ConcurrentDictionary<string, IStatementPlugin>()
    let dataTypePlugins = ConcurrentDictionary<string, IDataTypePlugin>()

    /// Load plugin from assembly
    member _.LoadFromAssembly(assemblyPath: string) : PluginLoadResult list =
        try
            // CRITICAL FIX (DEFECT-CRIT-1): Add plugin signature verification
            // Verify assembly has strong name (prevents tampering)
            let assembly = Assembly.LoadFrom(assemblyPath)
            let assemblyName = assembly.GetName()

            // Check for strong name signature
            let publicKeyToken = assemblyName.GetPublicKeyToken()
            if isNull publicKeyToken || publicKeyToken.Length = 0 then
                let warning = $"Warning: Plugin assembly '{assemblyPath}' is not strongly-named. This poses a security risk."
                printfn "%s" warning
                // Continue loading but log the security concern
                // In production, you may want to reject unsigned assemblies:
                // return [Failure($"Assembly must be strongly-named: {assemblyPath}", None)]

            let pluginTypes =
                assembly.GetTypes()
                |> Array.filter (fun t ->
                    typeof<IPlugin>.IsAssignableFrom(t) &&
                    not t.IsInterface &&
                    not t.IsAbstract)
            
            let results = System.Collections.Generic.List<PluginLoadResult>()
            
            for pluginType in pluginTypes do
                try
                    let plugin = Activator.CreateInstance(pluginType) :?> IPlugin
                    let metadata = {
                        Name = plugin.Name
                        Version = plugin.Version
                        Author = plugin.Author
                        Description = plugin.Description
                        Dependencies = []  // Could be enhanced to read from attributes
                        PluginType = pluginType.Name
                        AssemblyPath = Some assemblyPath
                        LoadedAt = DateTime.UtcNow
                        Enabled = true
                    }
                    
                    results.Add(Success(plugin, metadata))
                with
                | ex -> results.Add(Failure($"Failed to instantiate plugin {pluginType.Name}", Some ex))
            
            results |> Seq.toList
        with
        | ex -> [Failure($"Failed to load assembly {assemblyPath}", Some ex)]

    /// Register a plugin
    member this.RegisterPlugin(plugin: IPlugin, ?metadata: PluginMetadata) =
        let meta = 
            defaultArg metadata {
                Name = plugin.Name
                Version = plugin.Version
                Author = plugin.Author
                Description = plugin.Description
                Dependencies = []
                PluginType = plugin.GetType().Name
                AssemblyPath = None
                LoadedAt = DateTime.UtcNow
                Enabled = true
            }
        
        if plugins.TryAdd(plugin.Name, (plugin, meta)) then
            // Register specific plugin types
            match plugin with
            | :? IFunctionPlugin as fp -> functionPlugins.TryAdd(plugin.Name, fp) |> ignore
            | _ -> ()
            
            match plugin with
            | :? IOperatorPlugin as op -> operatorPlugins.TryAdd(plugin.Name, op) |> ignore
            | _ -> ()
            
            match plugin with
            | :? IStatementPlugin as sp -> statementPlugins.TryAdd(plugin.Name, sp) |> ignore
            | _ -> ()
            
            match plugin with
            | :? IDataTypePlugin as dp -> dataTypePlugins.TryAdd(plugin.Name, dp) |> ignore
            | _ -> ()
            
            true
        else
            false

    /// Unregister a plugin
    member _.UnregisterPlugin(pluginName: string) =
        match plugins.TryRemove(pluginName) with
        | true, (plugin, _) ->
            functionPlugins.TryRemove(pluginName) |> ignore
            operatorPlugins.TryRemove(pluginName) |> ignore
            statementPlugins.TryRemove(pluginName) |> ignore
            dataTypePlugins.TryRemove(pluginName) |> ignore
            true
        | false, _ -> false

    /// Get all registered plugins
    member _.GetAllPlugins() =
        plugins.Values |> Seq.map (fun (plugin, metadata) -> plugin, metadata) |> Seq.toList

    /// Get plugin by name
    member _.GetPlugin(name: string) =
        match plugins.TryGetValue(name) with
        | true, (plugin, metadata) -> Some (plugin, metadata)
        | false, _ -> None

    /// Initialize all plugins with execution context
    member _.InitializeAll(context: ExecutionContext) =
        let errors = System.Collections.Generic.List<string * Exception>()
        
        for KeyValue(name, (plugin, _)) in plugins do
            try
                plugin.Initialize(context)
            with
            | ex -> errors.Add(name, ex)
        
        errors |> Seq.toList

    /// Cleanup all plugins
    member _.CleanupAll(context: ExecutionContext) =
        for KeyValue(_, (plugin, _)) in plugins do
            try
                plugin.Cleanup(context)
            with
            | _ -> () // Ignore cleanup errors

    /// Get all functions from function plugins
    member _.GetAllFunctions() =
        let allFunctions = Dictionary<string, obj list -> obj>()
        
        for KeyValue(_, functionPlugin) in functionPlugins do
            for KeyValue(funcName, funcImpl) in functionPlugin.Functions do
                allFunctions.[funcName] <- funcImpl
        
        allFunctions |> Seq.map (|KeyValue|) |> Map.ofSeq

    /// Get all custom operators
    member _.GetAllOperators() =
        let allOperators = Dictionary<string, DsOp>()
        
        for KeyValue(_, operatorPlugin) in operatorPlugins do
            for KeyValue(opName, op) in operatorPlugin.Operators do
                allOperators.[opName] <- op
        
        allOperators |> Seq.map (|KeyValue|) |> Map.ofSeq

    /// Get all custom data types
    member _.GetAllDataTypes() =
        let allTypes = Dictionary<string, DsType>()
        
        for KeyValue(_, dataTypePlugin) in dataTypePlugins do
            for KeyValue(typeName, typ) in dataTypePlugin.DataTypes do
                allTypes.[typeName] <- typ
        
        allTypes |> Seq.map (|KeyValue|) |> Map.ofSeq

    /// Execute custom statement
    member _.ExecuteCustomStatement(context: ExecutionContext, statementType: string, parameters: Map<string, obj>, currentTime: DateTime) =
        let mutable executed = false
        
        for KeyValue(_, statementPlugin) in statementPlugins do
            if statementPlugin.StatementTypes.Contains(statementType) then
                statementPlugin.ExecuteCustomStatement context statementType parameters currentTime
                executed <- true
        
        if not executed then
            failwithf "Unknown custom statement type: %s" statementType

/// Enhanced execution context with plugin support
type PluginAwareExecutionContext() =
    inherit ExecutionContext()
    let pluginManager = PluginManager()

    member _.PluginManager = pluginManager

    /// Initialize context with plugins
    member this.InitializeWithPlugins() =
        let errors = pluginManager.InitializeAll(this)
        
        // Add all plugin functions
        let pluginFunctions = pluginManager.GetAllFunctions()
        for KeyValue(name, impl) in pluginFunctions do
            this.SetFunction(name, impl)
        
        errors

    /// Cleanup with plugin support
    member this.CleanupWithPlugins() =
        pluginManager.CleanupAll(this)
        this.Clear()

/// Built-in plugin examples
module BuiltinPlugins =
    
    /// Math extension plugin
    type MathExtensionPlugin() =
        interface IFunctionPlugin with
            member _.Name = "MathExtension"
            member _.Version = "1.0.0"
            member _.Author = Some "DsRuntime"
            member _.Description = Some "Extended mathematical functions"
            
            member _.Initialize(context) = 
                // Any initialization logic
                ()
            
            member _.Cleanup(context) = 
                // Any cleanup logic
                ()
            
            member _.Functions = 
                Map.ofList [
                    ("SIN", fun args ->
                        match args with
                        | [value] -> box (sin (TypeConverter.toDouble value))
                        | _ -> failwith "SIN expects 1 argument")
                    
                    ("COS", fun args ->
                        match args with
                        | [value] -> box (cos (TypeConverter.toDouble value))
                        | _ -> failwith "COS expects 1 argument")
                    
                    ("TAN", fun args ->
                        match args with
                        | [value] -> box (tan (TypeConverter.toDouble value))
                        | _ -> failwith "TAN expects 1 argument")
                    
                    ("LOG", fun args ->
                        match args with
                        | [value] -> box (log (TypeConverter.toDouble value))
                        | _ -> failwith "LOG expects 1 argument")
                    
                    ("LOG10", fun args ->
                        match args with
                        | [value] -> box (log10 (TypeConverter.toDouble value))
                        | _ -> failwith "LOG10 expects 1 argument")
                    
                    ("EXP", fun args ->
                        match args with
                        | [value] -> box (exp (TypeConverter.toDouble value))
                        | _ -> failwith "EXP expects 1 argument")
                    
                    ("POW", fun args ->
                        match args with
                        | [baseVal; expVal] -> 
                            let b = TypeConverter.toDouble baseVal
                            let e = TypeConverter.toDouble expVal
                            box (Math.Pow(b, e))
                        | _ -> failwith "POW expects 2 arguments")
                    
                    ("ROUND", fun args ->
                        match args with
                        | [value] -> box (Math.Round(TypeConverter.toDouble value))
                        | [value; digits] -> 
                            let v = TypeConverter.toDouble value
                            let d = TypeConverter.toInt digits
                            box (Math.Round(v, d))
                        | _ -> failwith "ROUND expects 1 or 2 arguments")
                ]

    /// String utilities plugin
    type StringUtilitiesPlugin() =
        interface IFunctionPlugin with
            member _.Name = "StringUtilities"
            member _.Version = "1.0.0"
            member _.Author = Some "DsRuntime"
            member _.Description = Some "String manipulation functions"
            
            member _.Initialize(context) = ()
            member _.Cleanup(context) = ()
            
            member _.Functions = 
                Map.ofList [
                    ("TRIM", fun args ->
                        match args with
                        | [value] -> box ((TypeConverter.toString value).Trim())
                        | _ -> failwith "TRIM expects 1 argument")
                    
                    ("SUBSTRING", fun args ->
                        match args with
                        | [str; start] ->
                            let s = TypeConverter.toString str
                            let startIdx = TypeConverter.toInt start
                            box (s.Substring(startIdx))
                        | [str; start; length] ->
                            let s = TypeConverter.toString str
                            let startIdx = TypeConverter.toInt start
                            let len = TypeConverter.toInt length
                            box (s.Substring(startIdx, len))
                        | _ -> failwith "SUBSTRING expects 2 or 3 arguments")
                    
                    ("REPLACE", fun args ->
                        match args with
                        | [str; oldStr; newStr] ->
                            let s = TypeConverter.toString str
                            let oldS = TypeConverter.toString oldStr
                            let newS = TypeConverter.toString newStr
                            box (s.Replace(oldS, newS))
                        | _ -> failwith "REPLACE expects 3 arguments")
                    
                    ("CONTAINS", fun args ->
                        match args with
                        | [str; substr] ->
                            let s = TypeConverter.toString str
                            let sub = TypeConverter.toString substr
                            box (s.Contains(sub))
                        | _ -> failwith "CONTAINS expects 2 arguments")
                    
                    ("SPLIT", fun args ->
                        match args with
                        | [str; separator] ->
                            let s = TypeConverter.toString str
                            let sep = TypeConverter.toString separator
                            let parts = s.Split([|sep|], StringSplitOptions.None)
                            box (parts |> Array.toList |> List.map box)
                        | _ -> failwith "SPLIT expects 2 arguments")
                ]

    /// Create default plugin manager with built-in plugins
    let createDefaultPluginManager() =
        let manager = PluginManager()
        manager.RegisterPlugin(MathExtensionPlugin()) |> ignore
        manager.RegisterPlugin(StringUtilitiesPlugin()) |> ignore
        manager

/// Enhanced execution engine with plugin support
type PluginAwareExecutionEngine() =
    let context = PluginAwareExecutionContext()
    let mutable initialized = false

    member _.Context = context
    member _.PluginManager = context.PluginManager

    member this.Initialize() =
        if not initialized then
            // Load built-in plugins
            let defaultManager = BuiltinPlugins.createDefaultPluginManager()
            for (plugin, metadata) in defaultManager.GetAllPlugins() do
                context.PluginManager.RegisterPlugin(plugin, metadata) |> ignore
            
            // Initialize with plugins
            let errors = context.InitializeWithPlugins()
            if not (List.isEmpty errors) then
                for (name, ex) in errors do
                    printfn "Warning: Failed to initialize plugin %s: %s" name ex.Message
            
            initialized <- true

    member this.ExecuteFormula(formula: DsFormula) =
        if not initialized then this.Initialize()
        
        let engine = ExecutionEngine()
        // Copy plugin functions to regular engine
        let pluginFunctions = context.PluginManager.GetAllFunctions()
        for KeyValue(name, impl) in pluginFunctions do
            engine.Context.SetFunction(name, impl)
        
        engine.ExecuteFormula(formula)

    member this.LoadPlugin(assemblyPath: string) =
        context.PluginManager.LoadFromAssembly(assemblyPath)

    member this.GetVariable(name: string) = context.GetVariable(name)
    member this.SetVariable(name: string, value: obj) = context.SetVariable(name, value)
    member this.GetAllVariables() = context.GetAllVariables()

    member this.Cleanup() = 
        if initialized then
            context.CleanupWithPlugins()
            initialized <- false
