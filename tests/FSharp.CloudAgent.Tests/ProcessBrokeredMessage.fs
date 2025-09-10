[<NUnit.Framework.TestFixture>]
module FSharp.CloudAgent.Tests.``Process Brokered Message``

open FSharp.CloudAgent.Tests.Helpers
open FSharp.CloudAgent.Messaging
open NUnit.Framework
open Swensen.Unquote
open System
open System.Threading

[<Test>]
let ``Passes deserialized object into agent``() =
    let person, agent = getBasicAgent()
    let _ = { Person.Name = "Isaac" }
            |> createPersonMessage
            |> processPersonMessage agent
            |> Async.RunSynchronously
    Async.Sleep(100) |> Async.RunSynchronously
    Assert.Equals({ Person.Name = "Isaac" }, !person)

[<Test>]
let ``Immediately returns completed for BasicCloudAgent``() =
    let _, agent = getBasicAgent()
    let result = { Person.Name = "Isaac" }
                 |> createPersonMessage
                 |> processPersonMessage agent
                 |> Async.RunSynchronously
    Assert.Equals(Completed, result)

[<Test>]
let ``Serialization failure immediately returns Failed``() =
    let _, agent = getBasicAgent()
    let message =
        let serializer = JsonSerializer<Thing>()
        createMessage serializer (DateTime.UtcNow.AddSeconds 10.) { Thing.Name = { Person.Name = "Isaac" } }

    let result = message |> processPersonMessage agent |> Async.RunSynchronously
    Assert.Equals(Failed, result)

[<Test>]
let ``Returns result from Resilient agent``() =
    let agent = getResilientAgent(fun _ -> Abandoned)
    let result = { Person.Name = "Isaac" }
                 |> createPersonMessage
                 |> processPersonMessage agent
                 |> Async.RunSynchronously
    Assert.Equals(Abandoned, result)

[<Test>]
let ``No result from Resilient agent returns Failed``() =
    let agent = getResilientAgent(fun _ -> Thread.Sleep 2001; Completed)
    let result = { Person.Name = "Isaac" }
                 |> createMessage personSerializer (DateTime.UtcNow.AddSeconds 2.)
                 |> processPersonMessage agent
                 |> Async.RunSynchronously
    Assert.Equals(Failed, result)

[<Test>]
let ``Exception from Resilient agent returns Failed``() =
    let agent = getResilientAgent(fun _ -> failwith "test")
    let result = { Person.Name = "Isaac" }
                 |> createMessage personSerializer (DateTime.UtcNow.AddSeconds 2.)
                 |> processPersonMessage agent
                 |> Async.RunSynchronously
    Assert.Equals(Failed, result)

[<Test>]
let ``Message with maximum expiry time is correctly processed``() =
    let agent = getResilientAgent(fun _ -> Completed)
    let result = { Person.Name = "Isaac" }
                 |> createMessage personSerializer DateTime.MaxValue
                 |> processPersonMessage agent
                 |> Async.RunSynchronously
    Assert.Equals(Completed, result)
