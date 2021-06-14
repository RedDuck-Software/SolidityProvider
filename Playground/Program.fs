// Learn more about F# at http://fsharp.org

open Newtonsoft.Json.Linq
open System.IO
open FSharp.Json

(*
type Address = string

type Parameter = {
    internalType:string;
    name:string;
    [<JsonField("type")>]
    _type:string;
}

// fallbacks - they don't have input, and name .. 
// events - they dont have output, payable
type Root = {
    constant: bool option; // absent for constructor
    inputs: Parameter array option; // absent for fallbacks
    name: string option; // absent for constructor and fallbacks
    outputs: Parameter array option; // absent for constructor
    payable: bool option; // absent for events
    stateMutability: string option; // absent for events
    [<JsonField("type")>]
    _type: string;
}
*)
type Address = string

type Parameter = {
    internalType:string;
    name:string;
    [<JsonField("type")>]
    _type:string;
}

type Root = {
    constant: bool;
    inputs: Parameter array;
    name: string;
    outputs: Parameter array;
    payable: bool;
    stateMutability: string;
    [<JsonField("type")>]
    _type: string;
}

[<EntryPoint>]
let main argv =
    let json = File.ReadAllText @"dEth.json"
    let parsedJson = JObject.Parse(json)
    printfn "parsedJson: %A" parsedJson
    let abis = parsedJson.["abi"]
    let abiJson = abis.Children<JObject>() 
                    |> Seq.where (fun i -> string i.["type"] = "function") 
                    |> JArray |> string
    printfn "abiJson: %A" abiJson
    let contractName = parsedJson.["contractName"].ToString()

    let roots = Json.deserialize<Root array> abiJson

    printfn "Hello World from F#!"
    0 // return an integer exit code
