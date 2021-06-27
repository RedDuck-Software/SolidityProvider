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
open System.Diagnostics
open Nethereum.Contracts
open Microsoft.FSharp.Quotations

[<TypeProviderAssembly>]
do ()

type Address = string

type Parameter = {
    internalType:string;
    name:string;
    [<JsonField("type")>]
    _type:string;
}

type Root = {
    constant: bool;
    inputs: Parameter array;
    name: string;
    outputs: Parameter array;
    payable: bool;
    stateMutability: string;
    [<JsonField("type")>]
    _type: string;
}

let getAttributeWithParams (attributeType:Type) (args: obj[]) = 
    { new Reflection.CustomAttributeData() with
        member __.Constructor = args |> Array.map (fun i -> i.GetType()) |> attributeType.GetConstructor
        member __.ConstructorArguments = args |> Array.map (fun i -> CustomAttributeTypedArgument(i.GetType(), i)) :> Collections.Generic.IList<_>
        member __.NamedArguments = [||] :> Collections.Generic.IList<_> }

let constructRootType (ns:string) (typeName:string) (paramValues: obj[]) = 
    let createType (contractName, abi) =
        let roots = Json.deserialize<Root array> abi

        // mock for a while
        let solidityTypeToNetType solType = typeof<bigint>

        let solidityOutputToNetProperty index (param:Parameter) =
            Debug.WriteLine(sprintf "index: %A | output: %A" index param)
            let netType = solidityTypeToNetType param._type
            let name = if param.name |> System.String.IsNullOrWhiteSpace then (sprintf "Prop%i" index) else param.name
            let property = ProvidedProperty(name, netType)
        
            getAttributeWithParams typeof<ParameterAttribute> [|param._type;param.name;index+1|]
            |> property.AddCustomAttribute

            property

        let solidityTypesToNetDTO (functionName:string) (solTypes:Parameter array) isOutput =
            Debug.WriteLine(sprintf "functionName: %A | solTypes: %A" functionName solTypes)
            let properties = solTypes |> Array.mapi solidityOutputToNetProperty |> Array.toList

            let (postfix, baseType, attribute) = 
                    if isOutput
                        then ("OutputDTO", typeof<FunctionOutputDTO>, getAttributeWithParams typeof<FunctionOutputAttribute> [||])
                        else ("Function", typeof<FunctionOutputDTO>, getAttributeWithParams typeof<FunctionOutputAttribute> [||])
                                //typeof<FunctionMessage>,
                                //getAttributeWithParams typeof<FunctionAttribute> [|functionName;typeof<FunctionOutputAttribute>|]) // todo here we need to know about all outputDTOs

            let netType = ProvidedTypeDefinition(sprintf "%s%s" functionName postfix, Some <| baseType, isErased=false)
            let ctor = ProvidedConstructor(parameters = [], invokeCode = fun _ -> <@@ () @@>)

            attribute |> netType.AddCustomAttribute
            properties |> netType.AddMembers
            ctor |> netType.AddMember

            netType

        // todo here - restructure to first process output then input
        let solidityFunctionToNetMethod (solidityFunction:Root) = 
            let returnTypeIn = solidityTypesToNetDTO solidityFunction.name solidityFunction.inputs false
            let returnTypeOut = solidityTypesToNetDTO solidityFunction.name solidityFunction.outputs true
            
            let functionType = ProvidedTypeDefinition(sprintf "%sFunctionTypes" solidityFunction.name, Some <| typeof<obj>, hideObjectMethods = true, isErased=false)
            functionType.AddMembers <| [returnTypeOut;returnTypeIn]

            functionType, [returnTypeIn;returnTypeOut]

        let functionDTOsAndTypesToAdd = 
                      roots
                      |> Array.where (fun i -> i._type = "function") 
                      |> Array.map solidityFunctionToNetMethod

        let functionDTOs = functionDTOsAndTypesToAdd |> Array.map fst
        let typesToAdd = functionDTOsAndTypesToAdd |> Array.map snd |> Array.toList |> List.fold (fun currentList list -> List.concat [currentList;list]) []
        
        let asm = ProvidedAssembly()

        let providedType = ProvidedTypeDefinition(sprintf "%sContract" contractName, Some typeof<obj>, isErased=false)

        providedType.AddMembers (Array.toList functionDTOs)
        providedType.AddMember <| ProvidedConstructor(parameters = [], invokeCode = fun _ -> <@@ () @@>)
        asm.AddTypes([providedType])
        (providedType, typesToAdd)

    match paramValues with 
    | [| :? string as contractsFolderPath |] -> 
        let asm = ProvidedAssembly()
        let rootType = ProvidedTypeDefinition(asm, ns, typeName, Some typeof<obj>, isErased=false)
        let (contractTypes, typesToAddToAssembly) = 
                    Directory.EnumerateFiles(contractsFolderPath) 
                    |> Seq.map File.ReadAllText
                    |> Seq.map (fun i ->
                                        //do printfn "ReadAllText %A" i 
                                        i)
                    |> Seq.map (fun json -> 
                                let parsedJson = JObject.Parse(json)
                                //printfn "parsedJson: %A" parsedJson
                                let abis = parsedJson.["abi"]
                                let abiJson = abis.Children<JObject>() 
                                                |> Seq.where (fun i -> string i.["type"] = "function") 
                                                |> JArray |> string
                                //printfn "abiJson: %A" abiJson
                                let contractName = parsedJson.["contractName"].ToString()
                                printfn "contractName: %A" contractName

                                (contractName, abiJson))
                    |> Seq.map createType
                    |> Seq.toList
                    |> List.fold (fun (contractTypes, typesToAdd1) (contractType, typesToAdd2) -> (contractType::contractTypes, List.concat [typesToAdd1;typesToAdd2])) ([], [])
        
        //List.concat [contractTypes; typesToAddToAssembly] |> asm.AddTypes

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