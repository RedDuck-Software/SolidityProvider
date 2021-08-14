namespace AbiTypeProvider

open System.Numerics
open Nethereum.Web3
open Nethereum.Hex.HexTypes
open Nethereum.RPC.Eth.DTOs
open System
open AbiTypeProvider.Common

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

    member this.QueryObjAsync<'a when 'a: (new: unit -> 'a)> functionName arguments = 
        (this.Function functionName).CallDeserializingToObjectAsync<'a> (arguments)

    member this.QueryObj<'a when 'a: (new: unit -> 'a)> functionName arguments = 
        this.QueryObjAsync<'a> functionName arguments |> runNow

    member this.QueryAsync<'a> functionName arguments = 
        (this.Function functionName).CallAsync<'a> (arguments)

    member this.Query<'a> functionName arguments = 
        this.QueryAsync<'a> functionName arguments |> runNow

    member this.FunctionData functionName arguments = 
        (this.Function functionName).GetData(arguments)

    member this.ExecuteFunctionAsyncWithValueFrom functionName arguments (weiValue:WeiValue) (gasLimit:GasLimit) (gasPrice:GasPrice) = 
        let data = this.FunctionData functionName arguments 
        this.SendTxAsync data weiValue gasLimit gasPrice

    member this.ExecuteFunctionAsyncWithValue functionName arguments (weiValue:WeiValue) (gasLimit:GasLimit) (gasPrice:GasPrice) = 
        this.ExecuteFunctionAsyncWithValueFrom functionName arguments weiValue gasLimit gasPrice

    member this.ExecuteFunctionAsync functionName arguments (weiValue:WeiValue) (gasLimit:GasLimit) (gasPrice:GasPrice) = 
        this.ExecuteFunctionAsyncWithValue functionName arguments weiValue gasLimit gasPrice 

    member this.ExecuteFunction functionName arguments (weiValue:WeiValue) (gasLimit:GasLimit) (gasPrice:GasPrice) = 
        this.ExecuteFunctionAsync functionName arguments weiValue gasLimit gasPrice |> runNow



type MethodHelper() = 
    static member ExecuteFunctionMethod0Async (args: obj[]) = 
        let functionName = args.[0] :?> string
        let argsLength = args.[1] :?> int
        let this = args.[2]
        let contract = this.GetType().GetProperty("ContractPlug").GetValue(this) :?> ContractPlug
        let fargs = 
            if argsLength > 0 then
                args.[3..3 + argsLength - 1]
            else
                [||]
        contract.ExecuteFunctionAsync functionName fargs (WeiValue 0UL) contract.GasLimit contract.GasPrice

    static member ExecuteFunctionMethod0 (args: obj[]) = 
        MethodHelper.ExecuteFunctionMethod0Async args |> runNow

    static member ExecuteFunctionMethod1Async (args: obj[]) = 
        let functionName = args.[0] :?> string
        let argsLength = args.[1] :?> int
        let this = args.[2]
        let contract = this.GetType().GetProperty("ContractPlug").GetValue(this) :?> ContractPlug
        let fargs = 
            if argsLength > 0 then
                args.[3..3 + argsLength - 1]
            else
                [||]
        let weiValue = args.[3 + argsLength] :?> WeiValue
        contract.ExecuteFunctionAsync functionName fargs weiValue contract.GasLimit contract.GasPrice

    static member ExecuteFunctionMethod1 (args: obj[]) = 
        MethodHelper.ExecuteFunctionMethod1Async args |> runNow

    static member ExecuteFunctionMethod2Async (args: obj[]) = 
        let functionName = args.[0] :?> string
        let argsLength = args.[1] :?> int
        let this = args.[2]
        let contract = this.GetType().GetProperty("ContractPlug").GetValue(this) :?> ContractPlug
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
        printfn "weiValue: %A; gasLimit: %A; gasPrice: %A" weiValue gasLimit gasPrice
        contract.ExecuteFunctionAsync functionName fargs weiValue gasLimit gasPrice

    static member ExecuteFunctionMethod2 (args: obj[]) = 
        MethodHelper.ExecuteFunctionMethod2Async args |> runNow

type QueryHelperGeneric<'a> =
    static member QueryAsync (plug:ContractPlug, functionName, arguments) =
        plug.QueryAsync<'a> functionName arguments

        //let r = plug.QueryAsync<'a> functionName arguments |> runNow
        
        //System.Threading.Tasks.Task<'a>.Factory.StartNew(fun () -> r )

    static member Query (plug:ContractPlug, functionName, arguments) =
        plug.QueryAsync<'a> functionName arguments |> runNow

type QueryObjHelperGeneric<'a when 'a: (new: unit -> 'a)> =
    static member QueryObjAsync (plug:ContractPlug, functionName, arguments) =
        plug.QueryObjAsync<'a> functionName arguments

    static member QueryObj (plug:ContractPlug, functionName, arguments) =
        plug.QueryObjAsync<'a> functionName arguments |> runNow

type QueryHelper() =

    static member queryAsync (args: obj[]) =
        let functionName = args.[0] :?> string
        let outType = args.[1] :?> Type
        let argsLength = args.[2] :?> int
        let this = args.[3]
        let contract = this.GetType().GetProperty("ContractPlug").GetValue(this) :?> ContractPlug
        let fargs = 
            if argsLength > 0 then
                args.[4..4 + argsLength - 1]
            else
                [||]
        
        let queryHelper = typedefof<QueryHelperGeneric<_>>.MakeGenericType(outType)
        let method = queryHelper.GetMethod("QueryAsync")
        let args = [|
            (contract:> obj)
            (functionName:> obj)
            (fargs:> obj)
        |]
        method.Invoke(null, args)

    static member query (args: obj[]) =
        let functionName = args.[0] :?> string
        let outType = args.[1] :?> Type
        let argsLength = args.[2] :?> int
        let this = args.[3]
        let contract = this.GetType().GetProperty("ContractPlug").GetValue(this) :?> ContractPlug
        let fargs = 
            if argsLength > 0 then
                args.[4..4 + argsLength - 1]
            else
                [||]

        let queryHelper = typedefof<QueryHelperGeneric<_>>.MakeGenericType(outType)
        let method = queryHelper.GetMethod("Query")
        let args = [|
            (contract:> obj)
            (functionName:> obj)
            (fargs:> obj)
        |]
        method.Invoke(null, args)

    static member queryObjAsync (args: obj[]) =
        let functionName = args.[0] :?> string
        let outType = args.[1] :?> Type
        let argsLength = args.[2] :?> int
        let this = args.[3]
        let contract = this.GetType().GetProperty("ContractPlug").GetValue(this) :?> ContractPlug
        let fargs = 
            if argsLength > 0 then
                args.[4..4 + argsLength - 1]
            else
                [||]

        let queryHelper = typedefof<QueryObjHelperGeneric<_>>.MakeGenericType(outType)
        let method = queryHelper.GetMethod("QueryObjAsync")
        let args = [|
            (contract:> obj)
            (functionName:> obj)
            (fargs:> obj)
        |]
        method.Invoke(null, args)

    static member  queryObj (args: obj[]) =
        let functionName = args.[0] :?> string
        let outType = args.[1] :?> Type
        let argsLength = args.[2] :?> int
        let this = args.[3]
        let contract = this.GetType().GetProperty("ContractPlug").GetValue(this) :?> ContractPlug
        let fargs = 
            if argsLength > 0 then
                args.[4..4 + argsLength - 1]
            else
                [||]

        let queryHelper = typedefof<QueryObjHelperGeneric<_>>.MakeGenericType(outType)
        let method = queryHelper.GetMethod("QueryObj")
        let args = [|
            (contract:> obj)
            (functionName:> obj)
            (fargs:> obj)
        |]
        method.Invoke(null, args)

type FunctionDataHelper() = 
    static member FunctionData (args: obj[]) = 
        let functionName = args.[0] :?> string
        let argsLength = args.[1] :?> int
        let this = args.[2]
        let contract = this.GetType().GetProperty("ContractPlug").GetValue(this) :?> ContractPlug
        let fargs = 
            if argsLength > 0 then
                args.[3..3 + argsLength - 1]
            else
                [||]
        contract.FunctionData functionName fargs

module QueryHelperOld =

    type QueryHelper<'a> =
        static member QueryAsync (plug:ContractPlug) functionName arguments =
            plug.QueryAsync<'a> functionName arguments

        static member Query (plug:ContractPlug) functionName arguments =
            plug.QueryAsync<'a> functionName arguments |> runNow

    type QueryObjHelper<'a when 'a: (new: unit -> 'a)> =
        static member QueryObjAsync (plug:ContractPlug) functionName arguments =
            plug.QueryObjAsync<'a> functionName arguments

        static member QueryObj (plug:ContractPlug) functionName arguments =
            plug.QueryObjAsync<'a> functionName arguments |> runNow


    let queryAsync (plug:ContractPlug) (outType: Type) functionName arguments =
        let queryHelper = typedefof<QueryHelper<_>>.MakeGenericType(outType)
        let method = queryHelper.GetMethod("QueryAsync")
        let args = [|
            (plug:> obj)
            (functionName:> obj)
            (arguments:> obj)
        |]
        method.Invoke(null, args)

    let query (plug:ContractPlug) (outType: 'a) functionName arguments =
        let queryHelper = typedefof<QueryHelper<_>>.MakeGenericType(outType)
        let method = queryHelper.GetMethod("Query")
        let args = [|
            (plug:> obj)
            (functionName:> obj)
            (arguments:> obj)
        |]
        method.Invoke(null, args)

    let queryObjAsync (plug:ContractPlug) (outType: 'a) functionName arguments =
        let queryHelper = typedefof<QueryObjHelper<_>>.MakeGenericType(outType)
        let method = queryHelper.GetMethod("QueryObjAsync")
        let args = [|
            (plug:> obj)
            (functionName:> obj)
            (arguments:> obj)
        |]
        method.Invoke(null, args)

    let queryObj (plug:ContractPlug) (outType: 'a) functionName arguments =
        let queryHelper = typedefof<QueryObjHelper<_>>.MakeGenericType(outType)
        let method = queryHelper.GetMethod("QueryObj")
        let args = [|
            (plug:> obj)
            (functionName:> obj)
            (arguments:> obj)
        |]
        method.Invoke(null, args)