module Program 

open System.IO
open Nethereum.JsonRpc
open System.IO
open Newtonsoft.Json
open Newtonsoft.Json.Linq
open Nethereum.RPC

type Abi(filename) =
    member val JsonString = File.OpenText(filename).ReadToEnd()
    member this.AbiString = JsonConvert.DeserializeObject<JObject>(this.JsonString).GetValue("abi").ToString()
    member this.Bytecode = JsonConvert.DeserializeObject<JObject>(this.JsonString).GetValue("bytecode").ToString()

//type A = SolidityProviderNS.SolidityTypes< @"C:\Users\Ilyas\source\repos\ConsoleApp1\Contracts">
    
let [<EntryPoint>] main _ = 
    //let eth = EthApiService(null)
    //let deth = A.dETHContract(eth, "12341234")
    //let f = deth.allowance
    //printfn "%A" f


    //System.Console.ReadLine() |> ignore
    printfn "Ok" 

    0

