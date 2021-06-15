#r @"..\..\src\SolidityProvider.Runtime\bin\Debug\netcoreapp3.1\SolidityProvider.Runtime.dll"

[<Literal>]
let rootFolder = @"C:\Users\user\Git\SolidityProvider"

[<Literal>]
let path = rootFolder + @"\Playground\Contracts"

type a = SolidityProviderNS.SolidityTypes<path>

let dEth = a.dETHContract ()