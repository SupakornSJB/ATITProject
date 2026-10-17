# Unity Editor Setup — Phase 6 (mTLS gRPC Security)

> Assumes Phases 1–5 are already set up and working.
> Phase 6 has **no Unity Editor changes** — no new GameObjects, no new prefabs,
> no Inspector wiring. All changes are code and cert files only.
> The cross-repo [setup guide](https://github.com/SupakornSJB/ATITFinalProject/blob/main/docs/setup.md)
> covers the whole system; this page is the Unity part.

---

## What You Need to Do

### Step 1 — Generate certificates (firmware repo)

Run from the `atit_micropython/` repository root (Git Bash on Windows), after
`scripts/gen_ca.sh` has created the project CA:

```bash
bash scripts/gen_device_cert.sh unity-server
```

This creates `certs/unity-server/device.crt` and `certs/unity-server/device.key`
signed by the project CA.

### Step 2 — Copy certs into the Unity project

```bash
# From atit_micropython/
cp certs/unity-server/device.crt  ../ATITProject/Assets/Certs/device.crt
cp certs/unity-server/device.key  ../ATITProject/Assets/Certs/device.key
cp certs/ca.crt                   ../ATITProject/Assets/Certs/ca.crt
```

`Assets/Certs/.gitignore` keeps all three out of git — every deployment generates its
own CA, so no certificate is committed.

### Step 3 — Verify the Certs folder

```
Assets/Certs/
├── .gitignore    ← committed
├── ca.crt        ← git-ignored
├── device.crt    ← git-ignored
└── device.key    ← git-ignored (never commit)
```

### Step 4 — Build the dedicated server

In Unity: **File → Build Settings → Dedicated Server → Build**, or from the command line
with `-executeMethod BuildServer.Build` (output: `Builds/Server/ATITProject.exe`).
`CertPostprocessor` in `Assets/Editor/BuildServer.cs` copies the three cert files to
`<build>/Assets/Certs/` next to the binary.

### Step 5 — Run the server

```powershell
Builds/Server/ATITProject.exe -batchmode -nographics `
  -grpcPort 9090 -gamePort 7777 `
  -grpcCert Assets/Certs/device.crt `
  -grpcKey  Assets/Certs/device.key `
  -caCert   Assets/Certs/ca.crt `
  -logFile  server.log
```

If cert paths are omitted, the server defaults to `Assets/Certs/device.crt`,
`Assets/Certs/device.key` and `Assets/Certs/ca.crt`. Relative paths resolve against the
**working directory** the server is started from, not the binary's folder: started from the
project root (as above) it uses the project's own `Assets/Certs/`; started from inside
`Builds/Server/` it uses the copies `CertPostprocessor` placed there. Absolute paths avoid
the ambiguity.

---

## What Changed in Code (no editor action needed)

| File | Change |
|---|---|
| `GrpcServerHost.cs` | `Grpc.Core.Server` now uses `SslServerCredentials` with `RequestAndRequireAndVerify` — a client certificate signed by `ca.crt` is required |
| `IoTGameServer.cs` | `Initialize()` takes cert paths instead of a shared secret |
| `ServerBootstrap.cs` | Parses `-grpcCert`, `-grpcKey`, `-caCert` CLI args; `-sharedSecret` removed |
| `IoTGameServiceImpl.cs` | Shared-secret metadata check removed — authentication is the TLS handshake |
| `link.xml` | Stripping exemptions for X509 / Pkcs |
| `BuildServer.cs` | `CertPostprocessor` copies certs into the build output |
| `Assets/Certs/.gitignore` | Ignores all certificates and keys |

A switch to ASP.NET Core Kestrel (`Grpc.AspNetCore`, installed via NuGetForUnity) was
planned for this phase but not adopted; the server still runs on `Grpc.Core`.

---

## Verifying mTLS Works

### Success case — the relay connects with a valid cert

Confirm in `server.log`:
```
[gRPC] Starting with mTLS on port 9090
[gRPC] Server listening on port 9090
```
followed by input arriving from the relay.

### Failure case — connection without a client cert

```bash
# Should fail with a TLS handshake error before any gRPC message is sent
grpcurl -plaintext localhost:9090 list
grpcurl -insecure  localhost:9090 list
```

### Insecure fallback

If any of the three cert files is missing, the server logs
`[gRPC] Cert files not found — starting INSECURE` and accepts plain connections.
Always check for the `Starting with mTLS` line.

---

## Troubleshooting

| Symptom | Likely cause | Fix |
|---|---|---|
| `starting INSECURE` in the log | A cert file isn't found at the given path — relative paths resolve against the working directory | Pass absolute `-grpcCert` / `-grpcKey` / `-caCert` paths, or start the server from a folder that contains `Assets/Certs/` |
| Relay's calls fail with a TLS error | Relay's client cert not signed by the same `ca.crt` | Regenerate the relay cert from the same CA |
| IL2CPP build crash with no clear error | Missing stripping exemption | Confirm `link.xml` includes `System.Security.Cryptography.X509Certificates` |

---

## Testing with grpcurl

```bash
grpcurl -insecure \
  -cert path/to/client.crt \
  -key  path/to/client.key \
  localhost:9090 list
```

`-insecure` skips verifying Unity's certificate — needed because `gen_device_cert.sh` puts
the name only in the CN (no subjectAltName), which grpcurl's Go TLS stack no longer accepts
for host-name checks. The client certificate is still sent and verified by Unity. `list`
needs server reflection, which this server does not enable, so with a valid client
certificate you get a reflection error *after* a successful handshake; without one, the
handshake itself fails (see the failure case above).
