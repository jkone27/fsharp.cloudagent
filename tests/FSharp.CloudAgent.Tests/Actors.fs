﻿[<NUnit.Framework.TestFixture>]
module FSharp.CloudAgent.Tests.``Actor Factory``

open FSharp.CloudAgent
open FSharp.CloudAgent.Actors
open FSharp.CloudAgent.Messaging
open FSharp.CloudAgent.Tests.Helpers
open NUnit.Framework
open System
open Swensen.Unquote
open Microsoft.FSharp.Linq.NullableOperators

// https://docs.nunit.org/articles/nunit/writing-tests/assertions/assertions.html

let private getActorStore() = Factory.CreateActorStore(fun _ -> getBasicAgent() |> snd)

let private assertAgentIsStarted (agent:MailboxProcessor<_>) =
    Assertions.raisesWith<InvalidOperationException>
        <@ agent.Start() @>
        (fun ex -> <@ ex.Message = "The MailboxProcessor has already been started." @>)

[<Test>]
let ``ActorStore creates new actors``() =
    let actorStore = getActorStore()
    actorStore.GetActor (ActorKey "isaac") |> ignore

[<Test>]
let ``ActorStore automatically starts agents``() =
    let actorStore = getActorStore()
    let (BasicCloudAgent isaac) = actorStore.GetActor (ActorKey "isaac")
    isaac |> assertAgentIsStarted

[<Test>]
let ``ActorStore manages multiple new actors``() =
    let actorStore = getActorStore()
    let isaac = actorStore.GetActor (ActorKey "isaac")
    let tony = actorStore.GetActor (ActorKey "tony")
    Assert.That(isaac <> tony)

[<Test>]
let ``ActorStore reuses existing actors``() =
    let actorStore = getActorStore()
    let first = actorStore.GetActor (ActorKey "isaac")
    let second = actorStore.GetActor (ActorKey "isaac")
    Assert.That(first, Is.EqualTo second)

[<Test>]
let ``ActorStore removes existing actors``() =
    let actorStore = getActorStore()
    let first = actorStore.GetActor (ActorKey "isaac")
    actorStore.RemoveActor (ActorKey "isaac")
    let second = actorStore.GetActor (ActorKey "isaac")
    Assert.That(first <> second)

[<Test>]
let ``ActorStore disposes of removed agents``() =
    let actorStore = getActorStore()
    let (BasicCloudAgent isaac) = actorStore.GetActor (ActorKey "isaac")
    actorStore.RemoveActor (ActorKey "isaac")

[<Test>]
let ``AgentSelector creates already-started agents``() =
    let selector = Factory.CreateAgentSelector(5, fun _ -> getBasicAgent() |> snd)
    for _ in 1 .. 10 do
        let (BasicCloudAgent agent) = selector() |> Async.RunSynchronously
        agent |> assertAgentIsStarted