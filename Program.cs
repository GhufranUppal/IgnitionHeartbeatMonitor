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
            Console.WriteLine("[App] Ctrl+C received. Shutting down...");
        };

        await using var reader = new OpcUaTagReader(cfg);

        Console.WriteLine("[App] Starting subscription test…");
        bool firstMessageSeen = false;
        bool errorOccurred = false;

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
                // The reader already prints every update; we can add more here if needed
            }
        }
        catch (OperationCanceledException) { /* normal on Ctrl+C */ }
        catch (ServiceResultException srx)
        {
            errorOccurred = true;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[App] ❌ OPC UA error: {srx.Message}");
            Console.WriteLine($"[App] Status Code: {srx.StatusCode}");
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
        Console.WriteLine("\n[App] Press any key to exit...");
        Console.ReadKey();
    }
}
