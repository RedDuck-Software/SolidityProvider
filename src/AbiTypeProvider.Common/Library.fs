namespace AbiTypeProvider.Common

open System.Numerics
open Nethereum.Hex.HexTypes

[<AutoOpenAttribute>]
module misc =
    let inline runNow task =
        task
        |> Async.AwaitTask
        |> Async.RunSynchronously

    let inline bigInt (value: uint64) = BigInteger(value)
    let inline hexBigInt (value: uint64) = HexBigInteger(bigInt value)

type GasLimit(v: HexBigInteger) = 
    member this.Value = v
    new (v:BigInteger) = GasLimit(HexBigInteger v)
    new (v:uint64) = GasLimit(bigint v)
    with override this.ToString() = v.ToString()
type GasPrice(v: HexBigInteger) = 
    member this.Value = v
    new (v:BigInteger) = GasPrice(HexBigInteger v)
    new (v:uint64) = GasPrice(bigint v)
    with override this.ToString() = v.ToString()
type WeiValue(v: HexBigInteger) = 
    member this.Value = v
    new (v:BigInteger) = WeiValue(HexBigInteger v)
    new (v:uint64) = WeiValue(bigint v)
    with override this.ToString() = v.ToString()

type gasLlimit = GasLimit
type gasPrice = GasPrice
type weiValue = WeiValue