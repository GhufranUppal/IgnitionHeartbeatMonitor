using Microsoft.Extensions.Configuration;
using System;
using System.Threading;
using System.Threading.Tasks;
using Opc.Ua;
using Opc.Ua.Client;

class Program
{
    static async Task Main(string[] args)
    {
        var cfg = new ConfigurationBuilder()
            .AddJsonFile("config/apps.json", optional: false)
            .AddEnvironmentVariables()
            .Build();

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
            Console.WriteLine("\n[App] Ctrl+C received. Shutting down...");
        };

        await using var reader = new OpcUaTagReader(cfg);

        Console.WriteLine("[App] Starting subscription test…");
        bool firstMessageSeen = false;
        bool errorOccurred = false;

        var ageNodeId = cfg["OpcUa:NodeIds:HeartbeatAgeSeconds"];
        TagValue<object>? age = null;
        var lastLineLength = 0;

        void ClearAgeLine()
        {
            Console.Write("\r" + new string(' ', lastLineLength) + "\r");
            lastLineLength = 0;
        }

        void RenderAge()
        {
            var quality = age?.Quality ?? "-";
            var good = quality.Contains("Good", StringComparison.OrdinalIgnoreCase);
            var line = $"Time since last Heartbeat {age?.Value?.ToString() ?? "--"} seconds";

            Console.ForegroundColor = good ? ConsoleColor.Green : ConsoleColor.Yellow;
            Console.Write("\r" + line.PadRight(lastLineLength));
            Console.ResetColor();
            lastLineLength = line.Length;
        }

        void PrintHeartbeat(TagValue<object> hb)
        {
            ClearAgeLine();
            var good = hb.Quality.Contains("Good", StringComparison.OrdinalIgnoreCase);
            var ts = hb.ServerTimestamp.ToLocalTime().ToString("HH:mm:ss");
            Console.ForegroundColor = good ? ConsoleColor.Green : ConsoleColor.Yellow;
            Console.WriteLine(good
                ? $"[Heartbeat] Heartbeat Received - Ignition Gateway Healthy | {ts}"
                : $"[Heartbeat] Heartbeat Received - quality={hb.Quality} (not healthy) | {ts}");
            Console.ResetColor();
            if (age is not null) RenderAge();
        }

        try
        {
            await foreach (var tv in reader.ReadStreamAsync(cts.Token))
            {
                if (!firstMessageSeen)
                {
                    firstMessageSeen = true;
                    Console.ForegroundColor = ConsoleColor.Blue;
                    Console.WriteLine("[App] ✅ Subscription is working. We are receiving messages from the OPC UA server.");
                    Console.ResetColor();
                }

                if (tv.NodeId == ageNodeId)
                {
                    age = tv;
                    RenderAge();
                }
                else
                {
                    PrintHeartbeat(tv);
                }
            }
        }
        catch (OperationCanceledException) { /* normal on Ctrl+C */ }
        catch (ServiceResultException srx)
        {
            errorOccurred = true;
            var statusName = StatusCodes.GetBrowseName(srx.StatusCode);
            Console.ForegroundColor = ConsoleColor.Red;
            if (IsConnectionFailure(srx.StatusCode))
            {
                Console.WriteLine($"[App] ❌ Could not connect to the OPC UA server at {cfg["OpcUa:EndpointUrl"]}");
                Console.WriteLine($"[App]    Reason: {statusName} (0x{srx.StatusCode:X8})");
                Console.WriteLine("[App]    Check:");
                Console.WriteLine("[App]      1. Is the Ignition gateway running and the OPC UA server module enabled?");
                Console.WriteLine("[App]      2. Are the host and port in OpcUa:EndpointUrl (config/apps.json) correct?");
                Console.WriteLine("[App]      3. Is a firewall blocking the port?");
            }
            else
            {
                Console.WriteLine($"[App] ❌ OPC UA error: {srx.Message}");
                Console.WriteLine($"[App]    Status: {statusName} (0x{srx.StatusCode:X8})");
            }
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            errorOccurred = true;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[App] ❌ Unhandled error: {ex.GetType().Name}");
            Console.WriteLine($"[App] Message: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"[App] Inner exception: {ex.InnerException.Message}");
            }
            Console.ResetColor();
        }

        Console.WriteLine("[App] Stopped.");

        // If we got here without seeing the first message, diagnose what went wrong
        if (!firstMessageSeen && !errorOccurred)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("[App] ⚠ No messages received from OPC UA server.");
            Console.WriteLine("[App] Check:");
            Console.WriteLine("[App]   1. Is the OPC UA server running at the configured endpoint?");
            Console.WriteLine("[App]   2. Are the NodeIds in config/apps.json correct?");
            Console.WriteLine("[App]   3. Do you have network connectivity to the server?");
            Console.ResetColor();
        }

        // Keep console open so user can see the output
        if (!Console.IsInputRedirected)
        {
            Console.WriteLine("\n[App] Press any key to exit...");
            Console.ReadKey();
        }
    }

    static bool IsConnectionFailure(uint code) => code is
        StatusCodes.BadNotConnected or
        StatusCodes.BadServerNotConnected or
        StatusCodes.BadNoCommunication or
        StatusCodes.BadCommunicationError or
        StatusCodes.BadConnectionRejected or
        StatusCodes.BadConnectionClosed or
        StatusCodes.BadTcpEndpointUrlInvalid or
        StatusCodes.BadTimeout;
}
