module Tests

open Xunit

[<Literal>]
let a = @"C:\Users\user\Git\dEth\smart-contracts\dETH2\build\contracts"

[<Fact>]
let ``My test`` () =
    Assert.True(true)
    Domain.constructRootType (Assembly.GetExecutingAssembly()) "someNS" "SolidityType" [|a|]