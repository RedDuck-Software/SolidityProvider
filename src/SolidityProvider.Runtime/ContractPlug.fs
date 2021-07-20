namespace SolidityProviderNamespace

open System.Numerics
open Nethereum.Web3
open Nethereum.Hex.HexTypes
open Nethereum.RPC.Eth.DTOs

[<AutoOpenAttribute>]
module task =

    let inline runNow task =
        task
        |> Async.AwaitTask
        |> Async.RunSynchronously


type ContractPlug(getWeb3: unit->Web3, abi: string, address: string) =
    let bigInt (value: uint64) = BigInteger(value)
    let hexBigInt (value: uint64) = HexBigInteger(bigInt value)

    new(web3: Web3, abi: string, address: string) = ContractPlug((fun ()-> web3), abi, address)

    member val Gas = hexBigInt 9500000UL with get, set
    member val GasPrice = hexBigInt 8000000000UL with get, set
    member this.Account with get() = getWeb3().TransactionManager.Account
    member this.Contract with get() = getWeb3().Eth.GetContract(abi, address)
    member this.Web3 with get() = getWeb3()

    member this.SendTxAsync (value:BigInteger) data = 
        let input: TransactionInput =
            TransactionInput(
                data, 
                address, 
                this.Account.Address, 
                this.Gas,
                this.GasPrice, 
                HexBigInteger(value))
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

    member this.ExecuteFunctionAsyncWithValue value functionName arguments = 
        this.FunctionData functionName arguments |> this.SendTxAsync value 

    member this.ExecuteFunctionAsync functionName arguments = 
        this.ExecuteFunctionAsyncWithValue (BigInteger(0)) functionName arguments

    member this.ExecuteFunction functionName arguments = 
        this.ExecuteFunctionAsync functionName arguments |> runNow

