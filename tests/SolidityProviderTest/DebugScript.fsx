#r @"..\..\src\SolidityProvider.Runtime\bin\Debug\netcoreapp3.1\SolidityProvider.Runtime.dll"

[<Literal>]
let path = @"C:\Users\user\Git\dEth\smart-contracts\dETH2\build\contracts"

type a = SolidityProviderNS.SolidityTypes<path>

let dEth = a.dETHContract ()