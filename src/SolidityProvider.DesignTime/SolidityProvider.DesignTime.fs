module SolidityProviderImplementation

open System.Reflection
open FSharp.Core.CompilerServices
open MyNamespace
open ProviderImplementation.ProvidedTypes

// Put any utility helpers here
[<AutoOpen>]
module internal Helpers =
    ()

[<TypeProvider>]
type SolidityProvider (config:TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces (config, assemblyReplacementMap=[("SolidityProvider.DesignTime", "SolidityProvider.Runtime")])

    let ns = "SolidityProviderNS"
    let asm = Assembly.GetExecutingAssembly()
    let staticParams = [ProvidedStaticParameter("value", typeof<string>)]

    // check we contain a copy of runtime files, and are not referencing the runtime DLL
    do assert (typeof<DataSource>.Assembly.GetName().Name = asm.GetName().Name)

    let t = ProvidedTypeDefinition(asm, ns, "SolidityTypes", Some typeof<obj>, hideObjectMethods = true)

    do t.DefineStaticParameters(staticParams, Domain.constructRootType asm ns)

    do this.AddNamespace(ns, [t])

[<TypeProviderAssembly>]
do ()