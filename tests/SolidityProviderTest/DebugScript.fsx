#r @"..\..\src\SolidityProvider.Runtime\bin\Debug\typeproviders\fsharp41\netcoreapp3.1\Nethereum.ABI.dll"
#r @"..\..\src\SolidityProvider.Runtime\bin\Debug\typeproviders\fsharp41\netcoreapp3.1\Nethereum.Contracts.dll"
#r @"..\..\src\SolidityProvider.Runtime\bin\Debug\typeproviders\fsharp41\netcoreapp3.1\Nethereum.RPC.dll"
#r @"..\..\src\SolidityProvider.Runtime\bin\Debug\typeproviders\fsharp41\netcoreapp3.1\Nethereum.HEX.dll"
#r @"..\..\src\SolidityProvider.Runtime\bin\Debug\typeproviders\fsharp41\netcoreapp3.1\Nethereum.JsonRpc.Client.dll"
#r @"..\..\src\SolidityProvider.Runtime\bin\Debug\typeproviders\fsharp41\netcoreapp3.1\Nethereum.Web3.dll"
#r @"..\..\src\SolidityProvider.Runtime\bin\Debug\typeproviders\fsharp41\netcoreapp3.1\Nethereum.Accounts.dll"
#r @"..\..\src\SolidityProvider.Runtime\bin\Debug\typeproviders\fsharp41\netcoreapp3.1\Nethereum.Signer.dll"

#r @"..\..\src\SolidityProvider.Runtime\bin\Debug\typeproviders\fsharp41\netcoreapp3.1\Newtonsoft.JSON.dll"
#r @"..\..\src\SolidityProvider.Runtime\bin\Debug\typeproviders\fsharp41\netcoreapp3.1\Common.Logging.Core.dll"
#r @"..\..\src\SolidityProvider.Runtime\bin\Debug\typeproviders\fsharp41\netcoreapp3.1\BouncyCastle.Crypto.dll"
#r @"..\..\src\SolidityProvider.Runtime\bin\Debug\netcoreapp3.1\SolidityProvider.Runtime.dll"

open Nethereum.ABI.FunctionEncoding.Attributes
open Nethereum.RPC
open Nethereum.Web3
open Nethereum.Web3.Accounts
open Nethereum.Hex.HexTypes
open System.Threading.Tasks
open System.IO
open Newtonsoft.Json
open Newtonsoft.Json.Linq
open System.Numerics

let inline runNow task =
    task
    |> Async.AwaitTask
    |> Async.RunSynchronously

type Abi(filename) =
    member val JsonString = File.OpenText(filename).ReadToEnd()
    member this.AbiString = JsonConvert.DeserializeObject<JObject>(this.JsonString).GetValue("abi").ToString()
    member this.Bytecode = JsonConvert.DeserializeObject<JObject>(this.JsonString).GetValue("bytecode").ToString()

type A = SolidityProviderNS.SolidityTypes< @"C:\Users\Ilyas\source\repos\ConsoleApp1\Contracts">

let hardhatPrivKey = "ac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80"
let hardhatURI = "http://192.168.122.1:8545"

let abi = Abi(@"C:\Users\Ilyas\source\repos\ConsoleApp1\Contracts\Oracle.json")

abi.Bytecode

let web3 = Web3(Account(hardhatPrivKey), hardhatURI)

let inline bigInt (value: uint64) = BigInteger(value)
let inline hexBigInt (value: uint64) = HexBigInteger(bigInt value)

let makerOracleMainnet = "0x729D19f657BD0614b4985Cf1D82531c67569197B"
let daiUsdMainnet = "0xAed0c38402a5d19df6E4c03F4E2DceD6e29c1ee9"
let ethUsdMainnet = "0x5f4eC3Df9cbd43714FE2740f5E3616155c5b8419"

// let r = 
//     web3.Eth.DeployContract.SendRequestAndWaitForReceiptAsync(
//         abi.AbiString,
//         abi.Bytecode,
//         web3.TransactionManager.Account.Address,
//         hexBigInt(9500000UL), hexBigInt(8000000000UL),
//         hexBigInt 0UL,
//         null,
//         [|makerOracleMainnet :> obj; daiUsdMainnet :> obj; ethUsdMainnet :> obj|]
//     )

//let debug = A.DebugContract("321", (fun () -> web3))

//let oracleContractMainnet = A.OracleContract((fun () -> web3), makerOracleMainnet, daiUsdMainnet, ethUsdMainnet)

let lottery = A.LotteryContract((fun () -> web3))

let qo = lottery.ContractPlug.Query<bigint> "random" [| BigInteger 12354; uint32 45 |]
printfn "%A" (qo)
let qo2 = lottery.randomQueryAsync(BigInteger 12354, uint32 45) |> runNow
printfn "%A" (qo2)


// let lottery = A.LotteryContract("0", web3)
// lottery.players(BigInteger 123)
// lottery.random(BigInteger 123, uint32 45)


//let allow = dEth.allowance("1", "2")

// let event = A.dETHContract.TransferEventDTO()
// let output = A.dETHContract.allowanceOutputDTO()


// let c = dEth.Contract
// printfn "%A" c.Address

// let abi = Abi(@"C:\Users\Ilyas\source\repos\ConsoleApp1\Contracts\dEth.json")
// let contract = web3.Eth.GetContract(abi.AbiString, hardhatURI)



// let f = contract.GetFunction("allowance")

// web3.Eth.GetContractHandler("").SendRequestAndWaitForReceiptAsync()
// let task = f.CallDeserializingToObjectAsync<obj>([| "321":>obj; "456":>obj |])
// task.Result
// contract.Address
// let debug = A.DebugContract(EthApiService(null), "http://address")
// debug.

//let dEth = A.dETHContract()

//printfn "A.FromFolder: %s" A.FromFolder
//printfn "A.dETHContract.FromFile: %s" A.dETHContract.FromFile

//let allowance = A.dEthContract.allowanceFunction()
//let allowanceOut = A.dETHContract.allowanceOutputDTO()
//printfn "before change: %A" allowanceOut.Prop0
//allowanceOut.Prop0 <- System.Numerics.BigInteger 1
//printfn "after change: %A" allowanceOut.Prop0

//let approval = A.dETHContract.ApprovalEventDTO()

//let settings = A.dETHContract.AutomationSettingsChangedEventDTO()
//let deployment = A.dETHContract.dETHDeployment()

// let fe = A.DebugContract.ForwardedEventDTO()
// printfn "%A" fe._success

// [| typeof<A> |]
// |> Array.map (fun t ->
//     printfn "--- %s --- %A" t.Name (t.GetCustomAttributes(true).Length)
// )


// [| typeof<A.dETHContract> |]
// |> Array.map (fun t ->
//         printfn "--- %s --- %A" t.Name (t.GetCustomAttributes(true).Length)
//         //t.GetCustomAttributes(false)
//         //|> Array.map (fun attr -> (attr :?> FunctionAttribute).Name |> printfn "%A")

//     )

// let t = A.dETHContract.allowanceOutputDTO()

// [| typeof<A.dETHContract.allowanceOutputDTO> |]
// |> Array.map (fun t ->
//         printfn "--- %s --- %A" t.Name (t.GetCustomAttributes(true).Length)
//         //t.GetCustomAttributes(false)
//         //|> Array.map (fun attr -> (attr :?> EventAttribute).Name |> printfn "%A")
//     )
    
//    EventAttribute

