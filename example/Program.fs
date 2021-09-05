open Nethereum.Web3
open Nethereum.Web3.Accounts
open AbiTypeProvider.Common

// Build contract types from json files
type Contracts = AbiTypeProvider.AbiTypes<"./build/contracts">

let hardhatPrivKey = "hardhatPrivKey" //set your hardhatPrivKey
let hardhatURI = "http://127.0.0.1:8545"

let storageContractAddress = "0x1613beb3b2c4f22ee086b2b38c1476a3ce7f78e8"

[<EntryPoint>]
let main argv =
    let web3 = Web3(Account(hardhatPrivKey), hardhatURI)

    // If you create of the contract without an address, then the contract will be automatically deloyed
    //let storageContract = Contracts.SimpleStorageContract(web3)

    // Create the contract with address
    let storageContract = Contracts.SimpleStorageContract(storageContractAddress, web3)
    printfn $"storageContract.Address: {storageContract.Address}" 

    // Execute get function and recive result
    let getResult = storageContract.getQuery()
    printfn $"getResult: {getResult}"

    printfn "Increase value"
    let newValue = getResult + (bigint 1UL)
    // Execute set function with required argument newValue and optional arguments weiValue and gasLlimit
    let transactionRecipient = storageContract.set(newValue, weiValue 0UL, gasLlimit 8500000UL)

    printfn $"transactionRecipient.Status {transactionRecipient.Status}"
    printfn $"transactionRecipient.GasUsed {transactionRecipient.GasUsed}"

    let getResult = storageContract.getQuery()
    printfn $"getResult: {getResult}"

    0 // return an integer exit code