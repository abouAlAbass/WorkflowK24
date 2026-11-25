---
name: tamam-testing-workflow
description: >-
  Testing rules and commands for Tamam Workflow Studio. Trigger whenever writing,
  modifying, or executing backend (dotnet test) or frontend (vitest / vite build)
  tests and validations.
---

# Tamam Testing Workflow

## Anti-Monolith Rule (API Integration Tests)
- **Never** bloat `backend/tests/WorkflowEngine.Api.IntegrationTests/Endpoints/WorkflowApiEndpointsTests.cs` with new test suites.
- Always create a **new dedicated test file** under `backend/tests/WorkflowEngine.Api.IntegrationTests/Endpoints/<FeatureName>Tests.cs`.
- Follow the pattern in `AtomicSubmissionAndConcurrencyTests.cs`:
  - Class implements `IClassFixture<TestWebApplicationFactory>`.
  - Inject `TestWebApplicationFactory` via constructor.

## Authenticated `HttpClient` Helper
Obtain an authenticated client in integration tests using `TestWebApplicationFactory` and `/api/auth/login`:
```csharp
private async Task<HttpClient> CreateAuthenticatedClientAsync(
    string email = "adel.sebti@corp.internal",
    string password = DatabaseInitializer.DefaultSeedPassword)
{
    var client = _factory.CreateClient();
    var loginRes = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
    loginRes.StatusCode.Should().Be(HttpStatusCode.OK);
    var body = await loginRes.Content.ReadFromJsonAsync<LoginResponse>();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.Token);
    return client;
}
```

## Fast Iteration (During Development)
Run only the targeted test or test class while iterating:
```bash
dotnet test backend/WorkflowEngine.sln --filter "FullyQualifiedName~<NomTest>" --verbosity minimal
```
For a single frontend test file:
```bash
npx vitest run <path/to/test.spec.ts>
```

## Final Validation (Run Once at the End)
Execute the full suite only once all edits are complete:
```bash
dotnet test backend/WorkflowEngine.sln --verbosity minimal
npm run build
npx vitest run
```
