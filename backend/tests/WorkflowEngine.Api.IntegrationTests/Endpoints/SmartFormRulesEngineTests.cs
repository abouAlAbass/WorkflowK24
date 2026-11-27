using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using WorkflowEngine.Api.Endpoints;
using WorkflowEngine.Application.Common.Exceptions;
using WorkflowEngine.Application.Features.SmartForms.DTOs;
using WorkflowEngine.Infrastructure.Persistence;
using Xunit;

namespace WorkflowEngine.Api.IntegrationTests.Endpoints;

/// <summary>
/// Milestone C / Step C.2 — Isomorphic JSONLogic Rules Engine, DAG cycle detection and server-side Calculated Fields.
/// </summary>
public class SmartFormRulesEngineTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public SmartFormRulesEngineTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

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

    [Fact]
    public async Task SaveSmartForm_WhenRulesContainCircularDependency_Returns422()
    {
        var client = await CreateAuthenticatedClientAsync();
        string formId = $"form-dag-cycle-{Guid.NewGuid():N}"[..24];

        var cyclicCreatePayload = new
        {
            id = formId,
            name = "CyclicRuleForm",
            title = "Cyclic Rule Form",
            description = "Tests DAG cycle rejection on POST",
            category = "Finance",
            publishImmediately = true,
            fields = new object[]
            {
                new { id = "f-a", name = "FieldA", label = "Field A", type = "number", required = false, readOnly = false },
                new { id = "f-b", name = "FieldB", label = "Field B", type = "number", required = false, readOnly = false }
            },
            rules = new object[]
            {
                new
                {
                    id = "r-1",
                    targetField = "FieldA",
                    effect = "computedValue",
                    expressionJsonLogic = new Dictionary<string, object>
                    {
                        ["+"] = new object[] { new Dictionary<string, object> { ["var"] = "FieldB" }, 10 }
                    }
                },
                new
                {
                    id = "r-2",
                    targetField = "FieldB",
                    effect = "computedValue",
                    expressionJsonLogic = new Dictionary<string, object>
                    {
                        ["*"] = new object[] { new Dictionary<string, object> { ["var"] = "FieldA" }, 2 }
                    }
                }
            }
        };

        var createRes = await client.PostAsJsonAsync("/api/smartforms", cyclicCreatePayload);
        createRes.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var createBody = await createRes.Content.ReadFromJsonAsync<JsonElement>();
        createBody.GetProperty("code").GetString().Should().Be(CyclicFormRuleException.ErrorCode);

        // Now create a valid acyclic form, then try to introduce a cycle via PUT
        var validPayload = new
        {
            id = formId,
            name = "CyclicRuleForm",
            title = "Valid Acyclic Form",
            description = "Initially valid",
            category = "Finance",
            publishImmediately = false,
            fields = new object[]
            {
                new { id = "f-a", name = "FieldA", label = "Field A", type = "number", required = false, readOnly = false },
                new { id = "f-b", name = "FieldB", label = "Field B", type = "number", required = false, readOnly = false }
            },
            rules = Array.Empty<object>()
        };

        var okCreate = await client.PostAsJsonAsync("/api/smartforms", validPayload);
        okCreate.StatusCode.Should().Be(HttpStatusCode.Created);

        var cyclicPutPayload = new
        {
            title = "Cyclic Update",
            fields = validPayload.fields,
            rules = cyclicCreatePayload.rules
        };

        var putRes = await client.PutAsJsonAsync($"/api/smartforms/{formId}", cyclicPutPayload);
        putRes.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var putBody = await putRes.Content.ReadFromJsonAsync<JsonElement>();
        putBody.GetProperty("code").GetString().Should().Be(CyclicFormRuleException.ErrorCode);
    }

    [Fact]
    public async Task SubmitSmartForm_WhenConditionalRequiredTriggered_RejectsIfMissing()
    {
        var client = await CreateAuthenticatedClientAsync();
        string formId = $"form-cond-req-{Guid.NewGuid():N}"[..24];

        var formPayload = new
        {
            id = formId,
            name = "ConditionalRequiredForm",
            title = "Conditional Required Form",
            description = "Justification required when Amount > 5000 AND Department == Finance",
            category = "Finance",
            publishImmediately = true,
            fields = new object[]
            {
                new { id = "f-amt", name = "Amount", label = "Amount", type = "currency", required = true, readOnly = false },
                new { id = "f-dept", name = "Department", label = "Department", type = "text", required = true, readOnly = false },
                new { id = "f-just", name = "ExecutiveJustification", label = "Executive Justification", type = "textarea", required = false, readOnly = false }
            },
            rules = new object[]
            {
                new
                {
                    id = "r-req-just",
                    targetField = "ExecutiveJustification",
                    effect = "required",
                    conditionJsonLogic = new Dictionary<string, object>
                    {
                        ["and"] = new object[]
                        {
                            new Dictionary<string, object>
                            {
                                [">"] = new object[] { new Dictionary<string, object> { ["var"] = "Amount" }, 5000 }
                            },
                            new Dictionary<string, object>
                            {
                                ["=="] = new object[] { new Dictionary<string, object> { ["var"] = "Department" }, "Finance" }
                            }
                        }
                    }
                }
            }
        };

        var createRes = await client.PostAsJsonAsync("/api/smartforms", formPayload);
        createRes.StatusCode.Should().Be(HttpStatusCode.Created);

        // 1. Triggered condition without ExecutiveJustification -> rejected
        var missingRes = await client.PostAsJsonAsync($"/api/smartforms/{formId}/submit", new
        {
            data = new Dictionary<string, object?>
            {
                ["Amount"] = 8500,
                ["Department"] = "Finance"
            },
            startLinkedWorkflow = false
        });

        missingRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var missingBody = await missingRes.Content.ReadFromJsonAsync<JsonElement>();
        missingBody.GetProperty("errors").TryGetProperty("ExecutiveJustification", out _).Should().BeTrue();

        // 2. Triggered condition WITH ExecutiveJustification -> accepted
        var validTriggeredRes = await client.PostAsJsonAsync($"/api/smartforms/{formId}/submit", new
        {
            data = new Dictionary<string, object?>
            {
                ["Amount"] = 8500,
                ["Department"] = "Finance",
                ["ExecutiveJustification"] = "Strategic Q4 renewal approved by CFO."
            },
            startLinkedWorkflow = false
        });
        validTriggeredRes.StatusCode.Should().Be(HttpStatusCode.Created);

        // 3. Condition NOT triggered (Amount <= 5000) without ExecutiveJustification -> accepted
        var nonTriggeredRes = await client.PostAsJsonAsync($"/api/smartforms/{formId}/submit", new
        {
            data = new Dictionary<string, object?>
            {
                ["Amount"] = 1200,
                ["Department"] = "Finance"
            },
            startLinkedWorkflow = false
        });
        nonTriggeredRes.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task SubmitSmartForm_RecomputesCalculatedFieldServerSideIgnoringTamperedClientValue()
    {
        var client = await CreateAuthenticatedClientAsync();
        string formId = $"form-calc-{Guid.NewGuid():N}"[..24];

        var formPayload = new
        {
            id = formId,
            name = "CalculatedTotalForm",
            title = "Repeater Total Calculation Form",
            description = "Recomputes TotalAmount from LineItems repeater server-side",
            category = "Finance",
            publishImmediately = true,
            fields = new object[]
            {
                new
                {
                    id = "f-lines",
                    name = "LineItems",
                    label = "Line Items",
                    type = "repeater",
                    required = true,
                    readOnly = false,
                    repeaterColumns = new object[]
                    {
                        new { name = "Item", label = "Item", type = "text", required = true },
                        new { name = "Qty", label = "Qty", type = "number", required = true },
                        new { name = "UnitPrice", label = "Unit Price", type = "number", required = true }
                    }
                },
                new
                {
                    id = "f-total",
                    name = "TotalAmount",
                    label = "Total Amount",
                    type = "currency",
                    required = false,
                    readOnly = false
                }
            },
            rules = new object[]
            {
                new
                {
                    id = "r-calc-total",
                    targetField = "TotalAmount",
                    effect = "computedValue",
                    expressionJsonLogic = new Dictionary<string, object>
                    {
                        ["sum"] = new object[]
                        {
                            new Dictionary<string, object> { ["var"] = "LineItems" },
                            new Dictionary<string, object>
                            {
                                ["*"] = new object[]
                                {
                                    new Dictionary<string, object> { ["var"] = "Qty" },
                                    new Dictionary<string, object> { ["var"] = "UnitPrice" }
                                }
                            }
                        }
                    }
                }
            }
        };

        var createRes = await client.PostAsJsonAsync("/api/smartforms", formPayload);
        createRes.StatusCode.Should().Be(HttpStatusCode.Created);

        // Client tampers with TotalAmount = 1 instead of 2*1200 + 3*50 = 2550
        var submitRes = await client.PostAsJsonAsync($"/api/smartforms/{formId}/submit", new
        {
            data = new Dictionary<string, object?>
            {
                ["LineItems"] = new object[]
                {
                    new Dictionary<string, object?> { ["Item"] = "Workstation", ["Qty"] = 2, ["UnitPrice"] = 1200 },
                    new Dictionary<string, object?> { ["Item"] = "Dock", ["Qty"] = 3, ["UnitPrice"] = 50 }
                },
                ["TotalAmount"] = 1
            },
            startLinkedWorkflow = false
        });

        submitRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var submission = await submitRes.Content.ReadFromJsonAsync<SmartFormSubmissionDto>();
        submission.Should().NotBeNull();
        submission!.Data.Should().ContainKey("TotalAmount");

        var totalJson = (JsonElement)submission.Data["TotalAmount"]!;
        totalJson.GetDecimal().Should().Be(2550m);
    }

    [Fact]
    public async Task SubmitSmartForm_WhenConditionalReadOnlyTriggered_RejectsModification()
    {
        var client = await CreateAuthenticatedClientAsync();
        string formId = $"form-cond-ro-{Guid.NewGuid():N}"[..24];

        var formPayload = new
        {
            id = formId,
            name = "ConditionalReadOnlyForm",
            title = "Conditional Read-Only Form",
            description = "Locks DiscountPercent when CustomerTier == Standard",
            category = "Sales",
            publishImmediately = true,
            fields = new object[]
            {
                new { id = "f-tier", name = "CustomerTier", label = "Customer Tier", type = "text", required = true, readOnly = false },
                new { id = "f-disc", name = "DiscountPercent", label = "Discount %", type = "number", required = false, readOnly = false, defaultValue = "0" }
            },
            rules = new object[]
            {
                new
                {
                    id = "r-ro-disc",
                    targetField = "DiscountPercent",
                    effect = "readOnly",
                    conditionJsonLogic = new Dictionary<string, object>
                    {
                        ["=="] = new object[]
                        {
                            new Dictionary<string, object> { ["var"] = "CustomerTier" },
                            "Standard"
                        }
                    }
                }
            }
        };

        var createRes = await client.PostAsJsonAsync("/api/smartforms", formPayload);
        createRes.StatusCode.Should().Be(HttpStatusCode.Created);

        // 1. Standard tier trying to set DiscountPercent = 25 -> rejected with READ_ONLY_FIELD_MODIFIED
        var tamperedRes = await client.PostAsJsonAsync($"/api/smartforms/{formId}/submit", new
        {
            data = new Dictionary<string, object?>
            {
                ["CustomerTier"] = "Standard",
                ["DiscountPercent"] = 25
            },
            startLinkedWorkflow = false
        });

        tamperedRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var tamperedBody = await tamperedRes.Content.ReadFromJsonAsync<JsonElement>();
        tamperedBody.GetProperty("code").GetString().Should().Be(SmartFormValidationFailedException.ReadOnlyModifiedCode);
        tamperedBody.GetProperty("errors").TryGetProperty("DiscountPercent", out _).Should().BeTrue();

        // 2. VIP tier (rule not triggered) setting DiscountPercent = 25 -> accepted
        var allowedRes = await client.PostAsJsonAsync($"/api/smartforms/{formId}/submit", new
        {
            data = new Dictionary<string, object?>
            {
                ["CustomerTier"] = "VIP",
                ["DiscountPercent"] = 25
            },
            startLinkedWorkflow = false
        });
        allowedRes.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
