// https://fsprojects.github.io/FSharp.CloudAgent/tutorial.html
#r "nuget: CloudAgentV2, 0.4.1-test"
// reference test containers azure service bus package
// https://testcontainers.com/modules/azure-servicebus/
#r "nuget: Testcontainers.ServiceBus"
// #r "nuget: System.ServiceModel.Primitives"
#r "nuget: Azure.Messaging.ServiceBus"

(* var serviceBusContainer = new ServiceBusBuilder()
  .WithImage("mcr.microsoft.com/azure-messaging/servicebus-emulator:latest")
  .Build();
await serviceBusContainer.StartAsync();*)

open FSharp.CloudAgent
open System
open FSharp.CloudAgent
open FSharp.CloudAgent.Messaging
open FSharp.CloudAgent.Connections
open Testcontainers.ServiceBus
open System.Threading.Tasks
open Azure.Messaging.ServiceBus

// A DTO
type Person = { Name : string; Age : int }

// https://raw.githubusercontent.com/Azure/azure-service-bus-emulator-installer/refs/heads/main/ServiceBus-Emulator/Config/Config.json
let serviceBusContainer =
    new ServiceBusBuilder()
    |> _.WithImage("mcr.microsoft.com/azure-messaging/servicebus-emulator:latest")
    |> _.WithName("test-az-service-bus-emulator")
    |> _.WithAcceptLicenseAgreement(true)
    |> _.Build()

(*
// https://dotnet.testcontainers.org/modules/servicebus/
// By default, the emulator uses the following configuration:
// https://learn.microsoft.com/en-us/azure/service-bus-messaging/test-locally-with-service-bus-emulator?tabs=automated-script#interact-with-the-emulator.

// Upload a custom configuration before the container starts using the
// `WithResourceMapping(string, string)` API or one of its overloads:
// `WithResourceMapping("Config.json", "/ServiceBus_Emulator/ConfigFiles/")`.
*)

serviceBusContainer.StartAsync() |> Async.AwaitTask |> Async.RunSynchronously

// make this an observable callback
let startAgent queueName =
    // Standard Azure Service Bus connection string
    let serviceBusConnection =
        ServiceBusConnection (serviceBusContainer.GetConnectionString())


    // A function which creates an Agent on demand.
    let createASimpleAgent agentId =
        MailboxProcessor.Start(fun inbox ->
            async {
                while true do
                    let! message = inbox.Receive()
                    printfn "{%s}: %s is %d years old." queueName message.Name message.Age
            })

    // Create a worker cloud connection to the Service Bus Queue "myMessageQueue"
    let cloudConnection = WorkerCloudConnection(serviceBusConnection, Queue queueName)

    // Start listening! A local pool of agents will be created that will receive messages.
    // Service bus messages will be automatically deserialised into the required message type.
    let agent = ConnectionFactory.StartListening(cloudConnection, createASimpleAgent >> BasicCloudAgent)

    (agent, cloudConnection)

let (agent, cloudConnection) = startAgent "queue.1"

printfn "agent started"

let sendToMyMessageQueue = ConnectionFactory.SendToWorkerPool cloudConnection

let sendMessagesAsync () = task {
    // These messages will be processed in parallel across the worker pool.
    let t1 = sendToMyMessageQueue { Name = "Isaac"; Age = 34 } |> Async.StartAsTask :> Task
    let t2 = sendToMyMessageQueue { Name = "Michael"; Age = 32 } |> Async.StartAsTask :> Task
    let t3 = sendToMyMessageQueue { Name = "Sam"; Age = 27 } |> Async.StartAsTask :> Task

    do! Task.WhenAll([| t1;t2;t3 |])

    do! Task.Delay(delay=TimeSpan.FromSeconds(3.))
}

sendMessagesAsync() |> Async.AwaitTask |> Async.RunSynchronously

printfn "sent messages"

agent.Dispose()

serviceBusContainer.DisposeAsync().AsTask() |> Async.AwaitTask |> Async.RunSynchronously

printfn "end of script."

