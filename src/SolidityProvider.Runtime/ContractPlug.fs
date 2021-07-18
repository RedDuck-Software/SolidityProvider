namespace SolidityProviderNamespace

open System.Numerics

[<AutoOpenAttribute>]
module task =

    let inline runNow task =
        task
        |> Async.AwaitTask
        |> Async.RunSynchronously


type ContractPlug(ethConn: EthereumConnection, abi: string, address: string) =
    member val public EthConn = ethConn
    
    member val public Address = address

    member val public Contract = 
        ethConn.Web3.Eth.GetContract(abi, address)

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

    member this.ExecuteFunctionFromAsyncWithValue value functionName arguments (connection:EthereumConnection) = 
        this.FunctionData functionName arguments |> connection.SendTxAsync this.Address value 

    member this.ExecuteFunctionFromAsync functionName arguments connection = 
        this.ExecuteFunctionFromAsyncWithValue (BigInteger(0)) functionName arguments connection

    member this.ExecuteFunctionFrom functionName arguments connection = 
        this.ExecuteFunctionFromAsync functionName arguments connection |> runNow

    member this.ExecuteFunctionAsync functionName arguments = 
        this.ExecuteFunctionFromAsync functionName arguments this.EthConn

    member this.ExecuteFunction functionName arguments = 
        this.ExecuteFunctionAsync functionName arguments |> runNow