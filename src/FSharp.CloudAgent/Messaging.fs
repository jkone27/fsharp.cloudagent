namespace FSharp.CloudAgent.Messaging

open FSharp.CloudAgent
open System
open Newtonsoft.Json
open System.Runtime.Serialization
open Azure.Messaging.ServiceBus
open System.Threading.Tasks
open System.Threading

[<AutoOpen>]
module internal Streams = 
    open FSharp.CloudAgent.Messaging

    /// Represents a stream of cloud messages.
    type ICloudMessageStream = 
        abstract GetNextMessage : TimeSpan -> Async<SimpleCloudMessage option>
        abstract CompleteMessage : SimpleCloudMessage -> Async<unit>
        abstract AbandonMessage : SimpleCloudMessage -> Async<unit>
        abstract DeadLetterMessage : SimpleCloudMessage -> Async<unit>

    /// Represents a stream of messages for a specific actor.
    type IActorMessageStream = 
        inherit ICloudMessageStream
        abstract RenewSessionLock : unit -> Async<unit>
        abstract AbandonSession : unit -> Async<unit>
        abstract SessionId : ActorKey

    type private QueueStream(receiver : ServiceBusReceiver) =
        interface ICloudMessageStream with            
            member __.DeadLetterMessage(message) = 
                message.ReceivedMessage |> receiver.DeadLetterMessageAsync |> Async.AwaitTaskEmpty
                
            member __.AbandonMessage(message) = 
                message.ReceivedMessage |> receiver.AbandonMessageAsync |> Async.AwaitTaskEmpty
                
            member __.CompleteMessage(message) = 
                message.ReceivedMessage |> receiver.CompleteMessageAsync |> Async.AwaitTaskEmpty
                
            member __.GetNextMessage(timeout) = 
                async { 
                    let! msg = receiver.ReceiveMessageAsync(timeout) |> Async.AwaitTask
                    if isNull msg then return None
                    else
                        let expiry =
                            if msg.ExpiresAt = DateTimeOffset.MinValue then DateTimeOffset.MaxValue
                            else msg.ExpiresAt
                        return Some {   Body = msg.Body.ToString()
                                        ReceivedMessage = msg
                                        Expiry = expiry }
                }
    
    type private SessionisedQueueStream(sessionReceiver : ServiceBusSessionReceiver) =
        inherit QueueStream(sessionReceiver :> ServiceBusReceiver)
        interface IActorMessageStream with
            member __.AbandonSession() = sessionReceiver.CloseAsync() |> Async.AwaitTaskEmpty
            member __.RenewSessionLock() = sessionReceiver.RenewSessionLockAsync() |> Async.AwaitTaskEmpty
            member __.SessionId = ActorKey sessionReceiver.SessionId

    let CreateActorMessageStream (connectionString, queueName, timeout:TimeSpan, wireSerializer) =
        let client = new ServiceBusClient(connectionString)
        fun () -> 
            async { 
                try
                    use cts = new CancellationTokenSource()
                    cts.CancelAfter(timeout)
                    let! session = client.AcceptNextSessionAsync(queueName, null, cts.Token) |> Async.AwaitTask |> Async.Catch
                    return
                        match session with
                        | Error _
                        | Result null -> None
                        | Result session -> Some(SessionisedQueueStream (session) :> IActorMessageStream)
                with _ -> return None
            }

    let CreateQueueStream(connectionString, queueName, wireSerializer) =
        let client = new ServiceBusClient(connectionString)
        let receiver = client.CreateReceiver(queueName)
        QueueStream(receiver) :> ICloudMessageStream

[<AutoOpen>]
module internal Serialization =
    /// Manages serialization / deserialization for putting messages on the queue.
    type ISerializer<'a> =
        /// Deserializes a string back into an object.
        abstract member Deserialize : string -> 'a
        /// Serializes an object into a string.
        abstract member Serialize : 'a -> string
    
    /// A serializer using Newtonsoft's JSON .NET serializer.
    let JsonSerializer<'a>() =
        { new ISerializer<'a> with
                member __.Deserialize(json) = JsonConvert.DeserializeObject<'a>(json)
                member __.Serialize(data) = JsonConvert.SerializeObject(data) }

[<AutoOpen>]
module internal Helpers =
    open FSharp.CloudAgent.Messaging

    /// Knows how to process a single brokered message from the service bus, with error handling
    /// and processing by the target queue.
    let ProcessBrokeredMessage<'a> (serializer:ISerializer<'a>) (agent:CloudAgentKind<'a>) message =
        async {
            let! messageBody = async { return serializer.Deserialize(message.Body) } |> Async.Catch
            let! processResult =
                match messageBody with
                | Error _ -> async { return Failed } // could not deserialize
                | Result messageBody ->
                    async {
                        match agent with
                        | BasicCloudAgent agent ->
                            // Do not wait for a response - just return success.
                            agent.Post messageBody
                            return Completed
                        | ResilientCloudAgent agent ->
                            // Wait for the response and return it. Timeout is set based on message expiry unless it's too large.
                            let expiryInMs =
                                match (int ((message.Expiry - DateTimeOffset.UtcNow).TotalMilliseconds)) with
                                | expiryMs when expiryMs < -1 -> -1
                                | expiryMs -> expiryMs

                            let! processingResult = agent.PostAndTryAsyncReply((fun ch -> messageBody, ch.Reply), expiryInMs) |> Async.Catch
                            return
                                match processingResult with
                                | Error _
                                | Result None -> Failed
                                | Result (Some status) -> status
                    }
            return processResult
        }
    
    /// Asynchronously gets the "next" item, repeatedly calling the supply getItem function
    /// until it returns something.
    let withAutomaticRetry getItem pollTime =
        let rec continuePolling() =
            async {
                let! nextItem = getItem() |> Async.Catch
                match nextItem with
                | Error ex -> return! continuePolling()
                | Result None -> return! continuePolling()
                | Result (Some item) -> return item
            }
        continuePolling()
    
/// Manages dispatching of messages to a service bus queue.
[<AutoOpen>]
module internal Dispatch =
    open Azure.Messaging.ServiceBus

    /// Contains configuration details for posting messages to a cloud of agents or actors.
    type MessageDispatcher<'a> = 
        {   ServiceBusConnectionString : string
            QueueName : string
            Serializer : ISerializer<'a> }

    /// Creates a dispatcher using default settings.
    let createMessageDispatcher<'a> (connectionString, queueName) =
        {   ServiceBusConnectionString = connectionString
            QueueName = queueName
            Serializer = JsonSerializer<'a>() }

    let private toServiceBusMessage options sessionId message =
        let payload = message |> options.Serializer.Serialize
        let sbMsg = ServiceBusMessage(BinaryData(payload))
        match sessionId with
        | Some id -> sbMsg.SessionId <- id
        | None -> ()
        sbMsg

    let postMessages (options:MessageDispatcher<'a>) sessionId messages =
        async { 
            let brokeredMessages = 
                messages
                |> Seq.map (toServiceBusMessage options sessionId) 
                |> Seq.toArray
            let client = new ServiceBusClient(options.ServiceBusConnectionString)
            let sender = client.CreateSender(options.QueueName)
            do! sender.SendMessagesAsync(brokeredMessages) |> Async.AwaitTaskEmpty
            do! sender.DisposeAsync().AsTask() |> Async.AwaitTaskEmpty
            do! client.DisposeAsync().AsTask() |> Async.AwaitTaskEmpty
        }

    let postMessage (options:MessageDispatcher<'a>) sessionId message =
        async { 
            let sbMsg = toServiceBusMessage options sessionId message
            let client = new ServiceBusClient(options.ServiceBusConnectionString)
            let sender = client.CreateSender(options.QueueName)
            do! sender.SendMessageAsync(sbMsg) |> Async.AwaitTaskEmpty
            do! sender.DisposeAsync().AsTask() |> Async.AwaitTaskEmpty
            do! client.DisposeAsync().AsTask() |> Async.AwaitTaskEmpty
        }
