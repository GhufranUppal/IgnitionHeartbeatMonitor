
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
    private readonly MessageSecurityMode _securityMode;
    private readonly string _securityPolicy;
    private readonly string _pkiRoot;
    private readonly bool _autoAccept;

    private ApplicationConfiguration? _appConfig;
    private ISession? _session;
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
        _securityMode = Enum.TryParse<MessageSecurityMode>(opc["SecurityMode"], true, out var sm) ? sm : MessageSecurityMode.None;
        _securityPolicy = opc["SecurityPolicy"] is { Length: > 0 } sp ? sp : "None";
        _pkiRoot = opc["PkiRoot"] is { Length: > 0 } pki ? pki : "pki";
        _autoAccept = !bool.TryParse(opc["AutoAcceptUntrustedCertificates"], out var aa) || aa;

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

            // ? Correct notification handler
            mi.Notification += (item, e) =>
            {
                if (e.NotificationValue is not MonitoredItemNotification change) return;
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
        // ? Use async create
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

        var application = new ApplicationInstance
        {
            ApplicationName = "IgnitionHeartbeatMonitor",
            ApplicationType = ApplicationType.Client
        };

#pragma warning disable CS0618 // Obsolete overload kept: it creates the default PKI stores from a root path
        _appConfig = await application
            .Build($"urn:{Utils.GetHostName()}:IgnitionHeartbeatMonitor", "uri:IgnitionHeartbeatMonitor")
            .AsClient()
            .AddSecurityConfiguration("CN=IgnitionHeartbeatMonitor, O=Lab", _pkiRoot)
            .SetAutoAcceptUntrustedCertificates(_autoAccept)
            .CreateAsync();
#pragma warning restore CS0618

        // Creates the client certificate in the PKI store if missing
        await application.CheckApplicationInstanceCertificatesAsync(false);

        var selectedEndpoint = await SelectEndpointAsync(ct);
        Console.WriteLine($"[Reader] Selected endpoint: {selectedEndpoint.EndpointUrl} (Policy={selectedEndpoint.SecurityPolicyUri}, Mode={selectedEndpoint.SecurityMode})");
        var endpointConfig = EndpointConfiguration.Create(_appConfig);
        var endpoint = new ConfiguredEndpoint(null, selectedEndpoint, endpointConfig);

        // Anonymous by default; swap to username/password if needed
        var identity = new UserIdentity(new AnonymousIdentityToken());

        // NOTE: If your package supports SessionFactory, prefer it.
        _session = await DefaultSessionFactory.Instance.CreateAsync(
            _appConfig,
            endpoint,
            false,
            "IgnitionHeartbeatMonitor",
            60000,
            identity,
            null,
            ct);

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"[Reader] Connected to OPC UA endpoint: {_endpointUrl} (SecurityPolicy={selectedEndpoint.SecurityPolicyUri}, Mode={selectedEndpoint.SecurityMode})");
        Console.ResetColor();
    }

    private async Task<EndpointDescription> SelectEndpointAsync(CancellationToken ct)
    {
        using var discovery = DiscoveryClient.Create(_appConfig!, new Uri(_endpointUrl));
        var endpoints = await discovery.GetEndpointsAsync(null, ct);

        var match = endpoints.FirstOrDefault(e =>
            e.SecurityMode == _securityMode &&
            e.SecurityPolicyUri.EndsWith("#" + _securityPolicy, StringComparison.OrdinalIgnoreCase));

        if (match is null)
        {
            var available = string.Join("; ", endpoints.Select(e => $"{e.SecurityMode}/{e.SecurityPolicyUri.Split('#').Last()}"));
            throw new InvalidOperationException(
                $"Server offers no endpoint with SecurityMode={_securityMode}, SecurityPolicy={_securityPolicy}. Available: {available}. " +
                "Adjust OpcUa:SecurityMode/SecurityPolicy in config/apps.json or enable the policy on the Ignition gateway.");
        }

        // Keep the host the user configured (servers often advertise their hostname)
        var cfgUri = new Uri(_endpointUrl);
        var advertised = new UriBuilder(match.EndpointUrl) { Host = cfgUri.Host, Port = cfgUri.Port };
        match.EndpointUrl = advertised.Uri.ToString();
        return match;
    }

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
}
