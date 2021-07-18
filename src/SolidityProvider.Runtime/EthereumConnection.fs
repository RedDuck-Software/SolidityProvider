namespace SolidityProviderNamespace

open Nethereum.Web3
open Nethereum.Hex.HexTypes
open System.Numerics
open Nethereum.RPC.Eth.DTOs


type EthereumConnection(web3: Web3) =
    let bigInt (value: uint64) = BigInteger(value)
    let hexBigInt (value: uint64) = HexBigInteger(bigInt value)
    
    member val Gas = hexBigInt 9500000UL with get, set
    member val GasPrice = hexBigInt 8000000000UL with get, set
    member this.Account with get() = web3.TransactionManager.Account
    member this.Web3 with get() = web3

    member this.SendTxAsync toAddress (value:BigInteger) data = 
        let input: TransactionInput =
            TransactionInput(
                data, 
                toAddress, 
                this.Account.Address, 
                this.Gas,
                this.GasPrice, 
                HexBigInteger(value))
        this.Web3.Eth.TransactionManager.SendTransactionAndWaitForReceiptAsync(input, null)
