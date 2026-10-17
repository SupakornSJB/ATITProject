# Unity Editor Setup — Phase 6 (mTLS gRPC Security)

> Assumes Phases 1–5 are already set up and working.
> Phase 6 has **no Unity Editor changes** — no new GameObjects, no new prefabs,
> no Inspector wiring. All changes are code and cert files only.

---

## What You Need to Do

### Step 1 — Generate certificates (firmware repo)

Run from the `ATITFinalProject-firmware/` directory:

```bash
./scripts/gen_device_cert.sh unity-server
```

This creates `certs/unity-server/device.crt` and `certs/unity-server/device.key`
signed by the project CA.

### Step 2 — Copy certs into the Unity project

```bash
# From ATITFinalProject-firmware/
cp certs/unity-server/device.crt  path/to/ATITProject/Assets/Certs/device.crt
cp certs/unity-server/device.key  path/to/ATITProject/Assets/Certs/device.key
cp certs/ca.crt                   path/to/ATITProject/Assets/Certs/ca.crt
```

The `Assets/Certs/` directory is already created. `.gitignore` in that folder keeps
`device.crt` and `device.key` out of git. `ca.crt` is safe to commit.

### Step 3 — Verify the Certs folder

```
Assets/Certs/
├── .gitignore          ← already committed
├── ca.crt              ← committed (public CA cert)
├── device.crt    ← gitignored (DO NOT commit)
└── device.key    ← gitignored (DO NOT commit)
```

### Step 4 — Build the dedicated server

In Unity: **File → Build Settings → Dedicated Server → Build**.

No project setting changes are needed. The `link.xml` update is already applied in
code and will take effect on the next build.

### Step 5 — Run the server

```bash
./ATITProject.x86_64 \
  -batchmode -nographics \
  -grpcPort 9090 \
  -gamePort 7777 \
  -grpcCert  Assets/Certs/device.crt \
  -grpcKey   Assets/Certs/device.key \
  -caCert    Assets/Certs/ca.crt \
  -logFile   server.log
```

If cert paths are omitted, the server defaults to `Assets/Certs/device.crt`,
`Assets/Certs/device.key`, and `Assets/Certs/ca.crt` relative to the binary.

---

## What Changed in Code (no editor action needed)

| File | Change |
|---|---|
| `GrpcServerHost.cs` | Replaced `Grpc.Core.Server` with ASP.NET Core Kestrel + mTLS |
| `IoTGameServer.cs` | `Initialize()` now takes cert paths instead of a shared secret |
| `ServerBootstrap.cs` | Parses `-grpcCert`, `-grpcKey`, `-caCert` CLI args; removed `-sharedSecret` |
| `IoTGameServiceImpl.cs` | `ValidateSecret()` removed; all three RPCs no longer check metadata |
| `link.xml` | Added X509, Pkcs, and Kestrel stripping exemptions |
| `Assets/Certs/.gitignore` | Ignores private keys and certs except `ca.crt` |

---

## Verifying mTLS Works

### Success case — ASP.NET connects with valid cert

Confirm in `server.log`:
```
[gRPC] Server started with mTLS on port 9090
[IoTGameServiceImpl] InputBatch — N events
```

### Failure case — connection without a client cert

```bash
# Should fail with TLS handshake error before any gRPC message is sent
grpcurl -plaintext localhost:9090 list
grpcurl -insecure localhost:9090 list
```

Expected output: TLS handshake failure. No log line from `IoTGameServiceImpl`.

If the connection is accepted with no cert, Kestrel's `ClientCertificateMode` is
not being applied — check that the mTLS `listenOpts` block is reached (port not
already bound by old server instance).

### Failure case — wrong CA

If ASP.NET presents a cert signed by a different CA:
```
[gRPC] Client cert rejected: UntrustedRoot
```
The connection is dropped before any RPC is processed.

---

## Troubleshooting

| Symptom | Likely cause | Fix |
|---|---|---|
| `FileNotFoundException` on start | Cert paths wrong or files missing | Check `-grpcCert`/`-grpcKey`/`-caCert` args; verify files exist |
| `CryptographicException: No credentials` on Windows | PFX export issue | `LoadCertWithKey` does the PFX export — if it fails, the cert or key file may be malformed |
| `X509ChainTrustMode.CustomRootTrust` compile error | .NET < 5 | Unity 6 LTS targets .NET Standard 2.1 with .NET 6 runtime — this should not occur; verify scripting backend is IL2CPP and API compatibility is .NET Standard 2.1 |
| gRPC calls rejected with `StatusCode.Unavailable` | Old `Grpc.Core` DLLs in `Assets/Plugins/grpc/` conflict with Kestrel | Ensure `Grpc.AspNetCore.Server.dll` is present and `Grpc.Core` (C-core) DLLs are not |
| IL2CPP build crash with no clear error | Missing stripping exemption | Confirm `link.xml` includes `System.Security.Cryptography.X509Certificates` |

---

## ASP.NET Counterpart

The ASP.NET relay server (`/server`) must be updated to load a client certificate
and present it on the gRPC channel to Unity. That change is outside this repo.
The client cert must be signed by the same CA (`ca.crt`) that Unity is validating against.

Until the ASP.NET side is updated, you can test with `grpcurl` and a client cert:

```bash
grpcurl \
  --cacert  Assets/Certs/ca.crt \
  --cert    path/to/aspnet-client.crt \
  --key     path/to/aspnet-client.key \
  localhost:9090 list
```
