module Domain

open System.Reflection
open System.IO
open FSharp.Core.CompilerServices
open FSharp.Data
open ProviderImplementation.ProvidedTypes
open Nethereum.ABI.FunctionEncoding.Attributes
open Newtonsoft.Json.Linq
open System

[<TypeProviderAssembly>]
do ()

[<Literal>]
let abiJsonSchema = @"[
{
    ""constant"": false,
    ""inputs"": [
    {
        ""internalType"": ""address"",
        ""name"": ""apt"",
        ""type"": ""address""
    },
    {
        ""internalType"": ""address"",
        ""name"": ""urn"",
        ""type"": ""address""
    },
    {
        ""internalType"": ""uint256"",
        ""name"": ""wad"",
        ""type"": ""uint256""
    }
    ],
    ""name"": ""daiJoin_join"",
    ""outputs"": [{
    ""internalType"": ""int256"",
    ""name"": """",
    ""type"": ""int256""
    }],
    ""payable"": false,
    ""stateMutability"": ""nonpayable"",
    ""type"": ""function""
}
]"

type AbiSchema = JsonProvider.JsonProvider<abiJsonSchema>

let getAttributeWithParams (attributeType:Type) (args: obj[]) = 
    { new Reflection.CustomAttributeData() with
        member __.Constructor = args |> Array.map (fun i -> i.GetType()) |> attributeType.GetConstructor
        member __.ConstructorArguments = args |> Array.map (fun i -> CustomAttributeTypedArgument(i.GetType(), i)) :> Collections.Generic.IList<_>
        member __.NamedArguments = [||] :> Collections.Generic.IList<_> }

let constructRootType (asm:Assembly) (ns:string) (typeName:string) (paramValues: obj[]) = 
    let createType (contractName, abi) =
        let data = AbiSchema.Parse(abi)

        // mock for a while
        let solidityTypeToNetType solType = typeof<bigint>

        let solidityOutputToNetProperty index (output:AbiSchema.Outputs) =
            let netType = solidityTypeToNetType output.Type
            let name = if output.Name |> System.String.IsNullOrWhiteSpace then (sprintf "Prop%i" index) else output.Name
            let property = ProvidedProperty(name, netType)
    
            getAttributeWithParams typeof<ParameterAttribute> [|output.Type;output.Name;index+1|]
            |> property.AddCustomAttribute

            property

        let solidityOutputTypesToNetReturnType (functionName:string) (solTypes:AbiSchema.Outputs[]) =
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

        let solidityFunctionToNetMethod (solidityFunction:AbiSchema.Root) = 
            let parameters = solidityFunction.Inputs
                             |> Array.map (fun j -> ProvidedParameter(j.Name, solidityTypeToNetType j.InternalType))
                             |> Array.toList
            let returnType = solidityOutputTypesToNetReturnType solidityFunction.Name solidityFunction.Outputs
            ProvidedMethod(solidityFunction.Name, parameters, returnType)

        let methods = data 
                      |> Array.where (fun i -> i.Type = "function") 
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