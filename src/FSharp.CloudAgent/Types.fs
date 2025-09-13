namespace FSharp.CloudAgent

/// <summary>
/// Represents a unique key to identify an agent or actor in the cloud agent system.
/// </summary>
type ActorKey = 
    /// <summary>
    /// The key value for the actor.
    /// </summary>
    | ActorKey of string

[<AutoOpen>]
module internal Async = 
    open System
    open System.Threading.Tasks
    
    /// <summary>
    /// Awaits a Task that does not return a value, converting it to an F# async workflow.
    /// </summary>
    let AwaitTaskEmpty(task : Task) = 
        Async.FromContinuations(fun (onSuccess, onException, onCancellation) -> 
            task.ContinueWith(fun t -> 
                if t.IsCompleted then onSuccess()
                elif t.IsFaulted then onException (t.Exception)
                else onCancellation (System.OperationCanceledException()))
            |> ignore)
    
    /// <summary>
    /// Active pattern for handling Choice results as Result or Error.
    /// </summary>
    let (|Result|Error|) =
        function
        | Choice1Of2 result -> Result result
        | Choice2Of2 (ex:Exception) -> Error ex

namespace FSharp.CloudAgent.Connections

/// <summary>
/// Represents details of a connection to an Azure Service Bus.
/// </summary>
type ServiceBusConnection = 
    /// <summary>
    /// The connection string for the Azure Service Bus.
    /// </summary>
    | ServiceBusConnection of string

/// <summary>
/// Represents a service bus queue.
/// </summary>
type Queue = 
    /// <summary>
    /// The name of the queue.
    /// </summary>
    | Queue of string

/// <summary>
/// Represents a connection to a pool of agents.
/// </summary>
type CloudConnection = 
    /// <summary>
    /// A generic worker cloud that can run workloads in parallel.
    /// </summary>
    | WorkerCloudConnection of ServiceBusConnection * Queue
    /// An actor-based cloud that can run workloads in parallel whilst ensuring sequential workloads per-actor.
    | ActorCloudConnection of ServiceBusConnection * Queue

namespace FSharp.CloudAgent.Messaging

open System
open Azure.Messaging.ServiceBus

/// The different completion statuses a CloudMessage can have.
type MessageProcessedStatus = 
    /// The message successfully completed.
    | Completed
    /// The message was not processed successfully and should be returned to the queue for processing again.
    | Failed
    /// The message cannot be processed and should be not be attempted again.
    | Abandoned

/// Represents the kinds of F# Agents that can be bound to an Azure Service Bus Queue for processing distributed messages, optionally with automatic retry.
type CloudAgentKind<'a> = 
    /// A simple cloud agent that offers simple forward-only processing of messages.
    | BasicCloudAgent of MailboxProcessor<'a>
    /// A cloud agent that requires explicit completion of processed message, with automatic retry and dead lettering.
    | ResilientCloudAgent of MailboxProcessor<'a * (MessageProcessedStatus -> unit)>

/// Contains the raw data of a cloud message.
type internal SimpleCloudMessage = 
    { Body : string
      ReceivedMessage : ServiceBusReceivedMessage
      Expiry : DateTimeOffset }
