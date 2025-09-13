(***
---
title: Agent Paradigms
description: Explanation of distributed agent models in FSharp.CloudAgent.
category: Concepts
---
***)
(**
Agent Paradigms
===============
This article briefly explains the two distributed agent models supported by CloudAgent.

Agent types
-----------
Different types of agents control the manner in which messages are passed through your
application as well as the sizing behaviour of the pools themselves.
### Worker Pools
Worker Pools represent agents which are entirely stateless. They can handle messages
in any order and at any time. Worker Pools in CloudAgent are fixed at a size of 512 workers
per node; messages will be randomly distributed through the worker pool. You will probably
use Workers for the majority of your distributed workloads.
### Actor Pools
Actor Pools represent agents which activate and deactive as work appears for them to
process. Messages are "routed" to a specific agent by means of an ActorKey - a simple
string which is used to determine which agent will receive the message. Each CloudAgent
node maintains a set of agents which will be created and destroyed as required to fulfil
requests for a specific actor. Thus, whilst an actor pool is theoretically unlimited in size,
the actual number of Actors running at any given time is equal to the number of messages
being processed, grouped by ActorKey.
As both Service Bus sessions and F# Agents naturally guarantee sequential flow of messages,
Actor Pools can be used in situations where you do not want to worry about concurrent processing
of messages. This can be used to dramatically simplify your software at the cost of removing
parallelisation of processing for messages keyed to the same actor.
### Hybrid Models
There is nothing to stop you sharing agent code across both types of agent pools. You can send
some messages over a standard Service Bus Queue for worker-style processing, and send
other messages that you need to run in isolation over a sessionised queue for Actor processing.

Agent Resiliency
----------------
Agents can handle messages in different ways to cope with errors. CloudAgent offers two different
models.
### Basic Agents
Basic agents are the most simple to create - you simply bind your existing F# Agent code
to a service bus queue. When messages arrive from the service bus, once they have been
successfully deserialised, CloudAgent will dispatch the to an agent for processing and
immediately mark them as complete on the service bus.
This is the simplest model for processing and means that your agents will never receive
the same message more than once. However, it is entirely your responsibility to handle
retry logic of messages which fail, or to log messages which cannot be processed. If
your agent crashes, or Azure decides to restart the machine on which your agent pool is
running on whilst processing a message, that message will be lost.
You might use Basic Agents when creating pools where failing to completely process a single
message is not critical to the overall operation of your application.
### Resilient Agents
Resilient Agents use the built-in capabilities of Azure Service Bus to guarantee message
processing, with built-in retry logic and dead lettering. Rather than simply receiving a
message and processing them, your F# Agents receive both a message and a reply channel.
Once your agent finishes processing a message, it should reply back with a status
(Completed, Failed or Abandon), which in turn is converted into a Service Bus message
acknowledgement. Only when Service Bus receives this acknowledge will it remove the message
from the queue. Otherwise, if your Agent crashes or fails to reply within the message timeout
period, Service Bus will once again make the message available for other nodes to consume.
Messages that repeatedly fail will automatically be pushed to a dead letter queue on the
service bus where they can be analysed in isolation.
In this model, messages are guaranteed to be delivered at least once to an agent for
processing. Use this model where you need to guarantee delivery of every message in the
system to your agents and need resliency of message processing.
*)