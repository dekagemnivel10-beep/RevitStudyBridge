# Revit Study Bridge

Experimental local request -> Revit API -> response integration for architecture studies.
The initial operation reads aggregate element counts from the active document.
Execution requires an explicit manual command and a confirmation inside Revit.

## Status

Source preparation only. Compilation, protocol checks and native Revit execution
have NOT been validated. The synthetic example response is not evidence of a
real model or a successful integration. No binaries are distributed.

## Requirements

- Windows with Autodesk Revit 2025, installed and licensed independently.
- .NET 8 SDK for compilation.
- Local RevitAPI and RevitAPIUI references; Autodesk binaries are not included.

## Build and Install

```powershell
dotnet build src/RevitStudyBridge/RevitStudyBridge.csproj -c Release
dotnet run --project tests/ProtocolChecks/ProtocolChecks.csproj
```

For a non-default Revit installation, provide `-p:RevitInstallDir="INSTALL_DIRECTORY"`.
Edit the assembly placeholder in `RevitStudyBridge.addin.example`, rename that
copy to `RevitStudyBridge.addin`, and place it in the Revit 2025 per-user Addins
directory. Do not replace other manifests. Inspect the source before deciding
whether to trust an unsigned add-in. Do not disable Revit security globally.

## Local Request and Response

Create `%LOCALAPPDATA%/RevitStudyBridge` yourself, without links or junctions.
Place a request matching `examples/request.synthetic.json` there as `request.json`.
Use a new, non-empty UUID for each request. Open a disposable model copy in Revit,
then invoke the external command and inspect its confirmation before proceeding.

Only `summarize_active_document` is supported. The parser rejects additional or
duplicate fields, other operations, malformed requests and oversized inputs.
Accepted requests move once into `processed/<id>.request.json`. Responses are
written once into `responses/<id>.json`. No automatic polling or retry occurs.
The active document is selected by the user, not by a request path or model name.

Counts are limited to walls, doors, windows, floors and rooms. An inspection
stops after 100,000 elements or five seconds between iterations. This is a
cooperative deadline, NOT a hard timeout around individual Revit API calls.

## Boundaries and Limitations

- No transactions, model changes, file opening, saving or rendering.
- No arbitrary commands, scripts, assemblies, URLs or output paths from requests.
- No automatic network access, publication or telemetry.
- Responses omit model titles, paths, geometry, element IDs and room names.
- The mailbox is for the trusted local Windows user, not an authenticated service
  or isolation boundary against that same user.
- Reparse checks do not prove global immutability or prevent every filesystem race.
- Response writes are not transactional. If a write is interrupted, a partial
  response can remain; consumers must validate complete JSON and the request ID.
  Do not automatically delete or replay a claimed request.
- Revit versions other than 2025 have not been evaluated.

## Distribution

This standalone tree contains new generic source and synthetic examples only.
No customer models, private project files, history, credentials, toolchains or
Autodesk libraries are included. The generic source and documentation are
licensed under the MIT License, copyright 2026 André Vieira. This license does
not cover Autodesk products or proprietary API binaries; see `DISTRIBUTION.md`.
