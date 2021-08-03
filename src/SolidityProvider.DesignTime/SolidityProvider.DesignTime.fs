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
open Nethereum.Contracts
open Microsoft.FSharp.Quotations
open System.Collections.Generic
open System.Threading.Tasks
open Nethereum.RPC.Eth.DTOs
open Nethereum.Web3
open System.Numerics
open System.Diagnostics
open System.Text.RegularExpressions

[<TypeProviderAssembly>]
do ()


type DataObject() =
    let data = Dictionary<string,obj>()
    member x.RuntimeOperation() = data.Count


type Address = string

type Parameter = {
    indexed: bool option
    internalType:string option;
    name:string;
    [<JsonField("type")>]
    _type:string;
}

type ConstructorJsonDTO = {
    inputs: Parameter array;
    payable: bool option;
    stateMutability: string;
}

type EventJsonDTO = {
    inputs: Parameter array;
    name: string;
    anonymous: bool option;
}

type FunctionJsonDTO = {
    constant: bool option;
    inputs: Parameter array;
    name: string;
    outputs: Parameter array;
    payable: bool option;
    stateMutability: string;
}


let solidityTypeToNetType solType = 
    match solType with
    | "uint256" | "unit160" | "uint128" | "uint80" | "int256" | "int160" | "int128" | "int80" -> typeof<BigInteger>
    | "uint8" -> typeof<uint8>
    | "uint16" -> typeof<uint16>
    | "uint32" -> typeof<uint32>
    | "uint64" -> typeof<uint64>
    | "address" -> typeof<string>
    | "bool" -> typeof<bool>
    | "bytes" | "bytes32" | "bytes4" -> typeof<byte array>
    | _ -> typeof<string>


let getAttribute (attributeType:Type)  = 
    { new Reflection.CustomAttributeData() with
        member __.Constructor = attributeType.GetConstructor([||])
        member __.ConstructorArguments = [||] :> IList<_>
        member __.NamedArguments = [||] :> IList<_> }

let getAttributeWithParams (attributeType:Type) (args: obj[]) = 
    { new Reflection.CustomAttributeData() with
        member __.Constructor = args |> Array.map (fun i -> i.GetType()) |> attributeType.GetConstructor
        member __.ConstructorArguments = args |> Array.map (fun i -> CustomAttributeTypedArgument(i.GetType(), i)) :> Collections.Generic.IList<_>
        member __.NamedArguments = [||] :> Collections.Generic.IList<_> }

let constructRootType (ns:string) (typeName:string) (buildPath: string) = 
    let createType (fileName: string, contractName, abis:JToken, byteCode: string option) =
        
        let contractPlug = ProvidedField("_contractPlugin", typeof<ContractPlug>)
        let gasArgs = [
            ProvidedParameter("gas", typeof<uint64>, optionalValue = uint64 9500000UL)
            ProvidedParameter("gasPrice", typeof<uint64>, optionalValue = uint64 8000000000UL)
        ]

        let weiValue = ProvidedParameter("weiValue", typeof<uint64>, optionalValue = 1UL)

        let getPropertyName index (param:Parameter) = 
            if param.name |> System.String.IsNullOrWhiteSpace then (sprintf "Prop%i" index) else param.name

        let makeField index (param:Parameter) =
            let netType = solidityTypeToNetType param._type
            let fieldName = "_" + (getPropertyName index param)
            ProvidedField(fieldName, netType)

        let makeProperty index (field: ProvidedField) (param:Parameter) =
            let propertyName = getPropertyName index param
            let netType = solidityTypeToNetType param._type
            let getter = fun [this] -> Expr.FieldGet(this, field)
            let setter = fun [this; v] -> Expr.FieldSet(this, field, v)
            let property = ProvidedProperty(propertyName, netType, isStatic = false, getterCode = getter, setterCode = setter)

            let attrs: obj [] = 
                match param.indexed with
                | Some indexed -> [|param._type;param.name;index+1;indexed|]
                | None -> [|param._type;param.name;index+1|]

            getAttributeWithParams typeof<ParameterAttribute> attrs
            |> property.AddCustomAttribute

            property


        let makeFunctionMethods name (inputs: Parameter seq) =
            let parametrList = 
                inputs 
                |> Seq.mapi(fun index param -> 
                        let netType = solidityTypeToNetType param._type
                        let parametrName = if param.name |> System.String.IsNullOrWhiteSpace then (sprintf "Prop%i" index) else param.name
                        ProvidedParameter(parametrName, netType)
                    ) 
                |> Seq.toList
            
            let asyncOutput = ProvidedTypeBuilder.MakeGenericType(typedefof<Task<_>>, [ typeof<TransactionReceipt> ])

            let funArgLength = parametrList.Length

            let getFargs (args: Expr list) = 
                if funArgLength > 0 then
                    Expr.NewArrayUnchecked(typeof<obj>, args.Tail.[0..funArgLength - 1] |> List.map(fun e -> Expr.Coerce(e, typeof<obj>)))
                else
                    Expr.NewArrayUnchecked(typeof<obj>, [])


            let method0 = 
                let invokeCode (args: Expr list) :Expr =
                    let fargs = getFargs args
                    let ctr = Expr.FieldGet(args.Head, contractPlug)
                    <@@ 
                        (%%ctr:ContractPlug).ExecuteFunction name (%%fargs: obj[]) (WeiValue 0UL) (%%ctr:ContractPlug).GasLimit (%%ctr:ContractPlug).GasPrice
                    @@>
                let invokeCodeAsync (args: Expr list) :Expr =
                    let fargs = getFargs args
                    let ctr = Expr.FieldGet(args.Head, contractPlug)
                    <@@ 
                        (%%ctr:ContractPlug).ExecuteFunctionAsync name (%%fargs: obj[]) (WeiValue 0UL) (%%ctr:ContractPlug).GasLimit (%%ctr:ContractPlug).GasPrice
                    @@>

                [
                    ProvidedMethod(name, parametrList, typeof<TransactionReceipt>, invokeCode = invokeCode, isStatic = false)
                    ProvidedMethod(name + "Async", parametrList, asyncOutput, invokeCode = invokeCodeAsync, isStatic = false)
                ]

            let method1 = 
                let weiValue = ProvidedParameter("weiValue", typeof<WeiValue>)
                let allParametrs = parametrList @ [weiValue]
                let invokeCode (args: Expr list) :Expr =
                    let fargs = getFargs args
                    let weiValue = args.[funArgLength + 1]
                    let ctr = Expr.FieldGet(args.Head, contractPlug)
                    <@@ 
                        (%%ctr:ContractPlug).ExecuteFunction name (%%fargs: obj[]) (%%weiValue:WeiValue) (%%ctr:ContractPlug).GasLimit (%%ctr:ContractPlug).GasPrice
                    @@>
                let invokeCodeAsync (args: Expr list) :Expr =
                    let fargs = getFargs args
                    let weiValue = args.[funArgLength + 1]
                    let ctr = Expr.FieldGet(args.Head, contractPlug)
                    <@@ 
                        (%%ctr:ContractPlug).ExecuteFunctionAsync name (%%fargs: obj[]) (%%weiValue:WeiValue) (%%ctr:ContractPlug).GasLimit (%%ctr:ContractPlug).GasPrice
                    @@>

                [
                    ProvidedMethod(name, allParametrs, typeof<TransactionReceipt>, invokeCode = invokeCode, isStatic = false)
                    ProvidedMethod(name + "Async", allParametrs, asyncOutput, invokeCode = invokeCodeAsync, isStatic = false)
                ]

            let method2 = 
                let weiValue = ProvidedParameter("weiValue", typeof<WeiValue>)
                let gasLimit = ProvidedParameter("gasLimit", typeof<GasLimit>)
                let gasPrice = ProvidedParameter("gasPrice", typeof<GasPrice>)

                let allParametrs = parametrList @ [weiValue; gasLimit; gasPrice]
                let invokeCode (args: Expr list) :Expr =
                    let fargs = getFargs args
                    let weiValue = args.[funArgLength + 1]
                    let gasLimit = args.[funArgLength + 2]
                    let gasPrice = args.[funArgLength + 3]
                    let ctr = Expr.FieldGet(args.Head, contractPlug)
                    <@@ 
                        (%%ctr:ContractPlug).ExecuteFunction name (%%fargs: obj[]) (%%weiValue:WeiValue) (%%gasLimit:GasLimit) (%%gasPrice:GasPrice)
                    @@>
                let invokeCodeAsync (args: Expr list) :Expr =
                    let fargs = getFargs args
                    let weiValue = args.[funArgLength + 1]
                    let gasLimit = args.[funArgLength + 2]
                    let gasPrice = args.[funArgLength + 3]
                    let ctr = Expr.FieldGet(args.Head, contractPlug)
                    <@@ 
                        (%%ctr:ContractPlug).ExecuteFunctionAsync name (%%fargs: obj[]) (%%weiValue:WeiValue) (%%gasLimit:GasLimit) (%%gasPrice:GasPrice)
                    @@>

                [
                    ProvidedMethod(name, allParametrs, typeof<TransactionReceipt>, invokeCode = invokeCode, isStatic = false)
                    ProvidedMethod(name + "Async", allParametrs, asyncOutput, invokeCode = invokeCodeAsync, isStatic = false)
                ]


            method0 @ method1 @ method2
            

        let makeFunctionQuery name (inputs: Parameter seq) (output: Type) (outIsObject:bool) =
            let parametrList = 
                inputs 
                |> Seq.mapi(fun index param -> 
                        let netType = solidityTypeToNetType param._type
                        let parametrName = if param.name |> System.String.IsNullOrWhiteSpace then (sprintf "Prop%i" index) else param.name
                        ProvidedParameter(parametrName, netType)
                    ) 
                |> Seq.toList

            let invokeCode (args: Expr list) :Expr =
                let fargs = Expr.NewArrayUnchecked(typeof<obj>, args.Tail |> List.map(fun e -> Expr.Coerce(e, typeof<obj>)))
                let ctr = Expr.FieldGet(args.Head, contractPlug)
                let result = 
                    if outIsObject then
                        <@@ 
                            QueryHelper.queryObj (%%ctr:ContractPlug) output name (%%fargs: obj[])
                        @@>
                    else
                        <@@ 
                            QueryHelper.query (%%ctr:ContractPlug) output name (%%fargs: obj[])
                        @@>

                Expr.Coerce(result, output)

            let asyncOutput = ProvidedTypeBuilder.MakeGenericType(typedefof<Task<_>>, [ output ])
            let invokeCodeAsync (args: Expr list) :Expr =
                let fargs = Expr.NewArrayUnchecked(typeof<obj>, args.Tail |> List.map(fun e -> Expr.Coerce(e, typeof<obj>)))
                let ctr = Expr.FieldGet(args.Head, contractPlug)
                let result = 
                    if outIsObject then
                        <@@ 
                            QueryHelper.queryObjAsync (%%ctr:ContractPlug) output name (%%fargs: obj[])
                        @@>
                    else
                        <@@ 
                            QueryHelper.queryAsync (%%ctr:ContractPlug) output name (%%fargs: obj[])
                        @@>

                Expr.Coerce(result, asyncOutput)


            let method = ProvidedMethod(name + "Query", parametrList, output, invokeCode = invokeCode, isStatic = false)

            let methodAsync = ProvidedMethod(name + "QueryAsync", parametrList, asyncOutput, invokeCode = invokeCodeAsync, isStatic = false)
            [method; methodAsync]

        let makeFunctionData name (inputs: Parameter seq) =
            let parametrList = 
                inputs 
                |> Seq.mapi(fun index param -> 
                        let netType = solidityTypeToNetType param._type
                        let parametrName = if param.name |> System.String.IsNullOrWhiteSpace then (sprintf "Prop%i" index) else param.name
                        ProvidedParameter(parametrName, netType)
                    ) 
                |> Seq.toList

            let invokeCode (args: Expr list) :Expr =
                let fargs = Expr.NewArrayUnchecked(typeof<obj>, args.Tail |> List.map(fun e -> Expr.Coerce(e, typeof<obj>)))
                let ctr = Expr.FieldGet(args.Head, contractPlug)
                <@@ 
                    (%%ctr:ContractPlug).FunctionData name (%%fargs: obj[])
                @@>

            ProvidedMethod(name + "Data", parametrList, typeof<string>, invokeCode = invokeCode, isStatic = false)


        let makeDefaultConstructor () =
            let abiString = abis.ToString()

            let ctr1 = 
                let ctrParams = 
                    seq {
                        yield ProvidedParameter("contractAddress",typeof<string>)
                        yield ProvidedParameter("getWeb3",typeof<unit->Web3>)
                        yield! gasArgs
                    } |> Seq.toList
                ProvidedConstructor(parameters = ctrParams, invokeCode = fun args -> 
                    <@@ 
                        %%Expr.FieldSetUnchecked(args.[0], contractPlug, 
                            <@@ ContractPlug((%%args.[2]:unit->Web3), abiString, (%%args.[1]:string), GasLimit (%%args.[3]:uint64), GasPrice(%%args.[4]:uint64)) @@>)
                        () :> obj 
                    @@>) 

            let ctr2 = 
                let ctrParams = 
                    seq {
                        yield ProvidedParameter("contractAddress",typeof<string>)
                        yield ProvidedParameter("web3",typeof<Web3>)
                        yield! gasArgs
                    } |> Seq.toList
                ProvidedConstructor(parameters = ctrParams, invokeCode = fun args -> 
                    <@@ 
                        %%Expr.FieldSetUnchecked(args.[0], contractPlug, 
                            <@@ ContractPlug((%%args.[2]: Web3), abiString, (%%args.[1]:string), GasLimit (%%args.[3]:uint64), GasPrice(%%args.[4]:uint64)) @@>)
                        () :> obj 
                    @@>) 

            [ctr1; ctr2]

        let makeDeployConstructor (byteCode: string) (inputs: Parameter seq) =
            let abiString = abis.ToString()

            let deployArgs = 
                inputs 
                |> Seq.mapi(fun index param -> 
                        let netType = solidityTypeToNetType param._type
                        let parametrName = if param.name |> System.String.IsNullOrWhiteSpace then (sprintf "Prop%i" index) else param.name
                        ProvidedParameter(parametrName, netType)
                    ) 
                |> Seq.toList
            let deployArgsLength = deployArgs.Length

            let ctr1 = 
                let ctrParams = 
                    seq {
                        yield ProvidedParameter("getWeb3",typeof<unit->Web3>)
                        yield! deployArgs
                        yield! gasArgs
                        }
                        |> Seq.toList

                ProvidedConstructor(parameters = ctrParams, invokeCode = fun args -> 
                    let fargs = 
                        if deployArgsLength > 0 then
                            Expr.NewArrayUnchecked(typeof<obj>, args.[2..2 + (deployArgs.Length) - 1] |> List.map(fun e -> Expr.Coerce(e, typeof<obj>)))
                        else
                            Expr.NewArrayUnchecked(typeof<obj>, [])
                    <@@ 
                        %%Expr.FieldSetUnchecked(args.[0], contractPlug, 
                            <@@ ContractPlug((%%args.[1]:unit->Web3), abiString, byteCode, (%%fargs: obj array), GasLimit (%%args.[2 + deployArgsLength]:uint64), GasPrice (%%args.[3 + deployArgsLength]:uint64)) @@>)
                        () :> obj 
                    @@>) 

            let ctr2 = 
                let ctrParams = 
                    seq {
                        yield ProvidedParameter("web3",typeof<Web3>); 
                        yield! deployArgs
                        yield! gasArgs
                        }
                        |> Seq.toList

                ProvidedConstructor(parameters = ctrParams, invokeCode = fun args -> 
                    let fargs = 
                        if deployArgsLength > 0 then
                            Expr.NewArrayUnchecked(typeof<obj>, args.[2..2 + (deployArgs.Length) - 1] |> List.map(fun e -> Expr.Coerce(e, typeof<obj>)))
                        else
                            Expr.NewArrayUnchecked(typeof<obj>, [])
                    <@@ 
                        %%Expr.FieldSetUnchecked(args.[0], contractPlug, 
                            <@@ ContractPlug((%%args.[1]: Web3), abiString, byteCode, (%%fargs: obj array), GasLimit (%%args.[2 + deployArgsLength]:uint64), GasPrice(%%args.[3 + deployArgsLength]:uint64)) @@>)
                        () :> obj 
                    @@>) 

            [ctr1; ctr2]

        let makeType name baseType = 
            let result = ProvidedTypeDefinition(name, Some <| baseType, isErased = false)
            let ctrDefault = ProvidedConstructor(parameters = [], invokeCode = fun _ -> <@@ () @@>)
            result.AddMember ctrDefault
            result

        let createNestedTypes (contractType:ProvidedTypeDefinition) nameSufix (json: JObject) =
            
            match string json.["type"] with
            | "function" -> 
                let dto = Json.deserialize<FunctionJsonDTO> (string json)
                
                let functionType = makeType (sprintf "%s%sFunction" dto.name nameSufix) typeof<FunctionMessage>

                let fields = dto.inputs |> Seq.mapi makeField |> Seq.toList
                let propertes = Seq.mapi2 makeProperty fields dto.inputs |> Seq.toList

                functionType.AddMembers fields
                functionType.AddMembers propertes


                let functionOutputType = makeType (sprintf "%s%sOutputDTO" dto.name nameSufix) typeof<FunctionOutputDTO> 

                let outputFields = dto.outputs |> Seq.mapi makeField |> Seq.toList
                let outputPropertes = Seq.mapi2 makeProperty outputFields dto.outputs |> Seq.toList
                
                functionOutputType.AddMembers outputFields
                functionOutputType.AddMembers outputPropertes

                functionOutputType.AddCustomAttribute (getAttribute typeof<FunctionOutputAttribute>)

                match dto.outputs with
                | [||] -> 
                    functionType.AddCustomAttribute (getAttributeWithParams typeof<FunctionAttribute> [|dto.name|])
                | [|param|] -> 
                    let querys = makeFunctionQuery (sprintf "%s%s" dto.name nameSufix) dto.inputs (solidityTypeToNetType param._type) false
                    contractType.AddMembers querys

                    functionType.AddCustomAttribute (getAttributeWithParams typeof<FunctionAttribute> [|dto.name; param._type|])
                | _ -> 
                    let querys = makeFunctionQuery (sprintf "%s%s" dto.name nameSufix) dto.inputs functionOutputType true
                    contractType.AddMembers querys

                    functionType.AddCustomAttribute (getAttributeWithParams typeof<FunctionAttribute> [|dto.name; functionOutputType|])

                let methods = makeFunctionMethods (sprintf "%s%s" dto.name nameSufix) dto.inputs
                contractType.AddMembers methods

                let functionData = makeFunctionData (sprintf "%s%s" dto.name nameSufix) dto.inputs
                contractType.AddMember functionData

                contractType.AddMembers [functionOutputType; functionType]
            | "event" -> 
                let dto = Json.deserialize<EventJsonDTO> (string json) 
                let eventType =  makeType (sprintf "%sEventDTO" dto.name) typeof<EventDTO>

                let fields = dto.inputs |> Seq.mapi makeField |> Seq.toList
                let propertes = Seq.mapi2 makeProperty fields dto.inputs |> Seq.toList
                
                eventType.AddMembers fields
                eventType.AddMembers propertes

                eventType.AddCustomAttribute (getAttributeWithParams typeof<EventAttribute> [|dto.name|])

                contractType.AddMember eventType
            | "constructor" ->
                let dto = Json.deserialize<ConstructorJsonDTO> (string json) 
                let constructorType =  makeType (sprintf "%sDeployment" contractName) typeof<obj>

                let fields = dto.inputs |> Seq.mapi makeField |> Seq.toList
                let propertes = Seq.mapi2 makeProperty fields dto.inputs |> Seq.toList

                constructorType.AddMembers fields
                constructorType.AddMembers propertes

                match byteCode with
                | Some byteCode ->
                    let construtors = makeDeployConstructor byteCode dto.inputs
                    contractType.AddMembers construtors
                | _ -> ()

                contractType.AddMember constructorType
            | _ -> ()

        let contractType = ProvidedTypeDefinition(sprintf "%sContract" contractName, Some typeof<obj>, isErased = false, hideObjectMethods = true)

        abis.Children<JObject>() 
        |> Seq.groupBy(fun json -> 
                let _type = string json.["type"]
                let _name = 
                    match json.TryGetValue "name" with
                    | true, v -> string v
                    | _ -> "Noname"
                _type + _name
        )
        |> Seq.iter(fun (_,group) ->
            match group |> Seq.toList with
            | [] -> ()
            | [json] -> createNestedTypes contractType "" json
            | headJson::tailJson -> 
                createNestedTypes contractType  "" headJson
                tailJson |> List.iteri(fun i json -> createNestedTypes contractType  (sprintf "%i" (i + 1)) json)
                )

        if contractType.GetConstructors().Length = 0 then
            match byteCode with
            | Some byteCode ->
                let construtors = makeDeployConstructor byteCode []
                contractType.AddMembers construtors
            | _ -> ()

        contractType.AddMember contractPlug

        let defaultConstructors = makeDefaultConstructor()
        contractType.AddMembers defaultConstructors

        contractType.AddMember <| ProvidedProperty(propertyName = "ContractPlug", propertyType = typeof<ContractPlug>, getterCode = fun args -> Expr.FieldGet(args.[0], contractPlug))

        let addressGetter = fun (args: Expr list) -> <@@ (%%Expr.FieldGet(args.[0], contractPlug): ContractPlug).Contract.Address @@>
        contractType.AddMember <| ProvidedProperty(propertyName = "Address", propertyType = typeof<string>, getterCode = addressGetter)
        contractType.AddMember <| ProvidedProperty(propertyName = "FromFile", propertyType = typeof<string>, isStatic = true, getterCode = fun _ -> <@@ fileName @@>)
        contractType




    let contractTypes = 
        Directory.EnumerateFiles(buildPath, "*.json") 
        |> Seq.map(fun fileName -> 
            let json = File.ReadAllText fileName
            let parsedJson = JObject.Parse(json)
            //printfn "parsedJson: %A" parsedJson
            let abis = parsedJson.["abi"]
            let contractName = parsedJson.["contractName"].ToString()
            let byteCode = 
                match parsedJson.TryGetValue("bytecode") with
                | true, token -> Some (token.ToString())
                | _ -> 
                    printfn "bytecode not found"
                    None
            printfn "contractName: %A" contractName

            (fileName, contractName, abis, byteCode))
        |> Seq.map createType
        |> Seq.toList
        |> List.fold (fun contractTypes contractType -> contractType::contractTypes) []

    let asm = ProvidedAssembly()
    let rootType = ProvidedTypeDefinition(asm, ns, typeName, Some typeof<obj>, isErased = false)
    rootType.AddMember <| ProvidedProperty(propertyName = "FromFolder", propertyType = typeof<string>, isStatic = true, getterCode = fun _ -> <@@ buildPath @@>)
    rootType.AddMembers contractTypes
    asm.AddTypes [rootType]
    rootType


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

let constructRootTypeByTruffle (ns:string) (cfg:TypeProviderConfig) (typeName:string) (paramValues: obj[]) =
    let truffleConfigFile = paramValues.[0] :?> string
    let resolutionFolder = paramValues.[1] :?> string
    
    printfn "file: %s dir: %s cfg: %s" truffleConfigFile resolutionFolder cfg.ResolutionFolder

    
    let buildPath = 
        let configFile = resolvePath truffleConfigFile resolutionFolder cfg
        let configFolder = Path.GetDirectoryName configFile
        
        let buildFolder = Path.Combine(configFolder, "build/contracts")
        Directory.GetFiles(buildFolder, "*.json") |> Seq.iter File.Delete

        let procInfo = ProcessStartInfo("npx")
        procInfo.Arguments <- "truffle build"
        procInfo.RedirectStandardOutput <- true
        procInfo.WorkingDirectory <- configFolder
        let proc = Process.Start(procInfo)
        proc.WaitForExit()
        let output = proc.StandardOutput.ReadToEnd()
        if proc.ExitCode <> 0 then failwith (sprintf "configFile: %s configDir: %s Error compiling: %s" configFile configFolder output)
        
        let m = Regex.Match(output, @"Artifacts written to (.+)\n")
        if not m.Success then failwith (sprintf "configFile: %s configDir: %s Error compiling output: %s" configFile configFolder output)
            
        m.Groups.[1].Value

    constructRootType ns typeName buildPath

[<TypeProvider>]
type SolidityProvider (config:TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces (config, assemblyReplacementMap=[("SolidityProvider.DesignTime", "SolidityProvider.Runtime")])

    let ns = "SolidityProviderNS"
    let asm = Assembly.GetExecutingAssembly()

    let staticParams = [
        ProvidedStaticParameter("ContractsFolderPath", typeof<string>)
        ProvidedStaticParameter("ResolutionFolder", typeof<string>, parameterDefaultValue = "")
    ]
    let staticParams4Trufle = [
        ProvidedStaticParameter("TrufleConfigFile", typeof<string>)
        ProvidedStaticParameter("ResolutionFolder", typeof<string>, parameterDefaultValue = "")
    ]

    // check we contain a copy of runtime files, and are not referencing the runtime DLL
    do assert (typeof<DataSource>.Assembly.GetName().Name = asm.GetName().Name)

    let typesByFolder = ProvidedTypeDefinition(asm, ns, "SolidityTypes", Some typeof<obj>, isErased = false)

    do typesByFolder.DefineStaticParameters(staticParams, constructRootTypeByFolder ns config)
    
    let typesByTruffle = ProvidedTypeDefinition(asm, ns, "SolidityTypesFromTruffle", Some typeof<obj>, isErased = false)
    
    do typesByTruffle.DefineStaticParameters(staticParams, constructRootTypeByTruffle ns config)
    
    do this.AddNamespace(ns, [typesByFolder; typesByTruffle])