# 🔥 Ignition Heartbeat Monitor  
Updated README with **working screenshot links** and **working link to HeartBeatContracts.md**.

---

## 📁 Repository Structure

```
IgnitionHeartbeatMonitor/
├── Readme.md
├── config/
│   ├── apps.json
│   └── apps.json.md
├── Ignition/
│   ├── HeartBeatTag.png
│   ├── HeartBeatTag1.png
│   ├── HeartBeatTag2.png
│   ├── HeartBeatTag3.png
│   ├── HeartBeatTag4.png
│   ├── HeartBeatTag5.png
│   └── HeartBeatTag6.png
├── src/
│   └── PipeLine/
│        ├── HeartBeatContracts.cs
│        └── HeartBeatContracts.md
└── Program.cs
```

---

## 🧩 Domain Contracts  
📄 Full documentation here:  
👉 **[HeartBeatContracts.md](src/PipeLine/HeartBeatContracts.md)**

This describes:

- `TagValue<T>`
- `HeartbeatState`
- `HeartbeatEvent`
- `ITagReader`
- `IValidator`
- `ITransformer`
- `IPublisher`

---

# ❤️ Heartbeat Generation in Ignition

## 1️⃣ Create HeartBeat tag  
![Heartbeat Tag](Ignition/HeartBeatTag.png)

---

## 2️⃣ Create Gateway Timer Script  
![Timer Script](Ignition/HeartBeatTag1.png)

---

## 3️⃣ Create Heartbeat_Age_Seconds  
![Age Seconds](Ignition/HeartBeatTag2.png)

---

## 4️⃣ Add Alarm Configuration  
![Alarm Config](Ignition/HeartBeatTag3.png)

---

## 5️⃣ Expose Tag Providers (OPC UA Settings)  
![Expose Tag Providers](Ignition/HeartBeatTag4.png)

---

## 6️⃣ Validate Tags via OPC Quick Client  
![Quick Client 1](Ignition/HeartBeatTag5.png)  
![Quick Client 2](Ignition/HeartBeatTag6.png)

---

# 🧪 apps.json Configuration

(This file drives OPC UA connection, rules, and publisher settings.)

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

# 🚧 Work In Progress  
More sections coming soon:

- OPC UA TagReader implementation  
- Validator logic  
- Event transformers  
- Azure publisher  
- Architecture diagrams  

