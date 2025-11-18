# 🚦 Ignition Heartbeat Monitor (C# + OPC UA)
### *(Work in Progress — More Sections Coming Soon)*

This repository contains a **C# application** designed to monitor an **Ignition Gateway heartbeat** using **OPC UA subscriptions**.  
The solution provides a foundation for building a reliable health-monitoring pipeline that can detect:

- Late heartbeats  
- Stalled / missing heartbeats  
- Gateway or tag failure  
- Communication interruptions  

The project also includes the ability to publish heartbeat results to multiple targets (Console, Azure Function, IoT Hub, etc.).

---

# 📁 Project Structure

```
IgnitionHeartbeatMonitor/
│
├── config/
│     ├── apps.json           # Application configuration (OPC UA nodes, rules, publishers)
│     └── apps.json.md        # Documentation for the JSON schema
│
├── Ignition/                 # Screenshots documenting Ignition configuration
│     ├── HeartBeatTag.png
│     ├── HeartBeatTag1.png
│     ├── HeartBeatTag2.png
│     ├── HeartBeatTag3.png
│     ├── HeartBeatTag4.png
│     ├── HeartBeatTag5.png
│     └── HeartBeatTag6.png
│
├── Program.cs                # Entry point for the C# heartbeat client
├── IgnitionHeartbeatMonitor.csproj
└── Readme.md                 # Project documentation (this file)
```

---

# 🧩 Solution Overview

The solution consists of **three coordinated pieces**:

---

## **1. Heartbeat Generation in Ignition**

Inside Ignition:

- A boolean memory tag `[default]HeartBeat/HeartBeat` is toggled every second.
- The toggle is driven by a **Gateway Timer Script**.
- An expression tag `[default]HeartBeat/Heartbeat_Age_Seconds` computes seconds since the last update.
- An alarm is triggered if the age exceeds a configured threshold.

This provides a stable internal heartbeat signal for external systems.

---

## **2. OPC UA Exposure**

Ignition’s built-in OPC UA server is used to expose the tags externally.

### Required Setting  
Enable:

```
Config → OPC UA → Settings → Advanced → Expose Tag Providers
```

Once enabled, the tags become accessible under:

```
ns=1;s=[default]HeartBeat/HeartBeat
ns=1;s=[default]HeartBeat/Heartbeat_Age_Seconds
```

These NodeIds are consumed by the C# application.

---

## **3. C# Heartbeat Monitoring Application**

The C# app performs the following:

- Connects to Ignition’s OPC UA server  
- Subscribes to the heartbeat tag  
- Monitors update frequency  
- Compares observed timing with configured thresholds  
- Publishes alerts or statuses  
- Supports multiple publishers (console, HTTP, Azure)  

All behavior is controlled through the configuration file.

---

# 📝 apps.json Configuration

The app is controlled using the JSON file located at:

```
config/apps.json
```

### Example:

```json
{
  "OpcUa": {
    "EndpointUrl": "opc.tcp://localhost:62541/UA/IgnitionOPCUAServer",
    "SecurityMode": "None",
    "SecurityPolicy": "None",
    "Username": "",
    "Password": "",
    "NodeIds": {
      "Heartbeat": "ns=1;s=[default]HeartBeat/HeartBeat",
      "HeartbeatAgeSeconds": "ns=1;s=[default]HeartBeat/Heartbeat_Age_Seconds"
    },
    "Subscription": {
      "Enabled": true,
      "PublishingIntervalMs": 1000,
      "SamplingIntervalMs": 1000,
      "QueueSize": 2
    },
    "Polling": {
      "Enabled": false,
      "IntervalMs": 1000
    }
  },
  "HeartbeatRules": {
    "ExpectedPeriodMs": 1000,
    "LateThresholdMs": 3000,
    "StallThresholdMs": 10000
  },
  "Publisher": {
    "Kind": "Multi",
    "Targets": [
      { "Type": "Console" },
      {
        "Type": "Http",
        "EndpointUrl": "https://<your-azure-function-or-iothub-endpoint>",
        "AuthHeader": "Bearer <your-token-or-SAS-key>"
      }
    ]
  }
}
```

### 🔍 What this configuration controls

| Section | Purpose |
|--------|---------|
| `OpcUa` | How the C# app connects to Ignition |
| `NodeIds` | Which tags to monitor |
| `Subscription` | Controls OPC UA behavior |
| `HeartbeatRules` | Defines timing thresholds |
| `Publisher` | Defines where output is sent |

---

# 🖼️ Ignition Configuration (Screenshots Included)

The `/Ignition/` folder contains documentation showing:

- How to create the **heartbeat tag**
- How to add the **age expression**
- How to configure the **gateway timer script**
- How to create the **alarm**
- How to enable **Expose Tag Providers**
- How to test tags in **OPC Quick Client**

These screenshots guide you step-by-step.

---

# 🚧 Work in Progress

This repository is **actively evolving**.  
Upcoming additions:

- Implementation of `IgnitionOpcUaClient.cs`
- Cloud publishing pipeline
- Architecture diagrams
- Full setup guide
- Test suite
- Logging framework
- Packaging instructions

If you want any of these next, just ask.

---

# ✔️ Getting Started (Coming Soon)
A detailed running guide will be added after the OPC client class is included.

---
