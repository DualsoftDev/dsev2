namespace Ev2.S7Protocol.Tests

module ValueGenerators =
    let bools count = Array.init count (fun i -> i % 2 = 0)
