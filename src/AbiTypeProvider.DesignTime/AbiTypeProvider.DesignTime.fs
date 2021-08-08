module AbiTypeProviderImplementation

open System.Reflection
open System.IO
open System
open FSharp.Core.CompilerServices
open ProviderImplementation.ProvidedTypes
open AbiTypeProvider
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

let constructRootTypeByFolder (ns:string) (cfg:TypeProviderConfig) (typeName:string) (paramValues: obj[]) =
    let contractsFolderPath = paramValues.[0] :?> string
    let resolutionFolder = paramValues.[1] :?> string

    let buildPath = resolvePath contractsFolderPath resolutionFolder cfg
    constructRootType ns typeName buildPath


[<TypeProvider>]
type AbiTypeProvider (config:TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces (config, assemblyReplacementMap=[("AbiTypeProvider.DesignTime", "AbiTypeProvider.Runtime")])

    let ns = "AbiTypeProvider"
    let asm = Assembly.GetExecutingAssembly()

    let staticParams = [
        ProvidedStaticParameter("ContractsFolderPath", typeof<string>)
        ProvidedStaticParameter("ResolutionFolder", typeof<string>, parameterDefaultValue = "")
    ]

    // check we contain a copy of runtime files, and are not referencing the runtime DLL
    do assert (typeof<ContractPlug>.Assembly.GetName().Name = asm.GetName().Name)

    let typesByFolder = ProvidedTypeDefinition(asm, ns, "AbiTypes", Some typeof<obj>, isErased = false)

    do typesByFolder.DefineStaticParameters(staticParams, constructRootTypeByFolder ns config)
    
    do this.AddNamespace(ns, [typesByFolder])


