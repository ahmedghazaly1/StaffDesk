# Running tests (NFR-20)

From the repository root:

```powershell
$env:STAFFDESK_JWT_SECRET = 'LocalDev-StaffDesk-Signing-Secret-32ch'
dotnet test --nologo
```

That is the single documented test command.
