module Program 

open System.IO
open Nethereum.JsonRpc
open System.IO
open Newtonsoft.Json
open Newtonsoft.Json.Linq
open Nethereum.RPC

type A = AbiTypeProvider.AbiTypes< @"C:\Users\Ilyas\source\repos\ConsoleApp1\Contracts">
    
let [<EntryPoint>] main _ = 
    
    //let eth = EthApiService(null)
    //let deth = A.dETHContract(eth, "12341234")
    //let f = deth.allowance
    //printfn "%A" f

    //System.Console.ReadLine() |> ignore
    printfn "%A" A.FromFolder

    0

