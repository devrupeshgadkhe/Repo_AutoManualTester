# Auto Manual Tester

A Windows desktop application for exploring and testing other Windows desktop applications through UI Automation.

## Project status

Initial scaffold. The first milestone is a safe application launcher and UI Automation inspector. Automated exploration, verification, evidence capture, and reporting will be added incrementally.

## Requirements

- Windows 10/11 x64
- .NET 10 SDK for development

## Build

```powershell
dotnet restore AutoManualTester/AutoManualTester.csproj
dotnet build AutoManualTester/AutoManualTester.csproj -c Release
```

## Safety

The tester only attaches to the application process selected by the user. Destructive actions and real business transactions must require explicit approval. GitHub-hosted CI builds the application; interactive desktop testing requires a Windows machine with an interactive session.
