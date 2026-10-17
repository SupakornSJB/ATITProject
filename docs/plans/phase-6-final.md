# Phase 6 — Mutual TLS Security for the Embedded gRPC Server

## Overview

Phase 6 adds **mutual TLS (mTLS)** to the Unity dedicated server's embedded gRPC endpoint.
No game logic changes were made. The only goal is securing the gRPC channel between the
ASP.NET relay server and the Unity dedicated server so that only an authorised peer holding
a certificate signed by the project CA can send events.

---

## Problem Being Solved

Before Phase 6, the Unity gRPC server (port 9090) accepted any TCP connection and relied
on a shared-secret string in the gRPC request metadata for authentication. This had two
weaknesses:

1. Anyone who could reach port 9090 could attempt to connect and probe the service.
2. The shared secret was a string in config — a log file, environment variable dump, or
   accidental config commit would expose it.

---

## Threat Model

| Threat | Before Phase 6 | After Phase 6 |
|---|---|---|
| Attacker reaches port 9090 and sends gRPC events | Blocked only by shared secret in metadata | TLS handshake fails — no gRPC message ever delivered |
| Shared secret leaks from config or logs | Attacker can craft arbitrary gRPC calls | Secret removed entirely — irrelevant |
| Man-in-the-middle on ASP.NET → Unity channel | No server certificate — MITM possible | Both sides validate each other's certificate |

---

## What Changed

### 1. `GrpcServerHost.cs` — mTLS via `Grpc.Core.SslServerCredentials`

The `Start()` signature changed from:

```csharp
Start(int port)
```

to:

```csharp
Start(int port, string certPath, string keyPath, string caCertPath)
```

At startup, if all three certificate files exist on disk, `SslServerCredentials` is
constructed with the Unity server certificate, its private key, and the CA certificate
used to validate incoming client certificates. The credential mode is set to
`RequestAndRequireAndVerify` — Kestrel rejects any client that does not present a valid
certificate signed by the project CA.

If the certificate files are missing at startup, the server falls back to
`ServerCredentials.Insecure` and logs a warning. This allows the server to run in
development without certificates while making the missing-cert condition visible.

```csharp
var keyCertPair = new KeyCertificatePair(
    File.ReadAllText(certPath),
    File.ReadAllText(keyPath));
credentials = new SslServerCredentials(
    new[] { keyCertPair },
    File.ReadAllText(caCertPath),
    SslClientCertificateRequestType.RequestAndRequireAndVerify);
```

The gRPC server itself (`Grpc.Core.Server`) is unchanged — only the credentials passed
to it changed.

### 2. `IoTGameServer.cs` — Facade signature update

`Initialize()` signature changed from:

```csharp
Initialize(int grpcPort, string secret)
```

to:

```csharp
Initialize(
    int    grpcPort   = 9090,
    string certPath   = "Assets/Certs/unity-server.crt",
    string keyPath    = "Assets/Certs/unity-server.key",
    string caCertPath = "Assets/Certs/ca.crt")
```

The `SharedSecret` property is removed entirely. Developers no longer configure a secret.

### 3. `ServerBootstrap.cs` — New CLI arguments

Three new command-line arguments replace `-sharedSecret`:

| Argument | Default | Description |
|---|---|---|
| `-grpcCert` | `Assets/Certs/device.crt` | Path to Unity server certificate (PEM) |
| `-grpcKey` | `Assets/Certs/device.key` | Path to Unity server private key (PEM) |
| `-caCert` | `Assets/Certs/ca.crt` | Path to CA certificate (PEM) |

Parsed in the same loop as `-grpcPort` and `-gamePort`, then forwarded to
`IoTGameServer.Initialize()`.

Run command:
```bash
./ATITProject.x86_64 \
  -batchmode -nographics \
  -grpcPort 9090 \
  -gamePort 7777 \
  -grpcCert  /path/to/Certs/device.crt \
  -grpcKey   /path/to/Certs/device.key \
  -caCert    /path/to/Certs/ca.crt \
  -logFile server.log
```

### 4. `IoTGameServiceImpl.cs` — Shared secret validation removed

The `ValidateSecret(ServerCallContext)` method and all calls to it are deleted from
all three RPC handlers (`SendInputEvent`, `SendInputBatch`, `SendEnvironmentEvent`,
`SendAdminCommand`). Authentication is now guaranteed by the TLS handshake — if
execution reaches a service method, the client certificate has already been verified.

### 5. `Assets/link.xml` — New IL2CPP stripping exemptions

Two assemblies added to prevent IL2CPP from stripping X.509 certificate internals
during a dedicated server build:

```xml
<assembly fullname="System.Security.Cryptography.X509Certificates" preserve="all"/>
<assembly fullname="System.Security.Cryptography.Pkcs"             preserve="all"/>
```

### 6. `Assets/Certs/` — New directory

| File | Git status | Purpose |
|---|---|---|
| `ca.crt` | Committed | Public CA certificate — safe to share |
| `device.crt` | Gitignored (`*.crt`) | Unity server certificate |
| `device.key` | Gitignored (`*.key`) | Unity server private key |

`.gitignore` in `Assets/Certs/` ignores all `*.key` and `*.crt` files and explicitly
un-ignores `ca.crt`:

```
*.key
*.crt
!ca.crt
```

---

## Certificate Generation

Certificates are generated by the firmware repository's `gen_device_cert.sh` script,
which signs device certificates against the shared project CA.

```bash
# Run in ATITFinalProject-firmware/
./scripts/gen_device_cert.sh unity-server

# Copy results to Unity project
cp certs/unity-server/device.crt path/to/ATITProject/Assets/Certs/device.crt
cp certs/unity-server/device.key path/to/ATITProject/Assets/Certs/device.key
cp certs/ca.crt                   path/to/ATITProject/Assets/Certs/ca.crt
```

The same CA signs:
- ESP32 device certificates (MQTT client auth to HiveMQ)
- ASP.NET relay server certificate (gRPC client cert presented to Unity)
- Unity server certificate (gRPC server cert, also used for client auth if needed)

All three components share one CA, so the trust store only ever contains a single root.

---

## Architecture Decision: Why mTLS

The shared secret approach validated identity *inside* the application, after the TCP
and TLS connection was already established. An attacker who could reach port 9090 could
probe the service, attempt dictionary attacks on the secret, or exploit gRPC parsing
bugs before the secret check was reached.

mTLS pushes authentication to the TLS handshake — the very first thing that happens on
a new connection. An unauthenticated peer cannot complete a TCP/TLS handshake. The Unity
server never allocates a gRPC context, never parses a protobuf message, and never
executes any service code for an uncertified peer. The private key never appears in
config files, environment variables, or logs.

---

## Implementation Note: `Grpc.Core` vs Kestrel

The implementation uses `Grpc.Core.Server` (C-core binding) with `SslServerCredentials`
rather than ASP.NET Core Kestrel hosting. The `Grpc.Core` library has built-in TLS
support via C-core's BoringSSL, which handles certificate loading from PEM strings
directly — no PFX conversion needed and no ASP.NET Core hosting dependencies.

This was chosen because:
- `Grpc.Core` was already in use from prior phases
- PEM files are natively supported without the Windows-specific PFX export workaround
  that `X509Certificate2.CreateFromPemFile` + Kestrel requires
- Fewer new DLL dependencies in the Unity plugins folder

---

## Demo Attack: Unauthorised gRPC Connection

To verify the security boundary, attempt to connect without a client certificate:

```bash
# Attempt plaintext — rejected
grpcurl -plaintext localhost:9090 list

# Attempt TLS without client cert — TLS handshake fails
grpcurl -insecure localhost:9090 list
```

Both fail before any gRPC service code runs — the TLS layer rejects the connection
independently of application logic.

---

## Files Changed in Phase 6

| File | Change |
|---|---|
| `Assets/IoTGame/Core/GrpcServerHost.cs` | New `certPath`/`keyPath`/`caCertPath` parameters; `SslServerCredentials` instead of `ServerCredentials.Insecure` |
| `Assets/IoTGame/Core/IoTGameServer.cs` | `Initialize()` signature updated; `SharedSecret` removed |
| `Assets/Scripts/Server/ServerBootstrap.cs` | Parses `-grpcCert`, `-grpcKey`, `-caCert` CLI args; removed `-sharedSecret` |
| `Assets/IoTGame/Core/IoTGameServiceImpl.cs` | `ValidateSecret()` removed from all RPC methods |
| `Assets/link.xml` | Added X509 and PKCS stripping exemptions |
| `Assets/Certs/ca.crt` | New — CA public certificate, committed |
| `Assets/Certs/.gitignore` | New — ignores `*.key` and `*.crt`, un-ignores `ca.crt` |

---

## Testing Checklist

```
[ ] gen_device_cert.sh unity-server produces device.crt and device.key
[ ] Certs copied to Assets/Certs/ correctly
[ ] GrpcServerHost.Start() logs "[gRPC] Starting with mTLS on port 9090"
[ ] ASP.NET relay presents valid client cert → gRPC calls succeed end-to-end
[ ] grpcurl without cert → TLS handshake failure logged, no gRPC message processed
[ ] No ValidateSecret() calls remain in IoTGameServiceImpl
[ ] IL2CPP dedicated server build succeeds with updated link.xml
[ ] Server starts via CLI with -grpcCert, -grpcKey, -caCert arguments
[ ] Game functions normally (input, environment, admin) over mTLS channel
```
