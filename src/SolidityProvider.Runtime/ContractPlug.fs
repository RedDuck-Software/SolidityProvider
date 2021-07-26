namespace SolidityProviderNamespace

open System.Numerics
open Nethereum.Web3
open Nethereum.Hex.HexTypes
open Nethereum.RPC.Eth.DTOs
open System

[<AutoOpenAttribute>]
module misc =
    let inline runNow task =
        task
        |> Async.AwaitTask
        |> Async.RunSynchronously

    let inline bigInt (value: uint64) = BigInteger(value)
    let inline hexBigInt (value: uint64) = HexBigInteger(bigInt value)

type GasLimit(v: HexBigInteger) = 
    member this.Value = v
    new (v:BigInteger) = GasLimit(HexBigInteger v)
    new (v:uint64) = GasLimit(bigint v)
    with override this.ToString() = v.ToString()
type GasPrice(v: HexBigInteger) = 
    member this.Value = v
    new (v:BigInteger) = GasPrice(HexBigInteger v)
    new (v:uint64) = GasPrice(bigint v)
    with override this.ToString() = v.ToString()
type WeiValue(v: HexBigInteger) = 
    member this.Value = v
    new (v:BigInteger) = WeiValue(HexBigInteger v)
    new (v:uint64) = WeiValue(bigint v)
    with override this.ToString() = v.ToString()

type gasLlimit = GasLimit
type gasPrice = GasPrice
type weiValue = WeiValue

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

module QueryHelper =

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
