namespace AbiTypeProvider

open System.Numerics
open Nethereum.Web3
open Nethereum.ABI.FunctionEncoding
open Nethereum.RPC.Eth.DTOs
open System
open AbiTypeProvider.Common
open System.Collections.Generic
open Nethereum.Contracts

// Base type for contract types
type ContractPlug(getWeb3: unit->Web3, abi: string, address: string, gasLimit: GasLimit, gasPrice: GasPrice) =

    new(web3: Web3, abi: string, address: string, gasLimit: GasLimit, gasPrice: GasPrice) = 
        ContractPlug((fun ()-> web3), abi, address, gasLimit, gasPrice)

    new(getWeb3:unit->Web3, abi: string, bytecode:string, constructorArguments: obj array, gasLimit: GasLimit, gasPrice: GasPrice) = 
        let transaction = 
            getWeb3().Eth.DeployContract.SendRequestAndWaitForReceiptAsync(
                abi,
                bytecode,
                getWeb3().TransactionManager.Account.Address,
                gasLimit.Value,
                gasPrice.Value,
                hexBigInt 0UL,
                null,
                constructorArguments) |> runNow
        
        ContractPlug(getWeb3, abi, transaction.ContractAddress, gasLimit, gasPrice)

    new(web3: Web3, abi: string, bytecode:string, constructorArguments: obj array, gas: GasLimit, gasPrice: GasPrice) = 
        ContractPlug((fun ()-> web3), abi, bytecode, constructorArguments, gas, gasPrice)

    member val GasLimit = gasLimit with get
    member val GasPrice = gasPrice with get

    member this.Account with get() = getWeb3().TransactionManager.Account
    member this.Contract with get() = getWeb3().Eth.GetContract(abi, address)
    member this.Web3 with get() = getWeb3()

    member this.SendTxAsync data (weiValue:WeiValue) (gasLimit:GasLimit) (gasPrice:GasPrice) = 
        let input: TransactionInput =
            TransactionInput(
                data, 
                address, 
                this.Account.Address, 
                gasLimit.Value,
                gasPrice.Value, 
                weiValue.Value)
        this.Web3.Eth.TransactionManager.SendTransactionAndWaitForReceiptAsync(input, null)

    member this.Function functionName = 
        this.Contract.GetFunction(functionName)

    member this.FunctionData functionName arguments = 
        (this.Function functionName).GetData(arguments)

    member this.FunctionTransactionInput functionName arguments (weiValue:WeiValue) (gasLimit:GasLimit) (gasPrice:GasPrice) = 
        let ti = (this.Function functionName).CreateTransactionInput(this.Account.Address, arguments)
        ti.Gas <- gasLimit.Value
        ti.GasPrice <- gasPrice.Value
        ti.Value <- weiValue.Value
        ti

    member this.ExecuteFunctionAsyncWithValueFrom functionName arguments (weiValue:WeiValue) (gasLimit:GasLimit) (gasPrice:GasPrice) = 
        let data = this.FunctionData functionName arguments 
        this.SendTxAsync data weiValue gasLimit gasPrice

    member this.ExecuteFunctionAsyncWithValue functionName arguments (weiValue:WeiValue) (gasLimit:GasLimit) (gasPrice:GasPrice) = 
        this.ExecuteFunctionAsyncWithValueFrom functionName arguments weiValue gasLimit gasPrice

    member this.ExecuteFunctionAsync functionName arguments (weiValue:WeiValue) (gasLimit:GasLimit) (gasPrice:GasPrice) = 
        this.ExecuteFunctionAsyncWithValue functionName arguments weiValue gasLimit gasPrice 

    member this.ExecuteFunction functionName arguments (weiValue:WeiValue) (gasLimit:GasLimit) (gasPrice:GasPrice) = 
        this.ExecuteFunctionAsync functionName arguments weiValue gasLimit gasPrice |> runNow

/// A type that stores property values
type ContainerTypeFields() =
    // All values are stored in this dictionary
    // The dictionary key is built from the instance id and the unique property key
    static let storage = Dictionary<string, obj>()
    static let objectIdGen = System.Runtime.Serialization.ObjectIDGenerator()

    static member getValue (instance:obj, defualValue: obj, key: obj) = 
        let key = key :?> string
        let id,_ = objectIdGen.GetId(instance)
        let fullKey = sprintf "%d_%s" id key
        if storage.ContainsKey(fullKey) |> not then
            storage.Add(fullKey, defualValue)
        storage.[fullKey]

    static member getValueBigInteger (instance:obj, key: obj) = 
        let key = key :?> string
        let id,_ = objectIdGen.GetId(instance)
        let fullKey = sprintf "%d_%s" id key
        if storage.ContainsKey(fullKey) |> not then
            storage.Add(fullKey, BigInteger(456))
        storage.[fullKey]

    static member setValue (instance:obj, key: obj, v: obj) = 
        let key = key:?> string
        let id,_ = objectIdGen.GetId(instance)
        let fullKey = sprintf "%d_%s" id key
        if storage.ContainsKey(fullKey) then
            storage.[fullKey] <- v
        else
            storage.Add(fullKey, v)


/// The type contains methods for performing functions on the ethereum
type MethodHelper() = 
    static member ExecuteFunctionMethodAsync (args: obj[]) = 
        let functionName = args.[0] :?> string
        let argsLength = args.[1] :?> int
        let contract = args.[2] :?> ContractPlug
        let fargs = 
            if argsLength > 0 then
                args.[3..3 + argsLength - 1]
            else
                [||]

        let weiValue = 
            if args.[3 + argsLength] = null then
                WeiValue 0UL
            else
                args.[3 + argsLength] :?> WeiValue
        let gasLimit = 
            if args.[4 + argsLength] = null then
                contract.GasLimit
            else
                args.[4 + argsLength] :?> GasLimit
        let gasPrice = 
            if args.[5 + argsLength] = null then
                contract.GasPrice
            else
                args.[5 + argsLength] :?> GasPrice
        contract.ExecuteFunctionAsync functionName fargs weiValue gasLimit gasPrice

    static member ExecuteFunctionMethod (args: obj[]) = 
        MethodHelper.ExecuteFunctionMethodAsync args |> runNow

    static member FunctionTransactionInput (args: obj[]) = 
        let functionName = args.[0] :?> string
        let argsLength = args.[1] :?> int
        let contract = args.[2] :?> ContractPlug
        let fargs = 
            if argsLength > 0 then
                args.[3..3 + argsLength - 1]
            else
                [||]

        let weiValue = 
            if args.[3 + argsLength] = null then
                WeiValue 0UL
            else
                args.[3 + argsLength] :?> WeiValue
        let gasLimit = 
            if args.[4 + argsLength] = null then
                contract.GasLimit
            else
                args.[4 + argsLength] :?> GasLimit
        let gasPrice = 
            if args.[5 + argsLength] = null then
                contract.GasPrice
            else
                args.[5 + argsLength] :?> GasPrice
        contract.FunctionTransactionInput functionName fargs weiValue gasLimit gasPrice

/// The type contains methods for performing functions on the ethereum and decoding result to simple types
type QueryHelperGeneric<'a> =
    static member QueryAsync (plug:ContractPlug, functionName, json: string, arguments: obj []) =
        let functionABI = 
            let abiDeserialiser = Nethereum.ABI.JsonDeserialisation.ABIDeserialiser()
            let convertor = Nethereum.ABI.JsonDeserialisation.ExpandoObjectConverter()
            let json = Newtonsoft.Json.JsonConvert.DeserializeObject<IDictionary<string, obj>>(json, convertor)
            abiDeserialiser.BuildFunction json 

        let ethFunction = plug.Function functionName
        
        let callInput = ethFunction.CreateCallInput(arguments)
        let parametrOutput = 
            functionABI.OutputParameters 
            |> Array.map(fun p -> 
                p.DecodedType <- solidityTypeToNetType p.Type
                ParameterOutput(Parameter = p))
        async {
            let! outputBytes = ethFunction.CallRawAsync(callInput) |> Async.AwaitTask
            let decoded = ParameterDecoder().DecodeOutput(outputBytes, parametrOutput)
            return 
                decoded 
                |> Seq.map(fun d -> d.Result)
                |> Seq.cast<'a>
                |> Seq.head
        } |> Async.StartAsTask

    static member Query (plug:ContractPlug, functionName, json: string, arguments) =
        QueryHelperGeneric<'a>.QueryAsync(plug, functionName, json, arguments) |> runNow

/// The type contains methods for performing functions on the ethereum and decoding result to FunctionOutput types
type QueryObjHelperGeneric<'a when 'a: (new: unit -> 'a)> =
    static member QueryAsync (plug:ContractPlug, functionName, json: string, keyList: string [], arguments) =
        let functionABI = 
            let abiDeserialiser = Nethereum.ABI.JsonDeserialisation.ABIDeserialiser()
            let convertor = Nethereum.ABI.JsonDeserialisation.ExpandoObjectConverter()
            let json = Newtonsoft.Json.JsonConvert.DeserializeObject<IDictionary<string, obj>>(json, convertor)
            abiDeserialiser.BuildFunction json 

        let ethFunction = plug.Function functionName
        
        let callInput = ethFunction.CreateCallInput(arguments)
        let parametrOutput = 
            functionABI.OutputParameters 
            |> Array.map(fun p -> 
                p.DecodedType <- solidityTypeToNetType p.Type
                ParameterOutput(Parameter = p))
        async {
            let! outputBytes = ethFunction.CallRawAsync(callInput) |> Async.AwaitTask
            let decoded = ParameterDecoder().DecodeOutput(outputBytes, parametrOutput)
            let r = Activator.CreateInstance(typeof<'a>) :?> 'a
            Seq.iter2(fun k (p:ParameterOutput) -> ContainerTypeFields.setValue(r, k, p.Result) ) keyList decoded
            return r
        } |> Async.StartAsTask

    static member Query (plug:ContractPlug, functionName, json: string, keyList: string [], arguments) =
        QueryObjHelperGeneric<'a>.QueryAsync(plug, functionName, json, keyList, arguments) |> runNow

/// The type contains methods for decoding TransactionReceipt to Event types
type EventHelperGeneric<'a when 'a: (new: unit -> 'a)> =
    static member DecodeAllEvents (json: string, keyList: string [], receipt: TransactionReceipt) =
        let makeInstance (paramList: ParameterOutput seq) = 
            let topicParams = paramList |> Seq.where(fun p -> p.Parameter.Indexed) |> Seq.sortBy(fun p-> p.Parameter.Order) |> Seq.toList
            let dataParams = paramList |> Seq.where(fun p -> not p.Parameter.Indexed) |> Seq.sortBy(fun p-> p.Parameter.Order) |> Seq.toList

            let r = Activator.CreateInstance(typeof<'a>) :?> 'a

            Seq.iter2(fun k (p:ParameterOutput) -> ContainerTypeFields.setValue(r, k, p.Result) ) keyList (topicParams @ dataParams)
            r

        let eventABI = 
            let abiDeserialiser = Nethereum.ABI.JsonDeserialisation.ABIDeserialiser()
            let convertor = Nethereum.ABI.JsonDeserialisation.ExpandoObjectConverter()
            let json = Newtonsoft.Json.JsonConvert.DeserializeObject<IDictionary<string, obj>>(json, convertor)
            abiDeserialiser.BuildEvent json

        eventABI.DecodeAllEventsDefaultTopics(receipt.Logs)
        |> Seq.map(fun e -> e.Event)
        |> Seq.map makeInstance 
        |> Seq.toArray

/// The type contains method for generate FunctionData
type FunctionDataHelper() = 
    static member FunctionData (args: obj[]) = 
        let functionName = args.[0] :?> string
        let argsLength = args.[1] :?> int
        let contract = args.[2] :?> ContractPlug
        let fargs = 
            if argsLength > 0 then
                args.[3..3 + argsLength - 1]
            else
                [||]
        contract.FunctionData functionName fargs
