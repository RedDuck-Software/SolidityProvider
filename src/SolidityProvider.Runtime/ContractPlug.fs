namespace SolidityProviderNamespace

open System.Numerics
open Nethereum.Web3
open Nethereum.Hex.HexTypes
open Nethereum.RPC.Eth.DTOs
open System.Threading.Tasks

[<AutoOpenAttribute>]
module misc =
    let inline runNow task =
        task
        |> Async.AwaitTask
        |> Async.RunSynchronously

    let inline bigInt (value: uint64) = BigInteger(value)
    let inline hexBigInt (value: uint64) = HexBigInteger(bigInt value)

type ContractPlug(getWeb3: unit->Web3, abi: string, address: string, gas: uint64, gasPrice: uint64) =

    new(web3: Web3, abi: string, address: string, gas: uint64, gasPrice: uint64) = 
        ContractPlug((fun ()-> web3), abi, address, gas, gasPrice)

    new(getWeb3:unit->Web3, abi: string, bytecode:string, constructorArguments: obj array, gas: uint64, gasPrice: uint64) = 
        let transaction = 
            getWeb3().Eth.DeployContract.SendRequestAndWaitForReceiptAsync(
                abi,
                bytecode,
                getWeb3().TransactionManager.Account.Address,
                hexBigInt(gas),
                hexBigInt(gasPrice),
                hexBigInt 0UL,
                null,
                constructorArguments) |> runNow
        
        ContractPlug(getWeb3, abi, transaction.ContractAddress, gas, gasPrice)

    new(web3: Web3, abi: string, bytecode:string, constructorArguments: obj array, gas: uint64, gasPrice: uint64) = 
        ContractPlug((fun ()-> web3), abi, bytecode, constructorArguments, gas, gasPrice)

    member val Gas = hexBigInt(gas) with get
    member val GasPrice = hexBigInt(gasPrice) with get

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

    member this.ExecuteFunctionAsyncWithValueFrom value functionName arguments = 
        let data = this.FunctionData functionName arguments 
        this.SendTxAsync value data 

    member this.ExecuteFunctionAsyncWithValue value functionName arguments = 
        this.ExecuteFunctionAsyncWithValueFrom value functionName arguments

    member this.ExecuteFunctionAsync functionName arguments = 
        this.ExecuteFunctionAsyncWithValue (BigInteger(0)) functionName arguments

    member this.ExecuteFunction functionName arguments = 
        this.ExecuteFunctionAsync functionName arguments |> runNow

