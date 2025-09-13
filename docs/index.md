---
title: FSharp.CloudAgent Documentation
category: General
---

# FSharp.CloudAgent

A simple framework for making distributed F# Agents in the cloud with the minimum of hassle.

## Documentation

- [Tutorial](content/tutorial.html): Basic examples of how to use FSharp.CloudAgent.
- [Agent Paradigms](content/agent-paradigms.html): Core differences between the types of supported Cloud Agents.
- [Azure Service Bus](content/azure-service-bus.html): Overview of Azure Service Bus and how it interoperates with F# Agents.
- [Windows Service Bus](content/windows-service-bus.html): Overview of Windows Service Bus and how it can be used in place of Azure Service Bus.
- [Error Handling](content/handling-errors.html): Principles for error handling within Cloud Agent.
- [API Reference](reference/index.html): Automatically generated documentation for all types, modules, and members.

## About

FSharp.CloudAgent provides a simple framework to easily distribute workloads over the cloud whilst using standard F# Agents as the processing mechanism. Support exists for both simple and reliable messaging via Azure Service Bus, and for both workers and actors.

## Maintainers

- [@isaacabraham](https://github.com/isaacabraham)
- [@jkone27](https://github.com/jkone27)

## Get Started

To build and test:

```sh
dotnet build
dotnet test
```

To generate and view docs locally:

```sh
dotnet fsdocs watch
```

For more details, see the [Zero to Hero guide](https://fsprojects.github.io/FSharp.Formatting/zero-to-hero.html).
