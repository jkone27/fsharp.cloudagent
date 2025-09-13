namespace FSharp.CloudAgent.Actors

open FSharp.CloudAgent
open FSharp.CloudAgent.Messaging
open System
open System.Collections.Generic

/// <summary>
/// Manages lifetime of actors in the cloud agent system.
/// </summary>
type internal IActorStore<'a> = 
    /// <summary>
    /// Requests an actor for a particular key.
    /// </summary>
    abstract GetActor : ActorKey -> CloudAgentKind<'a>
    /// <summary>
    /// Tells the store that an actor is no longer required and can be safely removed.
    /// </summary>
    abstract RemoveActor : ActorKey -> unit

// Manages per-session actors.
type private ActorStoreRequest =
    | Get of ActorKey
    | Remove of ActorKey

type private ActorStoreAgent<'a> = MailboxProcessor<ActorStoreRequest * AsyncReplyChannel<CloudAgentKind<'a>> option>

module internal Factory = 
    let private createActorStore<'a> createAgent = 
        let actorStore = 
            new ActorStoreAgent<'a>(fun inbox -> 
                let actors = Dictionary()
                async { 
                    while true do
                        let! message, replyChannel = inbox.Receive()
                        match message with
                        | Get(ActorKey agentId) -> 
                            let actor = 
                                if not (actors.ContainsKey agentId) then 
                                    let actor = createAgent (ActorKey agentId)
                                    actors.Add(agentId, actor)
                                    match actor with
                                    | ResilientCloudAgent actor -> 
                                        try actor.Start()
                                        with _ -> ()
                                    | BasicCloudAgent actor -> 
                                        try actor.Start()
                                        with _ -> ()
                                actors.[agentId]
                            replyChannel |> Option.iter (fun replyChannel -> replyChannel.Reply actor)
                        | Remove(ActorKey actorKey) -> 
                            if actors.ContainsKey(actorKey) then 
                                let actorToRemove = 
                                    match actors.[actorKey] with
                                    | ResilientCloudAgent actor -> actor :> IDisposable
                                    | BasicCloudAgent actor -> actor :> IDisposable
                                actors.Remove actorKey |> ignore
                                actorToRemove.Dispose()
                })
        actorStore.Start()
        actorStore
    
    /// <summary>
    /// Creates an IActorStore that can add, retrieve, and remove agents in a threadsafe manner, using the supplied function to create new agents on demand.
    /// </summary>
    let CreateActorStore<'a> createActor = 
        let actorStore = createActorStore<'a> createActor
        { new IActorStore<'a> with
              member __.GetActor(sessionId) = actorStore.PostAndReply(fun ch -> (Get sessionId), Some ch)
              member __.RemoveActor(sessionId) = actorStore.Post(Remove sessionId, None) }
    
    /// <summary>
    /// Function type for selecting an agent to consume a message.
    /// </summary>
    type AgentSelectorFunc<'a> = unit -> Async<CloudAgentKind<'a>>
    
    /// <summary>
    /// Generates a pool of CloudAgents of a specific size that can be used to select an agent when required.
    /// </summary>
    let CreateAgentSelector<'a>(size, createAgent : ActorKey -> CloudAgentKind<'a>) : AgentSelectorFunc<'a> = 
        let agents = 
            [ for i in 1 .. size -> 
                let agent = createAgent (ActorKey <| i.ToString())
                match agent with
                | BasicCloudAgent agent -> 
                    try agent.Start()
                    with _ -> ()
                | ResilientCloudAgent agent -> 
                    try agent.Start()
                    with _ -> ()
                agent ]
        
        let r = Random()
        fun () -> async { return agents.[r.Next(0, agents.Length)] }