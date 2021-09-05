[![NuGet version](https://badge.fury.io/nu/AbiTypeProvider.svg)](https://badge.fury.io/nu/AbiTypeProvider)

# AbiTypeProvider: Type providers for Ethereum smart contracts

The AbiTypeProvider implements interaction with smart contacts of the Ethereum. The provider allows to deploy contracts, execute the methods of the contracts, decode the results and events.

Remote calls, encoding and decoding are done using the library [Nethereum](https://github.com/Nethereum/Nethereum)

Contract types are generated from json files obtained with [Truffle](https://github.com/trufflesuite/truffle)

One area of application is testing smart contracts using dotnet tests tools

### Example
Example work with [SimpleStorage.sol](example/contracts/SimpleStorage.sol)

```fsharp
// Build contract types from json files
type Contracts = AbiTypeProvider.AbiTypes<"./build/contracts">

//Create and deploy contract
let storageContract = Contracts.SimpleStorageContract(web3)
//Execute get method
let getResult = storageContract.getQuery()
//Execute set method
storageContract.set(newValue, weiValue 0UL, gasLlimit 8500000UL)

```
Example project available in [examples](/example) folder


### Building provider:

    dotnet tool restore
    dotnet paket update
    dotnet build -c release

    dotnet paket pack nuget --interproject-references fix --version 0.0.2