
#r @"bin\Release\netcoreapp3.1\Nethereum.Web3.dll"
#r @"bin\Release\netcoreapp3.1\Nethereum.RPC.dll"
#r @"bin\Release\netcoreapp3.1\Nethereum.ABI.dll"
#r @"bin\Release\netcoreapp3.1\Nethereum.Contracts.dll"

#r @"bin\Release\netcoreapp3.1\Nethereum.Hex.dll"
#r @"bin\Release\netcoreapp3.1\Nethereum.JsonRpc.Client.dll"

#r @"bin\Release\netcoreapp3.1\AbiTypeProvider.Common.dll"
#r @"C:\Users\Ilyas\.nuget\packages\abitypeprovider\0.0.4\lib\netcoreapp3.1\AbiTypeProvider.Runtime.dll"

type A = AbiTypeProvider.AbiTypes< @"C:\Users\Ilyas\source\repos\ConsoleApp1\Contracts">

printfn "%s" A.FromFolder