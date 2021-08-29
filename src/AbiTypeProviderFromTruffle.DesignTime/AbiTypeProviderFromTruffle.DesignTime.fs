module AbiTypeProviderFromTruffleImplementation

open System.Reflection
open System.IO
open System
open FSharp.Core.CompilerServices
open ProviderImplementation.ProvidedTypes
open AbiTypeProvider
open System.Diagnostics
open System.Text.RegularExpressions
open ConstructRootType

[<TypeProviderAssembly>]
do ()

let resolvePath inPath resolutionFolder (cfg:TypeProviderConfig) = 
    match Uri.TryCreate(inPath, UriKind.RelativeOrAbsolute) with
    | true, uri -> 
        if uri.IsAbsoluteUri then
            inPath
        else
            let root = 
                if String.IsNullOrWhiteSpace resolutionFolder then 
                    cfg.ResolutionFolder
                else resolutionFolder
            Path.Combine(root, inPath) |> Path.GetFullPath
    | _ -> 
        if String.IsNullOrWhiteSpace resolutionFolder then 
            cfg.ResolutionFolder 
        else resolutionFolder

let constructRootTypeByTruffle asm (ns:string) (cfg:TypeProviderConfig) (typeName:string) (paramValues: obj[]) =
    let truffleConfigFile = paramValues.[0] :?> string
    let cleanBeforeBuild = paramValues.[1] :?> bool
    let resolutionFolder = paramValues.[2] :?> string

    let isWindows = Runtime.InteropServices.RuntimeInformation.IsOSPlatform(Runtime.InteropServices.OSPlatform.Windows)
    let configFile = resolvePath truffleConfigFile resolutionFolder cfg
    let configFolder = Path.GetDirectoryName configFile
    
    let contractsBuildDirectory = 
        let procInfo = 
            if isWindows then
                let procInfo = ProcessStartInfo("cmd.exe")
                procInfo.Arguments <- "/c npx.cmd truffle config get contracts_build_directory"
                procInfo
            else
                let procInfo = ProcessStartInfo("npx")
                procInfo.Arguments <- "truffle config get contracts_build_directory"
                procInfo

        procInfo.RedirectStandardOutput <- true
        procInfo.RedirectStandardError <- true
        procInfo.WorkingDirectory <- configFolder
        let proc = Process.Start(procInfo)
        proc.WaitForExit()

        let output = proc.StandardOutput.ReadToEnd()
        let error = proc.StandardError.ReadToEnd()
        if proc.ExitCode <> 0 then 
            let msg = sprintf "Error get contracts_build_directory: %s\n%s" output error
            System.Console.Error.Write msg
            failwith msg

        output.Trim()

    let build() = 
        if cleanBeforeBuild then 
            Directory.GetFiles(contractsBuildDirectory, "*.json") |> Seq.iter File.Delete

        
        let procInfo = 
            if isWindows then
                let procInfo = ProcessStartInfo("cmd.exe")
                procInfo.Arguments <- "/c npx.cmd truffle build"
                procInfo
            else
                let procInfo = ProcessStartInfo("npx")
                procInfo.Arguments <- "truffle build"
                procInfo

        procInfo.RedirectStandardOutput <- true
        procInfo.RedirectStandardError <- true
        procInfo.WorkingDirectory <- configFolder
        let proc = Process.Start(procInfo)
        proc.WaitForExit()

        let output = proc.StandardOutput.ReadToEnd()
        let error = proc.StandardError.ReadToEnd()
        if proc.ExitCode <> 0 then 
            let msg = sprintf "Error execute: %s\n%s" output error
            System.Console.Error.Write msg
            failwith msg
        
    build()
    constructRootType asm ns typeName contractsBuildDirectory


[<TypeProvider>]
type AbiTypeProviderFromTruffle (config:TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces (config, assemblyReplacementMap=[("AbiTypeProviderFromTruffle.DesignTime", "AbiTypeProviderFromTruffle.Runtime")])


    let ns = "AbiTypeProvider"
    let asm = Assembly.GetExecutingAssembly()

    let staticParams = [
        ProvidedStaticParameter("TrufleConfigFile", typeof<string>)
        ProvidedStaticParameter("CleanBeforeBuild", typeof<bool>, parameterDefaultValue = true)
        ProvidedStaticParameter("ResolutionFolder", typeof<string>, parameterDefaultValue = "")
    ]

    // check we contain a copy of runtime files, and are not referencing the runtime DLL
    do assert (typeof<ContractPlug>.Assembly.GetName().Name = asm.GetName().Name)

    let typesByFolder = ProvidedTypeDefinition(asm, ns, "AbiTypesFromTruffle", Some typeof<obj>, isErased = false)

    do typesByFolder.DefineStaticParameters(staticParams, constructRootTypeByTruffle asm ns config)
    
    do this.AddNamespace(ns, [typesByFolder])