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


let getAttribute (attributeType:Type)  = 
    { new Reflection.CustomAttributeData() with
        member __.Constructor = attributeType.GetConstructor(Type.EmptyTypes)
        member __.ConstructorArguments = [||] :> IList<_>
        member __.NamedArguments = [||] :> IList<_> }

let getAttributeWithParams (attributeType:Type) (args: obj[]) = 
    { new Reflection.CustomAttributeData() with
        member __.Constructor = args |> Array.map (fun i -> i.GetType()) |> attributeType.GetConstructor
        member __.ConstructorArguments = args |> Array.map (fun i -> CustomAttributeTypedArgument(i.GetType(), i)) :> Collections.Generic.IList<_>
        member __.NamedArguments = [||] :> Collections.Generic.IList<_> }

let constructRootType (ns:string) (cfg:TypeProviderConfig) (typeName:string) (paramValues: obj[]) = 
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

        let getTypes nameSufix (json: JObject) =
            
            match string json.["type"] with
            | "function" -> 
                let dto = Json.deserialize<FunctionJsonDTO> (string json) 
                let functionType = makeSolidityType (sprintf "%s%sFunction" dto.name nameSufix) typeof<FunctionMessage> dto.inputs
                let functionOutputType = makeSolidityType (sprintf "%s%sOutputDTO" dto.name nameSufix) typeof<FunctionOutputDTO> dto.outputs
                
                functionOutputType.AddCustomAttribute (getAttribute typeof<FunctionOutputAttribute>)

                match dto.outputs with
                | [||] -> 
                    functionType.AddCustomAttribute (getAttributeWithParams typeof<FunctionAttribute> [|dto.name|])
                | [|param|] -> 
                    functionType.AddCustomAttribute (getAttributeWithParams typeof<FunctionAttribute> [|dto.name; param._type|])
                | _ -> 
                    functionType.AddCustomAttribute (getAttributeWithParams typeof<FunctionAttribute> [|dto.name; functionOutputType|])
                

                [functionOutputType; functionType]
            | "event" -> 
                let dto = Json.deserialize<EventJsonDTO> (string json) 
                let eventType =  makeSolidityType (sprintf "%sEventDTO" dto.name) typeof<obj> dto.inputs //EventDTO
                
                eventType.AddCustomAttribute (getAttributeWithParams typeof<EventAttribute> [|dto.name|])

                [eventType]
            | "constructor" ->
                let dto = Json.deserialize<ConstructorJsonDTO> (string json) 
                let constructorType =  makeSolidityType (sprintf "%sDeployment" contractName) typeof<obj> dto.inputs

                [constructorType]
            | _ -> []

        
        let solidityTypes = 
            abi 
            |> Seq.groupBy(fun json -> 
                 let _type = string json.["type"]
                 let _name = 
                    match json.TryGetValue "name" with
                    | true, v -> string v
                    | _ -> "Noname"
                 _type + _name
            )
            |> Seq.collect(fun (_,group) ->
                match group |> Seq.toList with
                | [] -> []
                | [json] -> getTypes "" json
                | headJson::tailJson -> 
                    let headTypes = getTypes "" headJson
                    let tailTypes = tailJson |> List.mapi(fun i json -> getTypes (sprintf "%i" (i + 1)) json) |> List.collect id
                    List.append headTypes tailTypes
                 )
            |> Seq.toList

        let asm = ProvidedAssembly()
        asm.AddTypes(solidityTypes)

        let providedType = ProvidedTypeDefinition(sprintf "%sContract" contractName, Some typeof<obj>, isErased=false)

        providedType.AddMember <| ProvidedConstructor(parameters = [], invokeCode = fun _ -> <@@ () @@>)
        providedType.AddMembers(solidityTypes)
        asm.AddTypes([providedType])
        providedType

    let contractsFolderPath = paramValues.[0] :?> string
    let resolutionFolder = paramValues.[1] :?> string

    let fullPath = 
        match Uri.TryCreate(contractsFolderPath, UriKind.RelativeOrAbsolute) with
        | true, uri -> 
            if uri.IsAbsoluteUri then
                contractsFolderPath
            else
                let root = 
                    if String.IsNullOrWhiteSpace resolutionFolder then 
                        cfg.ResolutionFolder
                    else resolutionFolder
                Path.Combine(root, contractsFolderPath)
        | _ -> 
            if String.IsNullOrWhiteSpace resolutionFolder then 
                cfg.ResolutionFolder 
            else resolutionFolder

    let contractTypes = 
        Directory.EnumerateFiles(fullPath, "*.json") 
        |> Seq.map File.ReadAllText
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

    let staticParams = [
        ProvidedStaticParameter("ContractsFolderPath", typeof<string>)
        ProvidedStaticParameter("ResolutionFolder", typeof<string>, parameterDefaultValue = "")
    ]

    // check we contain a copy of runtime files, and are not referencing the runtime DLL
    do assert (typeof<DataSource>.Assembly.GetName().Name = asm.GetName().Name)

    let t = ProvidedTypeDefinition(asm, ns, "SolidityTypes", Some typeof<obj>, isErased=false)

    do t.DefineStaticParameters(staticParams, constructRootType ns config)
    
    do this.AddNamespace(ns, [t])