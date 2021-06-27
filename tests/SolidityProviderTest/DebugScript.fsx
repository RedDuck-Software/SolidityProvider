#r @"..\..\src\SolidityProvider.Runtime\bin\Debug\typeproviders\fsharp41\netcoreapp3.1\Nethereum.ABI.dll"
#r @"..\..\src\SolidityProvider.Runtime\bin\Debug\netcoreapp3.1\SolidityProvider.Runtime.dll"

[<Literal>]
let rootFolder = @"C:\Users\Ilyas\redDuck\SolidityProvider"

[<Literal>]
let path = rootFolder + @"\Playground\Contracts"

type A = SolidityProviderNS.SolidityTypes<path>

//printfn "%A" A
let dEth = A.dETHContract ()

printfn "%A" dEth