module ConstructRootType


open System.Reflection
open System.IO
open System
open FSharp.Json
open Nethereum.ABI.FunctionEncoding.Attributes
open Newtonsoft.Json.Linq
open ProviderImplementation.ProvidedTypes
open ProviderImplementation.ProvidedTypes.UncheckedQuotations
open AbiTypeProvider
open Nethereum.Contracts
open Microsoft.FSharp.Quotations
open System.Collections.Generic
open System.Threading.Tasks
open Nethereum.RPC.Eth.DTOs
open Nethereum.Web3
open System.Numerics
open AbiTypeProvider.Common
open Nethereum.ABI.Model


let constructRootType (asm:Assembly) (ns:string) (typeName:string) (buildPath: string) = 
    let createType (fileName: string, contractName, abis:JToken, byteCode: string option) =
        
        let gasArgs = [
            ProvidedParameter("gas", typeof<uint64>, optionalValue = uint64 9500000UL)
            ProvidedParameter("gasPrice", typeof<uint64>, optionalValue = uint64 8000000000UL)
        ]

        let getPropertyName index (param:Nethereum.ABI.Model.Parameter) = 
            if param.Name |> System.String.IsNullOrWhiteSpace then (sprintf "Prop%i" index) else param.Name

        let ContainerTypeFieldsGetValue = typeof<ContainerTypeFields>.GetMethod("getValue")
        let ContainerTypeFieldsGetValueBigInteger = typeof<ContainerTypeFields>.GetMethod("getValueBigInteger")
        let ContainerTypeFieldsSetValue = typeof<ContainerTypeFields>.GetMethod("setValue")
        let makeProperty index (param: Nethereum.ABI.Model.Parameter) =
            let getDefaultValue(t:Type) =
                if t.IsValueType then
                    Activator.CreateInstance(t)
                else
                    null
            let propertyName = getPropertyName index param
            let netType = solidityTypeToNetType param.Type
            
            let key = Guid.NewGuid().ToString()
            
            let getter = 
                if netType = typeof<BigInteger> then
                    fun (args: Expr list) -> Expr.Call(ContainerTypeFieldsGetValueBigInteger, [ Expr.Coerce(args.[0], typeof<obj>); Expr.Value key ])
                else
                    let defaulValue = getDefaultValue netType
                    fun (args: Expr list) -> Expr.Call(ContainerTypeFieldsGetValue, [ Expr.Coerce(args.[0], typeof<obj>); Expr.Value defaulValue; Expr.Value key ])
            let setter (args: Expr list) :Expr = Expr.Call(ContainerTypeFieldsSetValue, [ Expr.Coerce(args.[0], typeof<obj>); Expr.Value key; Expr.Coerce(args.[1], typeof<obj>) ])

            let property = ProvidedProperty(propertyName, netType, isStatic = false, getterCode = getter, setterCode = setter)
            
            key, property

        let ExecuteFunctionMethod = typeof<MethodHelper>.GetMethod("ExecuteFunctionMethod")
        let ExecuteFunctionMethodAsync = typeof<MethodHelper>.GetMethod("ExecuteFunctionMethodAsync")
        let makeFunctionMethods name (inputs: Nethereum.ABI.Model.Parameter seq) =
            let parametrList = 
                inputs 
                |> Seq.mapi(fun index param -> 
                        let netType = solidityTypeToNetType param.Type
                        let parametrName = if param.Name |> System.String.IsNullOrWhiteSpace then (sprintf "Prop%i" index) else param.Name
                        ProvidedParameter(parametrName, netType)
                    ) 
                |> Seq.toList
            
            let asyncOutput = ProvidedTypeBuilder.MakeGenericType(typedefof<Task<_>>, [ typeof<TransactionReceipt> ])

            let funArgLength = parametrList.Length

            let getAllArgs (args: Expr list) = 
                    Expr.NewArray(typeof<obj>, Expr.Value name :: Expr.Value funArgLength :: args |> List.map(fun e -> Expr.Coerce(e, typeof<obj>)))

            let weiValue = ProvidedParameter("weiValue", typeof<WeiValue>, optionalValue = None)
            let gasLimit = ProvidedParameter("gasLimit", typeof<GasLimit>, optionalValue = None)
            let gasPrice = ProvidedParameter("gasPrice", typeof<GasPrice>, optionalValue = None)

            let allParametrs = parametrList @ [weiValue; gasLimit; gasPrice]
            let invokeCode (args: Expr list) :Expr = Expr.Call(ExecuteFunctionMethod, [getAllArgs args])
            let invokeCodeAsync (args: Expr list) :Expr =  Expr.Call(ExecuteFunctionMethodAsync, [getAllArgs args])

            [
                ProvidedMethod(name, allParametrs, typeof<TransactionReceipt>, invokeCode = invokeCode, isStatic = false)
                ProvidedMethod(name + "Async", allParametrs, asyncOutput, invokeCode = invokeCodeAsync, isStatic = false)
            ]


        let FunctionTransactionInput = typeof<MethodHelper>.GetMethod("FunctionTransactionInput")
        let makeFunctionTransactionInput name (inputs: Nethereum.ABI.Model.Parameter seq) =
            let parametrList = 
                inputs 
                |> Seq.mapi(fun index param -> 
                        let netType = solidityTypeToNetType param.Type
                        let parametrName = if param.Name |> System.String.IsNullOrWhiteSpace then (sprintf "Prop%i" index) else param.Name
                        ProvidedParameter(parametrName, netType)
                    ) 
                |> Seq.toList
    
            let funArgLength = parametrList.Length

            let getAllArgs (args: Expr list) = 
                    Expr.NewArray(typeof<obj>, Expr.Value name :: Expr.Value funArgLength :: args |> List.map(fun e -> Expr.Coerce(e, typeof<obj>)))

            let weiValue = ProvidedParameter("weiValue", typeof<WeiValue>, optionalValue = None)
            let gasLimit = ProvidedParameter("gasLimit", typeof<GasLimit>, optionalValue = None)
            let gasPrice = ProvidedParameter("gasPrice", typeof<GasPrice>, optionalValue = None)

            let allParametrs = parametrList @ [weiValue; gasLimit; gasPrice]
            let invokeCode (args: Expr list) :Expr = Expr.Call(FunctionTransactionInput, [getAllArgs args])

            ProvidedMethod(name + "TransactionInput", allParametrs, typeof<TransactionInput>, invokeCode = invokeCode, isStatic = false)

            
        let makeFunctionQuery name (inputs: Nethereum.ABI.Model.Parameter seq) (output: Type) (json: string) =
            let parametrList = 
                inputs 
                |> Seq.mapi(fun index param -> 
                        let netType = solidityTypeToNetType param.Type
                        let parametrName = if param.Name |> System.String.IsNullOrWhiteSpace then (sprintf "Prop%i" index) else param.Name
                        ProvidedParameter(parametrName, netType)
                    ) 
                |> Seq.toList

            let queryHelperGeneric = ProvidedTypeBuilder.MakeGenericType(typedefof<QueryHelperGeneric<_>>, [output])
            let QueryHelperQuery = queryHelperGeneric.GetMethod("Query")
            let QueryHelperQueryAsync = queryHelperGeneric.GetMethod("QueryAsync")
            
            let invokeCode (args: Expr list) :Expr = 
                let fargs = Expr.NewArray(typeof<obj>, args.Tail |> List.map(fun e -> Expr.Coerce(e, typeof<obj>)))
                let result = Expr.Call(QueryHelperQuery, [args.Head; Expr.Value name; Expr.Value json; fargs])
                Expr.Coerce(result, output)

            let asyncOutput = ProvidedTypeBuilder.MakeGenericType(typedefof<Task<_>>, [ output ])
            let invokeCodeAsync (args: Expr list) :Expr =
                let fargs = Expr.NewArray(typeof<obj>, args.Tail |> List.map(fun e -> Expr.Coerce(e, typeof<obj>)))
                let result = Expr.Call(QueryHelperQueryAsync, [args.Head; Expr.Value name; Expr.Value json; fargs])
                Expr.Coerce(result, asyncOutput)

            [
                ProvidedMethod(name + "Query", parametrList, output, invokeCode = invokeCode, isStatic = false)
                ProvidedMethod(name + "QueryAsync", parametrList, asyncOutput, invokeCode = invokeCodeAsync, isStatic = false)
            ]


        let makeFunctionQueryObj name (inputs: Nethereum.ABI.Model.Parameter seq) (output: Type) (keyList: string []) (json: string) =
            let parametrList = 
                inputs 
                |> Seq.mapi(fun index param -> 
                        let netType = solidityTypeToNetType param.Type
                        let parametrName = if param.Name |> System.String.IsNullOrWhiteSpace then (sprintf "Prop%i" index) else param.Name
                        ProvidedParameter(parametrName, netType)
                    ) 
                |> Seq.toList

            let queryHelperGeneric = ProvidedTypeBuilder.MakeGenericType(typedefof<QueryObjHelperGeneric<_>>, [output])
            let QueryHelperQuery = queryHelperGeneric.GetMethod("Query")
            let QueryHelperQueryAsync = queryHelperGeneric.GetMethod("QueryAsync")
            
            let invokeCode (args: Expr list) :Expr = 
                let fargs = Expr.NewArray(typeof<obj>, args.Tail |> List.map(fun e -> Expr.Coerce(e, typeof<obj>)))
                let result = Expr.Call(QueryHelperQuery, [args.Head; Expr.Value name; Expr.Value json; Expr.Value keyList; fargs])
                Expr.Coerce(result, output)

            let asyncOutput = ProvidedTypeBuilder.MakeGenericType(typedefof<Task<_>>, [ output ])
            let invokeCodeAsync (args: Expr list) :Expr =
                let fargs = Expr.NewArray(typeof<obj>, args.Tail |> List.map(fun e -> Expr.Coerce(e, typeof<obj>)))
                let result = Expr.Call(QueryHelperQueryAsync, [args.Head; Expr.Value name; Expr.Value json; Expr.Value keyList; fargs])
                Expr.Coerce(result, asyncOutput)


            [
                ProvidedMethod(name + "Query", parametrList, output, invokeCode = invokeCode, isStatic = false)
                ProvidedMethod(name + "QueryAsync", parametrList, asyncOutput, invokeCode = invokeCodeAsync, isStatic = false)
            ]

        let makeDecodeEvent (output: Type) (keyList: string []) (json: string) =
            let parametrList = [ProvidedParameter("transactionReceipt", typeof<TransactionReceipt>)]

            let eventHelperGeneric = ProvidedTypeBuilder.MakeGenericType(typedefof<EventHelperGeneric<_>>, [output])
            let DecodeAllEvents = eventHelperGeneric.GetMethod("DecodeAllEvents")
            
            let outArrayType = output.MakeArrayType()
            let invokeCode (args: Expr list) :Expr = 
                Expr.Call(DecodeAllEvents, [Expr.Value json; Expr.Value keyList] @ args)

            ProvidedMethod("DecodeAllEvents", parametrList, outArrayType, invokeCode = invokeCode, isStatic = true)

        let FunctionDataHelper = typeof<FunctionDataHelper>.GetMethod("FunctionData")
        let makeFunctionData name (inputs: Nethereum.ABI.Model.Parameter seq) =
            let parametrList = 
                inputs 
                |> Seq.mapi(fun index param -> 
                        let netType = solidityTypeToNetType param.Type
                        let parametrName = if param.Name |> System.String.IsNullOrWhiteSpace then (sprintf "Prop%i" index) else param.Name
                        ProvidedParameter(parametrName, netType)
                    ) 
                |> Seq.toList

            let invokeCode (args: Expr list) :Expr =
                let funArgLength = parametrList.Length

                let allArgs = Expr.NewArray(typeof<obj>, Expr.Value name :: Expr.Value funArgLength :: args |> List.map(fun e -> Expr.Coerce(e, typeof<obj>)))
                Expr.Call(FunctionDataHelper, [allArgs])

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
                        ContractPlug((%%args.[1]:unit->Web3), abiString, (%%args.[0]:string), GasLimit (%%args.[2]:uint64), GasPrice(%%args.[3]:uint64))
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
                        ContractPlug((%%args.[1]: Web3), abiString, (%%args.[0]:string), GasLimit (%%args.[2]:uint64), GasPrice(%%args.[3]:uint64))
                    @@>) 

            [ctr1; ctr2]

        let makeDeployConstructor (byteCode: string) (inputs: Nethereum.ABI.Model.Parameter seq) =
            let abiString = abis.ToString()

            let deployArgs = 
                inputs 
                |> Seq.mapi(fun index param -> 
                        let netType = solidityTypeToNetType param.Type
                        let parametrName = if param.Name |> System.String.IsNullOrWhiteSpace then (sprintf "Prop%i" index) else param.Name
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
                            Expr.NewArrayUnchecked(typeof<obj>, args.[1..1 + (deployArgs.Length) - 1] |> List.map(fun e -> Expr.Coerce(e, typeof<obj>)))
                        else
                            Expr.NewArrayUnchecked(typeof<obj>, [])
                    <@@ 
                        ContractPlug((%%args.[0]:unit->Web3), abiString, byteCode, (%%fargs: obj array), GasLimit (%%args.[1 + deployArgsLength]:uint64), GasPrice (%%args.[2 + deployArgsLength]:uint64))
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
                        ContractPlug((%%args.[0]:Web3), abiString, byteCode, (%%fargs: obj array), GasLimit (%%args.[1 + deployArgsLength]:uint64), GasPrice (%%args.[2 + deployArgsLength]:uint64))
                    @@>) 

            [ctr1; ctr2]

        let makeType name baseType = 
            let result = ProvidedTypeDefinition(name, Some <| baseType, isErased = true)
            let ctr = baseType.GetConstructor([||])

            let ctrDefault = ProvidedConstructor(parameters = [], invokeCode = fun _ -> Expr.NewObject(ctr, []))
            result.AddMember ctrDefault
            result

        let makeContractFunction (contractType:ProvidedTypeDefinition) nameSufix (json:string) =
            let functionABI = 
                let abiDeserialiser = Nethereum.ABI.JsonDeserialisation.ABIDeserialiser()
                let convertor = Nethereum.ABI.JsonDeserialisation.ExpandoObjectConverter()
                let json = Newtonsoft.Json.JsonConvert.DeserializeObject<IDictionary<string, obj>>(json, convertor)
                abiDeserialiser.BuildFunction json 

            let functionOutputType = makeType (sprintf "%s%sOutputDTO" functionABI.Name nameSufix) typeof<FunctionOutputDTO> 
            let outParamsSorted = (functionABI.OutputParameters |> Array.sortBy(fun p -> p.Order))

            let outputPropertes = Array.mapi makeProperty outParamsSorted
            let keyList = outputPropertes |> Array.map fst 

            functionOutputType.AddMembers (outputPropertes |> Seq.map snd |> Seq.toList)
             
            contractType.AddMember functionOutputType

            match functionABI.OutputParameters with
            | [||] -> ()
            | [|param|] -> 
                let querys = makeFunctionQuery (sprintf "%s%s" functionABI.Name nameSufix) functionABI.InputParameters (solidityTypeToNetType param.Type) json
                contractType.AddMembers querys
            | _ -> 
                let querys = makeFunctionQueryObj (sprintf "%s%s" functionABI.Name nameSufix) functionABI.InputParameters functionOutputType keyList json
                contractType.AddMembers querys

            let methods = makeFunctionMethods (sprintf "%s%s" functionABI.Name nameSufix) functionABI.InputParameters
            contractType.AddMembers methods

            let functionData = makeFunctionData (sprintf "%s%s" functionABI.Name nameSufix) functionABI.InputParameters
            contractType.AddMember functionData

            let transactionInput = makeFunctionTransactionInput (sprintf "%s%s" functionABI.Name nameSufix) functionABI.InputParameters

            contractType.AddMember transactionInput
            ()

        let makeContractEvent (contractType:ProvidedTypeDefinition) (json: string) =
            let abiDeserialiser = Nethereum.ABI.JsonDeserialisation.ABIDeserialiser()
            let convertor = Nethereum.ABI.JsonDeserialisation.ExpandoObjectConverter()
            
            let eventABI = 
                let json = Newtonsoft.Json.JsonConvert.DeserializeObject<IDictionary<string, obj>>(json, convertor)
                abiDeserialiser.BuildEvent json 
            let eventType =  makeType (sprintf "%sEventDTO" eventABI.Name) typeof<EventDTO>

            let topicProperties = eventABI.InputParameters |> Seq.where(fun p -> p.Indexed) |> Seq.sortBy(fun p -> p.Order) |> Seq.toList
            let dataProperties = eventABI.InputParameters |> Seq.where(fun p -> not p.Indexed) |> Seq.sortBy(fun p -> p.Order) |> Seq.toList

            let propertes = Seq.mapi makeProperty (topicProperties @ dataProperties) |> Seq.toList
            eventType.AddMembers (propertes |> List.map snd)
            
            let keyList = propertes |> Seq.map fst |> Seq.toArray

            eventType.AddMember <| makeDecodeEvent eventType keyList json

            contractType.AddMember eventType
            ()

        let makeContractConstructor (contractType:ProvidedTypeDefinition) (constructorABI:ConstructorABI) =
            
            let constructorType =  makeType (sprintf "%sDeployment" contractName) typeof<obj>

            let propertes = Seq.mapi makeProperty constructorABI.InputParameters |> Seq.toList |> List.map snd

            constructorType.AddMembers propertes

            match byteCode with
            | Some byteCode ->
                let construtors = makeDeployConstructor byteCode constructorABI.InputParameters
                contractType.AddMembers construtors
            | _ -> ()

            contractType.AddMember constructorType
            ()

        let contractType = ProvidedTypeDefinition(sprintf "%sContract" contractName, Some typeof<ContractPlug>, isErased = true, hideObjectMethods = true)

        

        let functions = List<JObject>()
        let events = List<string>()

        for element in abis.Children<JObject>() do
            match string element.["type"] with
            | "constructor" -> 
                let abiDeserialiser = Nethereum.ABI.JsonDeserialisation.ABIDeserialiser()
                let convertor = Nethereum.ABI.JsonDeserialisation.ExpandoObjectConverter()
                let json = Newtonsoft.Json.JsonConvert.DeserializeObject<IDictionary<string, obj>>(element.ToString(), convertor)
                
                makeContractConstructor contractType (abiDeserialiser.BuildConstructor json)
            | "function" ->
                functions.Add element
            | "event" -> 
                events.Add (element.ToString())
            | _ -> ()

        events |> Seq.iter(makeContractEvent contractType)

        functions 
        |> Seq.groupBy(fun (json) -> 
            match json.TryGetValue "name" with
            | true, v -> string v
            | _ -> "Noname"
        )
        |> Seq.iter(fun (_, group) -> 
            match group |> Seq.toList with
            | [] -> ()
            | [json] -> makeContractFunction contractType "" (string json)
            | json::tail -> 
                makeContractFunction contractType "" (string json)
                tail |> List.iteri(fun i json -> makeContractFunction contractType (sprintf "%i" (i + 1)) (string json))

        )

        if contractType.GetConstructors().Length = 0 then
            match byteCode with
            | Some byteCode ->
                let construtors = makeDeployConstructor byteCode []
                contractType.AddMembers construtors
            | _ -> ()

        let defaultConstructors = makeDefaultConstructor()
        contractType.AddMembers defaultConstructors

        let addressGetter = fun (args: Expr list) -> <@@ (%%args.[0]: ContractPlug).Contract.Address @@>
        contractType.AddMember <| ProvidedProperty(propertyName = "Address", propertyType = typeof<string>, getterCode = addressGetter)
        contractType.AddMember <| ProvidedProperty(propertyName = "FromFile", propertyType = typeof<string>, isStatic = true, getterCode = fun _ -> <@@ fileName @@>)

        //contractType.GetMembers() 
        //|> Seq.groupBy(fun m -> m.MemberType)
        //|> Seq.iter(fun (mt, ms) -> 
        //    printfn "%A: %d" mt (ms |> Seq.length)
        //)
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

    let rootType = ProvidedTypeDefinition(asm, ns, typeName, Some typeof<obj>, isErased = true)
    rootType.AddMember <| ProvidedProperty(propertyName = "FromFolder", propertyType = typeof<string>, isStatic = true, getterCode = fun _ -> <@@ buildPath @@>)
    rootType.AddMembers contractTypes
    rootType
