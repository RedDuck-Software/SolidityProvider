module SolidityProviderImplementation

open System.Reflection
open System.IO
open System
open FSharp.Core.CompilerServices
open FSharp.Json
open Nethereum.ABI.FunctionEncoding.Attributes
open Newtonsoft.Json.Linq
open ProviderImplementation.ProvidedTypes
open SolidityProviderNamespace

[<TypeProviderAssembly>]
do ()

type Address = string

type Parameter = {
    internalType:string;
    name:string;
    _type:string; // todo add annotation
}

type Root = {
    constant: bool;
    inputs: Parameter array;
    name: string;
    outputs: Parameter array;
    payable: bool;
    stateMutability: string;
    _type: string; // todo add annotation
}

let getAttributeWithParams (attributeType:Type) (args: obj[]) = 
    { new Reflection.CustomAttributeData() with
        member __.Constructor = args |> Array.map (fun i -> i.GetType()) |> attributeType.GetConstructor
        member __.ConstructorArguments = args |> Array.map (fun i -> CustomAttributeTypedArgument(i.GetType(), i)) :> Collections.Generic.IList<_>
        member __.NamedArguments = [||] :> Collections.Generic.IList<_> }

let constructRootType (asm:Assembly) (ns:string) (typeName:string) (paramValues: obj[]) = 
    let createType (contractName, abi) =
        let roots = Json.deserialize<Root array> abi

        // mock for a while
        let solidityTypeToNetType solType = typeof<bigint>

        let solidityOutputToNetProperty index (output:Parameter) =
            let netType = solidityTypeToNetType output._type
            let name = if output.name |> System.String.IsNullOrWhiteSpace then (sprintf "Prop%i" index) else output.name
            let property = ProvidedProperty(name, netType)
        
            getAttributeWithParams typeof<ParameterAttribute> [|output._type;output.name;index+1|]
            |> property.AddCustomAttribute
        
            property

        let solidityOutputTypesToNetReturnType (functionName:string) (solTypes:Parameter array) =
            match Array.length solTypes with
              | 1 -> solTypes |> Array.head |> solidityTypeToNetType
              | _ ->
                let properties = solTypes |> Array.mapi solidityOutputToNetProperty |> Array.toList
                // TODO: not sure if we need to add the providedTypeDefinition somewhere
                let outputType = ProvidedTypeDefinition(functionName |> sprintf "%sOutputDTO", Some <| typeof<FunctionOutputDTO>)
        
                // throw if we don't find Nethereum's FunctionOutputAttribute
                asm.GetCustomAttributesData() |> Seq.find (fun i -> i.AttributeType = typeof<FunctionOutputAttribute>)
                |> outputType.AddCustomAttribute

                properties |> outputType.AddMembers

                upcast outputType

        let solidityFunctionToNetMethod (solidityFunction:Root) = 
            let parameters = solidityFunction.inputs
                             |> Array.map (fun j -> ProvidedParameter(j.name, solidityTypeToNetType j.internalType))
                             |> Array.toList
            let returnType = solidityOutputTypesToNetReturnType solidityFunction.name solidityFunction.outputs
            ProvidedMethod(solidityFunction.name, parameters, returnType)

        let methods = roots
                      |> Array.where (fun i -> i._type = "function") 
                      |> Array.map solidityFunctionToNetMethod

        let providedType = ProvidedTypeDefinition(sprintf "%sContract" contractName, Some typeof<obj>)

        providedType.AddMembers(Array.toList methods)
        providedType

    match paramValues with 
    | [| :? string as contractsFolderPath |] -> 
        let rootType = ProvidedTypeDefinition(asm, ns, typeName, Some typeof<obj>)
        let types = Directory.EnumerateFiles(contractsFolderPath) 
                    |> Seq.map File.ReadAllText 
                    |> Seq.map (fun json -> 
                                let parsedJson = JObject.FromObject(json)
                                let abiJson = parsedJson.["abi"].ToString()
                                let contractName = parsedJson.["contractName"].ToString()
                                (contractName, abiJson))
                    |> Seq.map createType
                    |> Seq.toList

        rootType.AddMembers types    
        rootType

[<TypeProvider>]
type SolidityProvider (config:TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces (config, assemblyReplacementMap=[("SolidityProvider.DesignTime", "SolidityProvider.Runtime")])

    let ns = "SolidityProviderNS"
    let asm = Assembly.GetExecutingAssembly()
    let staticParams = [ProvidedStaticParameter("value", typeof<string>)]

    // check we contain a copy of runtime files, and are not referencing the runtime DLL
    do assert (typeof<DataSource>.Assembly.GetName().Name = asm.GetName().Name)

    let t = ProvidedTypeDefinition(asm, ns, "SolidityTypes", Some typeof<obj>, hideObjectMethods = true)

    do t.DefineStaticParameters(staticParams, constructRootType asm ns)

    do this.AddNamespace(ns, [t])