// Domain DTOs
using System;
using System.Threading;
using System.Threading.Tasks;

public sealed record TagValue<T>(string NodeId, T? Value, DateTime ServerTimestamp, string Quality);

public enum HeartbeatState { Ok, Late, Stalled, BadQuality }

public sealed record HeartbeatEvent(
    HeartbeatState State,
    double? AgeSeconds,
    DateTime ObservedAtUtc,
    string Detail);

// Pipeline contracts
public interface ITagReader : IAsyncDisposable
{
    IAsyncEnumerable<TagValue<object>> ReadStreamAsync(CancellationToken ct);
    // Stream emits updates for the configured nodes. If subscription unavailable, it polls.
}

public interface IValidator
{
    HeartbeatState Validate(TagValue<object> hb, TagValue<object>? ageSec, TimeSpan expected, TimeSpan late, TimeSpan stall, out string detail);
}

public interface ITransformer
{
    HeartbeatEvent ToEvent(HeartbeatState state, TagValue<object> hb, TagValue<object>? ageSec, string detail);
}

public interface IPublisher
{
    Task PublishAsync(HeartbeatEvent @event, CancellationToken ct);
}