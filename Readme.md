# 🚦 Ignition Heartbeat Monitor (C# + OPC UA)
### *(Work in Progress — More Sections Coming Soon)*

This repository contains a **C# application** designed to monitor an **Ignition Gateway heartbeat** using **OPC UA subscriptions**.

---

# 📁 Project Structure

```
IgnitionHeartbeatMonitor/
│
├── config/
│     ├── apps.json
│     └── apps.json.md
│
├── Ignition/
│     ├── HeartBeatTag.png
│     ├── HeartBeatTag1.png
│     ├── HeartBeatTag2.png
│     ├── HeartBeatTag3.png
│     ├── HeartBeatTag4.png
│     ├── HeartBeatTag5.png
│     └── HeartBeatTag6.png
│
├── Program.cs
├── IgnitionHeartbeatMonitor.csproj
└── Readme.md
```

---

# 🧩 Solution Overview

The solution consists of **three coordinated pieces**.

---

# 🔹 1. Heartbeat Generation in Ignition

This section documents how the Ignition Gateway produces a reliable heartbeat.

---

## **1.1 Create the HeartBeat Tag**

![HeartBeat Tag](Ignition/HeartBeatTag.png)

Create a boolean memory tag at:

```
[default]HeartBeat/HeartBeat
```

This tag toggles every second.

---

## **1.2 Implement Gateway Timer Script**

![Gateway Timer Script](Ignition/HeartBeatTag1.png)

The Gateway Timer Script toggles the Boolean heartbeat:

```python
tagPath = "[default]HeartBeat/HeartBeat"

try:
    currentValue = system.tag.readBlocking([tagPath])[0].value
    newValue = not bool(currentValue)
    system.tag.writeBlocking([tagPath], [newValue])
except Exception as e:
    system.util.getLogger("Heartbeat").error("Toggle failed: %s" % e)
```

Runs every 1000 ms.

---

## **1.3 Create Heartbeat_Age_Seconds Tag**

![Heartbeat Age Tag](Ignition/HeartBeatTag2.png)

Expression:

```python
dateDiff(
    {[.]HeartBeat.Timestamp},
    now(),
    "second"
)
```

This shows how long it has been since the last heartbeat.

---

## **1.4 Configure Heartbeat Alarm**

![Heartbeat Alarm](Ignition/HeartBeatTag3.png)

Alarm triggers when heartbeat stalls for more than **5 seconds**.

---

## **1.5 Enable OPC UA Tag Provider Exposure**

![OPC UA Expose Providers](Ignition/HeartBeatTag4.png)

Enable:
```
Expose Tag Providers
```

---

## **1.6 Verify via OPC Quick Client**

![Quick Client](Ignition/HeartBeatTag5.png)

Your tags should appear under:

```
Tag Providers → default → HeartBeat
```

Final confirmation:

![Final Quick Client](Ignition/HeartBeatTag6.png)

---

# 🔹 2. OPC UA Exposure

Once enabled, tags are visible as:

```
ns=1;s=[default]HeartBeat/HeartBeat
ns=1;s=[default]HeartBeat/Heartbeat_Age_Seconds
```

---

# 🔹 3. C# Monitoring Application

This application connects to Ignition’s OPC UA endpoint and monitors:

- Heartbeat toggle rate  
- Heartbeat age  
- Late/stalled conditions  

Publishes results using configured targets.

---

# 📝 apps.json Configuration

```
config/apps.json
```

Contents:

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

---

# 🚧 Work in Progress

Upcoming additions:

- OPC UA Client implementation  
- Publisher pipeline  
- Azure integration  
- Architecture diagrams  
- Full "Getting Started" guide  

---
