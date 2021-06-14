module Tests

open Xunit

[<Literal>]
let path = @"C:\Users\user\Git\dEth\smart-contracts\dETH2\build\contracts"

[<Fact>]
let ``My test`` () =
    Assert.True(true)