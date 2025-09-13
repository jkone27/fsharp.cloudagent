### 0.1 - Initial release
* Basic distributed Agents
* Reliable agents backed by Service Bus queues
* Workers Pools
* Actor Pools

### 0.2 - Minor enhancements
* Fixed a couple of bugs in resilient agents.

### 0.3
* Explicit session abandonment.
* Batch message support.

### 0.4 - Breaking Changes
* moved to .netstandard2.0 and net8.0 support
* using Azure.Messaging.ServiceBus library
* removed paket to simplify usage for .net users
* added devcontainer
* replaces travis with CI build in github workflows
* added fsdocs-tool
* added dotnet-outdated-tool