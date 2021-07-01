module SolidityProviderImplementation

open System.Reflection
open System.IO
open System
open FSharp.Core.CompilerServices
open FSharp.Json
open Nethereum.ABI.FunctionEncoding.Attributes
open Newtonsoft.Json.Linq
open ProviderImplementation.ProvidedTypes
open ProviderImplementation.ProvidedTypes.UncheckedQuotations
open SolidityProviderNamespace
open System.Diagnostics
open Nethereum.Contracts
open Microsoft.FSharp.Quotations
open System.Collections.Generic

[<TypeProviderAssembly>]
do ()

type Address = string

type Parameter = {
    internalType:string;
    name:string;
    [<JsonField("type")>]
    _type:string;
}

type ConstructorJsonDTO = {
    inputs: Parameter array;
    payable: bool;
    stateMutability: string;
}

type EventJsonDTO = {
    inputs: Parameter array;
    name: string;
    anonymous: bool;
}

type FunctionJsonDTO = {
    constant: bool;
    inputs: Parameter array;
    name: string;
    outputs: Parameter array;
    payable: bool;
    stateMutability: string;
}

let getAttributeWithParams (attributeType:Type) (args: obj[]) = 
    { new Reflection.CustomAttributeData() with
        member __.Constructor = args |> Array.map (fun i -> i.GetType()) |> attributeType.GetConstructor
        member __.ConstructorArguments = args |> Array.map (fun i -> CustomAttributeTypedArgument(i.GetType(), i)) :> Collections.Generic.IList<_>
        member __.NamedArguments = [||] :> Collections.Generic.IList<_> }

let constructRootType (ns:string) (typeName:string) (paramValues: obj[]) = 
    let createType (contractName, abi:JEnumerable<JObject>) =

        let solidityTypeToNetType solType = 
            match solType with
            | "uint256" -> typeof<bigint>
            | "address" -> typeof<string>
            | "bytes" | "bytes32" | "bytes4" -> typeof<byte array>
            | _ -> typeof<string>

        let addProperty (provideType: ProvidedTypeDefinition) index (param:Parameter)  =

            Debug.WriteLine(sprintf "Proprty name: %A; index: %i" param.name index)
            let netType = solidityTypeToNetType param._type
            let propertyName = if param.name |> System.String.IsNullOrWhiteSpace then (sprintf "Prop%i" index) else param.name

            let field = ProvidedField("_" + propertyName, netType)

            let getter = fun [this] -> Expr.FieldGetUnchecked(this, field)
            let setter = fun [this; v] -> Expr.FieldSetUnchecked(this, field, v)
            let property = ProvidedProperty(propertyName, netType, isStatic = false, getterCode = getter, setterCode = setter)

            getAttributeWithParams typeof<ParameterAttribute> [|param._type;param.name;index+1|]
            |> property.AddCustomAttribute

            provideType.AddMember property
            provideType.AddMember field
            ()


        let makeSolidityType name baseType (inputs: Parameter seq) = 
            let solidityType = ProvidedTypeDefinition(name, Some <| baseType , hideObjectMethods = true, isErased=false)

            let constructor = ProvidedConstructor(parameters = [], invokeCode = fun _ -> <@@ () @@>)

            inputs |> Seq.iteri (addProperty solidityType)

            solidityType.AddMember constructor
            solidityType

        let getTypes (json: JObject) =
            
            match string json.["type"] with
            | "function" -> 
                let dto = Json.deserialize<FunctionJsonDTO> (string json) 
                let functionType = makeSolidityType (sprintf "%sFunction" dto.name) typeof<FunctionMessage> dto.inputs
                let functionOutputType = makeSolidityType (sprintf "%sOutputDTO" dto.name) typeof<FunctionOutputDTO> dto.outputs

                [functionType; functionOutputType]
            | "event" -> 
                let dto = Json.deserialize<EventJsonDTO> (string json) 
                let eventType =  makeSolidityType (sprintf "%sEventDTO" dto.name) typeof<obj> dto.inputs //EventDTO

                [eventType]
            | "constructor" ->
                let dto = Json.deserialize<ConstructorJsonDTO> (string json) 
                let constructorType =  makeSolidityType (sprintf "%sDeployment" contractName) typeof<obj> dto.inputs

                [constructorType]
            | _ -> []

        
        let solidityTypes = abi |> Seq.collect getTypes |> Seq.toList

        let asm = ProvidedAssembly()
        asm.AddTypes(solidityTypes)

        let providedType = ProvidedTypeDefinition(sprintf "%sContract" contractName, Some typeof<obj>, isErased=false)

        providedType.AddMember <| ProvidedConstructor(parameters = [], invokeCode = fun _ -> <@@ () @@>)
        providedType.AddMembers(solidityTypes)
        asm.AddTypes([providedType])
        providedType

    match paramValues with 
    | [| :? string as contractsFolderPath |] -> 
        let contractTypes = 
                    Directory.EnumerateFiles(contractsFolderPath) 
                    |> Seq.map File.ReadAllText
                    |> Seq.map (fun i ->
                                        //do printfn "ReadAllText %A" i 
                                        i)
                    |> Seq.map (fun json -> 
                                let parsedJson = JObject.Parse(json)
                                //printfn "parsedJson: %A" parsedJson
                                let abis = parsedJson.["abi"]
                                let abiJson = abis.Children<JObject>() //|> string
                                //printfn "abiJson: %A" abiJson
                                let contractName = parsedJson.["contractName"].ToString()
                                printfn "contractName: %A" contractName

                                (contractName, abiJson))
                    |> Seq.map createType
                    |> Seq.toList
                    |> List.fold (fun contractTypes contractType -> contractType::contractTypes) []

        let asm = ProvidedAssembly()
        let rootType = ProvidedTypeDefinition(asm, ns, typeName, Some typeof<obj>, isErased=false)
        
        rootType.AddMembers contractTypes
        asm.AddTypes([rootType])
        rootType

[<TypeProvider>]
type SolidityProvider (config:TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces (config, assemblyReplacementMap=[("SolidityProvider.DesignTime", "SolidityProvider.Runtime")])

    let ns = "SolidityProviderNS"
    let asm = Assembly.GetExecutingAssembly()

    let staticParams = [ProvidedStaticParameter("value", typeof<string>)]

    // check we contain a copy of runtime files, and are not referencing the runtime DLL
    do assert (typeof<DataSource>.Assembly.GetName().Name = asm.GetName().Name)

    let t = ProvidedTypeDefinition(asm, ns, "SolidityTypes", Some typeof<obj>, isErased=false)

    do t.DefineStaticParameters(staticParams, constructRootType ns)
    
    do this.AddNamespace(ns, [t])