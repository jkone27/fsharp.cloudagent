#r "nuget: Azure.Messaging.ServiceBus, 7.20.1"
#r "nuget: Newtonsoft.Json, 13.0.3"

#r "../../bin/net8.0/FSharp.CloudAgent.dll"

open FSharp.CloudAgent
open FSharp.CloudAgent.Connections
open FSharp.CloudAgent.Messaging
open FSharp.CloudAgent.ConnectionFactory

// Connection strings to different service bus queues
let connectionString = ServiceBusConnection "Endpoint=sb://yourServiceBus.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=yourKey"
let workerConn = WorkerCloudConnection(connectionString, Queue "workerQueue")
let actorConn = ActorCloudConnection(connectionString, Queue "actorQueue")

type Person = { Name : string; Age : int }



(* ------------- Standard F# Agent (typed) --------------- *)

let createBasicAgentPerson (ActorKey actorKey) =
    new MailboxProcessor<Person>(fun mailbox ->
        async {
            while true do
                let! message = mailbox.Receive()                
                printfn "Actor %s has received message '%A', processing..." actorKey message
        })

// Adapter: produce CloudAgentKind<obj> by wrapping a typed MailboxProcessor<Person>
let createBasicAgentObj (ak:ActorKey) : CloudAgentKind<obj> =
    let personAgent = createBasicAgentPerson ak
    let boxed = new MailboxProcessor<obj>(fun inbox ->
        async {
            while true do
                let! o = inbox.Receive()
                let p = unbox<Person> o
                personAgent.Post p
        })
    BasicCloudAgent(boxed)

// Start listening for messages as obj
let disposable = StartListening<obj>(workerConn, createBasicAgentObj)

// Send a message to the worker pool (box the Person)
{ Name = "Isaac"; Age = 34 }
|> box
|> SendToWorkerPool<obj> workerConn
|> Async.RunSynchronously

disposable.Dispose()



(* ------------- Resilient F# Agent (typed) --------------- *)

let createResilientAgentPerson (ActorKey actorKey) =
    MailboxProcessor.Start(fun mailbox ->
        async {
            while true do
                let! message, reply = mailbox.Receive()                
                printfn "Actor %s has received message '%A', processing..." actorKey message
                let response =
                    match message with
                    | { Name = "Isaac" } -> Completed
                    | { Name = "Mike" } -> Abandoned
                    | _ -> Failed
                printfn "%A" response
                reply response
        })

let createResilientAgentObj (ak:ActorKey) : CloudAgentKind<obj> =
    let personAgent = createResilientAgentPerson ak
    let boxed = MailboxProcessor<obj * (MessageProcessedStatus -> unit)>.Start(fun inbox ->
        async {
            while true do
                let! o, reply = inbox.Receive()
                let p = unbox<Person> o
                personAgent.Post(p, reply)
        })
    ResilientCloudAgent(boxed)

// Start listening for resilient agents
StartListening<obj>(workerConn, createResilientAgentObj) |> ignore

// Send a message to the worker pool
{ Name = "Isaac"; Age = 34 } 
|> box
|> SendToWorkerPool<obj> workerConn
|> Async.RunSynchronously



(* ------------- Actor-based F# Agents using Service Bus sessions to ensure synchronisation of messages--------------- *)

let actorDisposable = StartListening<obj>(actorConn, createResilientAgentObj)

// Send a message to an actor (box the Person)
{ Name = "Isaac"; Age = 34 } 
|> box
|> SendToActorPool<obj> actorConn (ActorKey "Tim")
|> Async.RunSynchronously

actorDisposable.Dispose()