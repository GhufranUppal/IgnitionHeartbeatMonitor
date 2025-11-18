# Ignition Heartbeat Monitoring Pipeline

This application monitors OPC UA heartbeat tags from Ignition and publishes health events to **Console** and **Azure Cloud**.

---

## Features
- OPC UA subscription or polling for tags:
  - `HeartBeat`
  - `Heartbeat_Age_Seconds`
- Health classification: **OK**, **Late**, **Stalled**, **BadQuality**
- Multi-target publishing:
  - Console output for local visibility
  - HTTP POST to Azure endpoint for cloud alarms

---

## Configuration

Edit `appsettings.json`:

### OPC UA Settings
```json
"OpcUa": {
  "EndpointUrl": "opc.tcp://localhost:62541/UA/IgnitionOPCUAServer",
  "NodeIds": {
    "Heartbeat": "ns=1;s=[default]HeartBeat/HeartBeat",
    "HeartbeatAgeSeconds": "ns=1;s=[default]HeartBeat/Heartbeat_Age_Seconds"
  },
  "Subscription": {
    "Enabled": true,
    "PublishingIntervalMs": 1000
  }
}
