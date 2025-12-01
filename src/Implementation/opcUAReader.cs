
using Microsoft.Extensions.Configuration;
using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

public sealed class OpcUaTagReader : ITagReader
{
    private readonly string _endpointUrl;
    private readonly string[] _nodeIds;
    private readonly int _pubMs;
    private readonly int _sampleMs;
    private readonly int _queue;

    private ApplicationConfiguration? _appConfig;
    private Session? _session;
    private Subscription? _subscription;

    public OpcUaTagReader(IConfiguration cfg)
    {
        var opc = cfg.GetSection("OpcUa");
        _endpointUrl = opc["EndpointUrl"] ?? throw new ArgumentNullException("OpcUa:EndpointUrl");

        var nodes = opc.GetSection("NodeIds");
        var hb = nodes["Heartbeat"] ?? throw new ArgumentNullException("OpcUa:NodeIds:Heartbeat");
        var age = nodes["HeartbeatAgeSeconds"]; // optional
        _nodeIds = (age is { Length: > 0 }) ? new[] { hb, age } : new[] { hb };

        var sub = opc.GetSection("Subscription");
        _pubMs = int.Parse(sub["PublishingIntervalMs"] ?? "1000");
        _sampleMs = int.Parse(sub["SamplingIntervalMs"] ?? "1000");
        _queue = int.Parse(sub["QueueSize"] ?? "2");

        Console.WriteLine($"[OpcUaTagReader] Config = EndpointUrl: '{_endpointUrl}'");
        Console.WriteLine($"[OpcUaTagReader] Config = Publish/Sample: {_pubMs}/{_sampleMs} ms, Queue={_queue}");
    }

    public async IAsyncEnumerable<TagValue<object>> ReadStreamAsync([EnumeratorCancellation] CancellationToken ct)
    {
        await EnsureConnectedAsync(ct);

        // Decouple UA callbacks from our async stream
        var channel = Channel.CreateUnbounded<TagValue<object>>();

        _subscription = new Subscription(_session!.DefaultSubscription)
        {
            PublishingInterval = _pubMs
        };

        foreach (var node in _nodeIds)
        {
            var mi = new MonitoredItem
            {
                StartNodeId = NodeId.Parse(node),
                SamplingInterval = _sampleMs,
                QueueSize = (uint)_queue,
                DiscardOldest = true
            };

            // ✅ Correct notification handler
            mi.Notification += (item, e) =>
            {
                foreach (var change in item.DequeueValues())
                {
                    var dv = change.Value;
                    var ts = dv.SourceTimestamp == DateTime.MinValue
                        ? DateTime.UtcNow
                        : dv.SourceTimestamp.ToUniversalTime();

                    var tv = new TagValue<object>(
                        node,
                        dv.Value,
                        ts,
                        dv.StatusCode.ToString());

                    channel.Writer.TryWrite(tv);
                }
            };

            _subscription.AddItem(mi);
        }

        _session.AddSubscription(_subscription);
        // ✅ Use async create
        await _subscription.CreateAsync();

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"[Reader] OPC UA subscription created at {_pubMs}ms. Monitored {_nodeIds.Length} nodes.");
        Console.ResetColor();

        foreach (var node in _nodeIds)
            Console.WriteLine($"[Reader] Subscribed to NodeId: {node}");

        try
        {
            while (!ct.IsCancellationRequested)
            {
                if (await channel.Reader.WaitToReadAsync(ct))
                {
                    while (channel.Reader.TryRead(out var next))
                    {
                        Console.ForegroundColor = next.Quality.Contains("Good", StringComparison.OrdinalIgnoreCase)
                            ? ConsoleColor.Green
                            : ConsoleColor.Yellow;

                        Console.WriteLine(
                            $"[Reader] UPDATE: node='{next.NodeId}' value='{FormatValue(next.Value)}' ts='{next.ServerTimestamp:O}' quality='{next.Quality}'");
                        Console.ResetColor();

                        yield return next;
                    }
                }
            }
        }
        finally
        {
            channel.Writer.TryComplete();
        }
    }

    private async Task EnsureConnectedAsync(CancellationToken ct)
    {
        if (_session is { Connected: true }) return;

        _appConfig = new ApplicationConfiguration
        {
            ApplicationName = "IgnitionHeartbeatMonitor",
            ApplicationType = ApplicationType.Client,
            SecurityConfiguration = new SecurityConfiguration
            {
                AutoAcceptUntrustedCertificates = true, // dev/lab
            },
            ClientConfiguration = new ClientConfiguration()
        };

        // ✅ async validate
        await _appConfig.ValidateAsync(ApplicationType.Client);

        // ✅ fix argument order (all unnamed or all named)
        var selectedEndpoint = CoreClientUtils.SelectEndpoint(_endpointUrl, false, 15000);
        var endpointConfig = EndpointConfiguration.Create(_appConfig);
        var endpoint = new ConfiguredEndpoint(null, selectedEndpoint, endpointConfig);

        // Anonymous by default; swap to username/password if needed
        var identity = new UserIdentity(new AnonymousIdentityToken());

        // NOTE: If your package supports SessionFactory, prefer it.
        _session = await Session.Create(
            _appConfig,
            endpoint,
            false,
            "IgnitionHeartbeatMonitor",
            60000,
            identity,
            null);

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"[Reader] Connected to OPC UA endpoint: {_endpointUrl} (SecurityPolicy={selectedEndpoint.SecurityPolicyUri}, Mode={selectedEndpoint.SecurityMode})");
        Console.ResetColor();
    }

    private static string FormatValue(object? v)
        => v switch
        {
            null => "null",
            byte[] bytes => $"blob({bytes.Length})",
            _ => v?.ToString() ?? "<null>"
        };

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_subscription != null)
            {
                await _subscription.DeleteAsync(true);
                _subscription.Dispose();
                Console.WriteLine("[Reader] Subscription disposed.");
            }

            if (_session != null)
            {
                await _session.CloseAsync();
                _session.Dispose();
                Console.WriteLine("[Reader] Session closed.");
            }
        }
        catch
        {
            // keep shutdown clean
        }
    }
