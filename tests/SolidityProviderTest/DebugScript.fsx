#r @"..\..\src\SolidityProvider.Runtime\bin\Debug\typeproviders\fsharp41\netcoreapp3.1\Nethereum.ABI.dll"
#r @"..\..\src\SolidityProvider.Runtime\bin\Debug\typeproviders\fsharp41\netcoreapp3.1\Nethereum.Contracts.dll"
#r @"..\..\src\SolidityProvider.Runtime\bin\Debug\netcoreapp3.1\SolidityProvider.Runtime.dll"


open Nethereum.ABI.FunctionEncoding.Attributes

[<Literal>]
let rootFolder = @"C:\Users\Ilyas\redDuck\SolidityProvider\tests\SolidityProviderTest"

type A = SolidityProviderNS.SolidityTypes< @"..\..\Playground\Contracts">

//printfn "%A" A
let dEth = A.dEthContract ()

let allowance = A.dEthContract.allowanceFunction()
//let allowanceOut = A.dETHContract.allowanceOutputDTO()
//printfn "before change: %A" allowanceOut.Prop0
//allowanceOut.Prop0 <- System.Numerics.BigInteger 1
//printfn "after change: %A" allowanceOut.Prop0

//let approval = A.dETHContract.ApprovalEventDTO()

//let settings = A.dETHContract.AutomationSettingsChangedEventDTO()
//let deployment = A.dETHContract.dETHDeployment()

[| typeof<A.dEthContract.getCollateralFunction> |]
|> Array.map (fun t ->
        printfn "--- %s ---" t.Name
        t.GetCustomAttributes(typeof<FunctionAttribute>, false)
        |> Array.map (fun attr -> (attr :?> FunctionAttribute).DTOReturnType |> printfn "%A")
    )


