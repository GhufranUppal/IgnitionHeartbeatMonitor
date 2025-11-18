# HeartBeatContracts.cs Documentation

This document describes the domain DTOs and pipeline interfaces defined in `HeartBeatContracts.cs`.

## Overview

These contracts define the core domain model and the processing pipeline for monitoring Ignition heartbeat signals via OPC UA.

They include:

- **TagValue<T>** – strongly typed tag value DTO  
- **HeartbeatState** – health state enumeration  
- **HeartbeatEvent** – normalized event emitted by the pipeline  
- **ITagReader** – abstraction for reading tag streams (subscription or polling)  
- **IValidator** – applies heartbeat timing & quality rules  
- **ITransformer** – converts raw data into a publishable event  
- **IPublisher** – sends events to external systems (Console, Azure, HTTP, etc.)  

---

## TagValue<T>

```csharp
public sealed record TagValue<T>(string NodeId, T? Value, DateTime ServerTimestamp, string Quality);
```

Represents a strongly typed value read from OPC UA or another tag source.

**Fields:**
- `NodeId` – Full OPC UA NodeId path  
- `Value` – Generic typed value (`bool`, `int`, `double`, etc.)  
- `ServerTimestamp` – Timestamp from OPC UA server  
- `Quality` – "Good", "Bad", "Uncertain", etc.  

This DTO is generic to work with any tag type.

---

## HeartbeatState

```csharp
public enum HeartbeatState { Ok, Late, Stalled, BadQuality }
```

Represents the heartbeat state as determined by validation.

- **Ok** – On time and valid  
- **Late** – Slightly behind expected schedule  
- **Stalled** – Fully missed/stopped heartbeat  
- **BadQuality** – Tag quality degraded or unreadable  

---

## HeartbeatEvent

```csharp
public sealed record HeartbeatEvent(
    HeartbeatState State,
    double? AgeSeconds,
    DateTime ObservedAtUtc,
    string Detail);
```

Normalized heartbeat event sent to consumers.

**Fields:**
- `State` – Ok, Late, Stalled, BadQuality  
- `AgeSeconds` – Optional value from `Heartbeat_Age_Seconds` tag  
- `ObservedAtUtc` – Timestamp when event generated  
- `Detail` – Human-readable explanation  

---

## ITagReader

```csharp
public interface ITagReader : IAsyncDisposable
{
    IAsyncEnumerable<TagValue<object>> ReadStreamAsync(CancellationToken ct);
}
```

Provides a continuous async stream of tag updates.

Responsibilities:
- Subscribe to OPC UA DataChange events  
- Fallback to polling if subscription unavailable  
- Emit data as `TagValue<object>`  
- Dispose underlying OPC UA session cleanly  

This is the first stage of the heartbeat pipeline.

---

## IValidator

```csharp
public interface IValidator
{
    HeartbeatState Validate(
        TagValue<object> hb,
        TagValue<object>? ageSec,
        TimeSpan expected,
        TimeSpan late,
        TimeSpan stall,
        out string detail);
}
```

Evaluates raw heartbeat and age tags to determine:

- Whether heartbeat is late  
- Whether heartbeat has stalled  
- Whether quality is acceptable  

Outputs a computed `HeartbeatState` and a detailed message.

---

## ITransformer

```csharp
public interface ITransformer
{
    HeartbeatEvent ToEvent(
        HeartbeatState state,
        TagValue<object> hb,
        TagValue<object>? ageSec,
        string detail);
}
```

Transforms validated raw tag values into a clean `HeartbeatEvent`.

Responsible for:
- Mapping domain model to pipeline event  
- Choosing final timestamps  
- Formatting details  

---

## IPublisher

```csharp
public interface IPublisher
{
    Task PublishAsync(HeartbeatEvent @event, CancellationToken ct);
}
```

Publishes `HeartbeatEvent` data to external systems.

Typical implementations:
- Console publisher  
- File publisher  
- HTTP / Azure Function publisher  
- IoT Hub publisher  
- Message bus publisher  

---

## Summary

The contracts in `HeartBeatContracts.cs` form the **backbone** of the heartbeat pipeline:

```
ITagReader  →  IValidator  →  ITransformer  →  IPublisher
```

They ensure the system remains:

- Modular  
- Testable  
- Extensible  
- Cleanly separated by responsibility  

Perfect foundation for a professional monitoring pipeline.

