module Harness =

    open BenchmarkDotNet.Engines
    open BenchmarkDotNet.Attributes

    [<MemoryDiagnoser>]
    type Harness() =

        [<Benchmark>]
        member _.Old() =
            Benchmarks.gentaMicin()

        [<Benchmark>]
        member _.New() =
            // This could be a candidate new implementation:
            Benchmarks.gentaMicin()

open BenchmarkDotNet.Running

[<EntryPoint>]
let main argv =
    
    BenchmarkRunner.Run<Harness.Harness>()
    |> printfn "%A"
    0 
