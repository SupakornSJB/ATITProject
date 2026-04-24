# Unity Editor Setup Checklist

## 1. gRPC DLLs (one-time, server build prerequisite)

> **Note:** The DLLs are already committed to `Assets/Plugins/grpc/`. If you cloned the
> repo and the folder is present, skip straight to the Inspector settings in step 4.
> Only follow steps 1–3 if you need to set up from scratch.

**Why `Grpc.Core` and not `Grpc.AspNetCore`:** `Grpc.AspNetCore` requires .NET 8/9 runtime.
Unity's compilation pipeline targets `netstandard2.1`. The only compatible option is the
legacy `Grpc.Core` package which ships `netstandard2.0` managed DLLs + a native extension.

### Step 1 — Install Grpc.Tools to get the NuGet cache populated

Create a throwaway console project to pull the packages into the NuGet cache:

```powershell
mkdir grpc-extractor && cd grpc-extractor
dotnet new console -f net9.0
dotnet add package Grpc.Core
dotnet add package Google.Protobuf
dotnet restore
cd ..
```

### Step 2 — Copy managed DLLs from NuGet cache to `Assets/Plugins/grpc/`

```powershell
# Google.Protobuf (netstandard2.0)
$pbVer = (ls "$env:USERPROFILE\.nuget\packages\google.protobuf\" | Select -Last 1).Name
Copy-Item "$env:USERPROFILE\.nuget\packages\google.protobuf\$pbVer\lib\netstandard2.0\Google.Protobuf.dll" "Assets\Plugins\grpc\"

# Grpc.Core.Api (netstandard2.0)
$apiVer = (ls "$env:USERPROFILE\.nuget\packages\grpc.core.api\" | Select -Last 1).Name
Copy-Item "$env:USERPROFILE\.nuget\packages\grpc.core.api\$apiVer\lib\netstandard2.0\Grpc.Core.Api.dll" "Assets\Plugins\grpc\"

# Grpc.Core managed DLL (netstandard2.0)
$coreVer = (ls "$env:USERPROFILE\.nuget\packages\grpc.core\" | Select -Last 1).Name
Copy-Item "$env:USERPROFILE\.nuget\packages\grpc.core\$coreVer\lib\netstandard2.0\Grpc.Core.dll" "Assets\Plugins\grpc\"

# Grpc.Core native binary (Windows x64)
# IMPORTANT: must be renamed from grpc_csharp_ext.x64.dll → grpc_csharp_ext.dll
# Unity fails to recognize the native plugin with the .x64 infix in the name.
Copy-Item "$env:USERPROFILE\.nuget\packages\grpc.core\$coreVer\runtimes\win-x64\native\grpc_csharp_ext.x64.dll" "Assets\Plugins\grpc\grpc_csharp_ext.dll"
```

### Step 3 — Set Unity Inspector settings for each DLL

Open each DLL in `Assets/Plugins/grpc/` in the Unity Inspector and apply these settings:

| DLL | Include Platforms | Define Constraints | CPU |
|-----|-------------------|--------------------|-----|
| `Google.Protobuf.dll` | Editor + Standalone | — | Any |
| `Grpc.Core.Api.dll` | Editor + Standalone | — | Any |
| `Grpc.Core.dll` | Editor + Standalone | `UNITY_SERVER` | Any |
| `grpc_csharp_ext.dll` | Editor + Standalone | — | x86_64 |

> **Unity 6 quirk:** In Unity 6 Build Profiles, the `Server` platform checkbox does not
> appear in the plugin Inspector. Use **Define Constraints** (`UNITY_SERVER`) instead to
> restrict `Grpc.Core.dll` to server builds only. You must still check at least one
> platform (e.g. Editor) or Unity ignores the DLL entirely.

## 2. Generate gRPC stubs (only needed if proto files change)

> The generated stubs are already committed to `Assets/Scripts/Generated/`. Only
> regenerate if `Proto/game_controller_v2.proto` changes.

```powershell
$toolsVer = (ls "$env:USERPROFILE\.nuget\packages\grpc.tools\" | Select -Last 1).Name
$plugin = "$env:USERPROFILE\.nuget\packages\grpc.tools\$toolsVer\tools\windows_x64\grpc_csharp_plugin.exe"

protoc `
  --csharp_out=Assets/Scripts/Generated `
  --grpc_out=Assets/Scripts/Generated `
  "--plugin=protoc-gen-grpc=$plugin" `
  --proto_path=Proto `
  Proto/game_controller_v2.proto
```

Output files in `Assets/Scripts/Generated/` — do **not** edit manually.

## 3. NetworkManager GameObject

1. Create an empty GameObject named `NetworkManager` in `SampleScene`
2. Add component: `NetworkManager`
3. Add component: `UnityTransport`
4. In `NetworkManager` Inspector:
   - **Network Transport** → assign the `UnityTransport` component
   - Enable **Connection Approval** checkbox

## 4. PlayerBird Prefab

1. Create a Cube GameObject, rename to `PlayerBird`
2. Add components:
   - `NetworkObject`
   - `Rigidbody` — enable `useGravity`, freeze **X and Z rotation** constraints
   - `PlayerBird` script
   - Assign a URP `UniversalRenderPipeline/Lit` material (color will be overridden at runtime)
3. Save as a prefab in `Assets/Prefabs/`

## 5. Manager GameObjects in SampleScene

Create one persistent GameObject per manager, each needs `NetworkObject` + the script:

| GameObject name          | Scripts to attach                              |
|--------------------------|------------------------------------------------|
| `PlayerDistinguisherManager` | `NetworkObject`, `PlayerDistinguisherManager` |
| `BirdSpawnManager`       | `NetworkObject`, `BirdSpawnManager`            |
| `IoTInputManager`        | `NetworkObject`, `IoTInputManager`             |

For `BirdSpawnManager` → assign the `PlayerBird` prefab to **Bird Prefab** in the Inspector.

## 6. ServerBootstrap GameObject in SampleScene

1. Create empty GameObject `ServerBootstrap`
2. Add component: `ServerBootstrap` script
3. In the Inspector (client build settings):
   - **Player Id** — unique string per client build (`player-1` … `player-4`)
   - **Server Address** — IP of the dedicated server
   - **Client Game Port** — `7777` (or match `-gamePort` arg)

## 7. Build Settings

### Dedicated Server build
1. **File → Build Settings → Platform** → switch to **Dedicated Server**
2. Enable **Development Build** for testing
3. Add `SampleScene` (index 0)
4. Build

### Host build (local testing — no separate dedicated server needed)
A host runs both the server and one client in the same process. No gRPC DLLs or
stub generation required. IoT button input is unavailable in this mode (Kestrel is
not started); use it to verify NGO spawning, color assignment, and gravity only.

1. **File → Build Settings → Platform** → Standalone (Windows / Mac / Linux)
2. Add `SampleScene` (index 0)
3. Select the `ServerBootstrap` GameObject in the scene:
   - Tick **Start As Host**
   - Set **Player Id** to the host player's ID (e.g. `player-1`)
   - **Server Address** and **Client Game Port** are ignored in host mode
4. Build (or run directly in Play Mode in the Editor)

> **In-Editor Play Mode**: tick **Start As Host** on `ServerBootstrap` and press Play.
> Additional clients can connect from standalone builds pointed at `127.0.0.1:7777`.

### Client build (connects to a dedicated server or host)
1. **File → Build Settings → Platform** → Standalone (Windows / Mac / Linux)
2. Add `SampleScene` (index 0)
3. On `ServerBootstrap`: leave **Start As Host** unticked, set **Player Id**, **Server Address**, and **Client Game Port**
4. Build — repeat for each player ID

## 8. Scene NGO Registration

All `NetworkObject` prefabs that are spawned at runtime must be registered in the
`NetworkManager` prefab list:

1. Select `NetworkManager` GameObject
2. In `NetworkManager` Inspector → **Network Prefabs** → add `PlayerBird` prefab

## 9. Obsolete Scripts (do not attach to any GameObject)

These files exist but are superseded or out of scope for the current POC:
- `BirdController.cs` — replaced by `PlayerBird.cs`
- `BirdDistinguisher.cs` — functionality is in `PlayerBird.cs`
- `PlayerDistinguisher.cs` — superseded
- `PlayerMovement.cs` — not used (birds use Rigidbody, not CharacterController)
- `PipeController.cs` — pipes are out of scope for POC
- `PipeSpawner.cs` — pipes are out of scope for POC