module Tests

open Xunit
open Nethereum.RPC
open Nethereum.JsonRpc
open System.IO
open Newtonsoft.Json
open Newtonsoft.Json.Linq

[<Literal>]
let path = @"C:\Users\user\Git\dEth\smart-contracts\dETH2\build\contracts"



[<Fact>]
let ``My test`` () =
    //let eth = EthApiService(null)
    //let abi = Abi(@"C:\Users\Ilyas\source\repos\ConsoleApp1\Contracts\dEth.json")
    //printfn "%A" abi.AbiString
    //let contract = Nethereum.Contracts.Contract(eth, abi.AbiString, "")
    //printfn "Ok" 
    //let deth = A.dETHContract(eth, abi.AbiString, "")
    Assert.True(true)
