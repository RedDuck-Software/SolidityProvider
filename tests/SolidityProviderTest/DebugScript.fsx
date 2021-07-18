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
open System.Threading.Tasks
open System.IO
open Newtonsoft.Json
open Newtonsoft.Json.Linq
open System.Numerics

type Abi(filename) =
    member val JsonString = File.OpenText(filename).ReadToEnd()
    member this.AbiString = JsonConvert.DeserializeObject<JObject>(this.JsonString).GetValue("abi").ToString()
    member this.Bytecode = JsonConvert.DeserializeObject<JObject>(this.JsonString).GetValue("bytecode").ToString()

type A = SolidityProviderNS.SolidityTypes< @"C:\Users\Ilyas\source\repos\ConsoleApp1\Contracts">

let hardhatPrivKey = "ac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80"
let hardhatURI = "http://192.168.122.1:8545"

let abi = Abi(@"C:\Users\Ilyas\source\repos\ConsoleApp1\Contracts\dEth.json")

let web3 = Web3(Account(hardhatPrivKey), hardhatURI)

let dEth = A.dETHContract("0", web3)

dEth.allowanceTest("1", "2")

let lottery = A.LotteryContract("0", web3)
lottery.playersTest(BigInteger 123)
lottery.randomTest(BigInteger 123, uint32 45)
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

