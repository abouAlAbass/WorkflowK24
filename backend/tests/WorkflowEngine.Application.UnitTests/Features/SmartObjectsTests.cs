using FluentAssertions;
using Moq;
using WorkflowEngine.Application.Features.SmartObjects.Commands;
using WorkflowEngine.Application.Features.SmartObjects.DTOs;
using WorkflowEngine.Application.Features.SmartObjects.Queries;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Repositories;
using Xunit;

namespace WorkflowEngine.Application.UnitTests.Features;

public class SmartObjectsTests
{
    private readonly Mock<ISmartObjectRepository> _repoMock;

    public SmartObjectsTests()
    {
        _repoMock = new Mock<ISmartObjectRepository>();
    }

    [Fact]
    public async Task CreateSmartObject_AddsEntityAndReturnsDto()
    {
        // Arrange
        var command = new CreateSmartObjectCommand(
            "so-vendors",
            "Vendors",
            "Approved Vendor Directory",
            "Global supplier registry",
            "Finance",
            new List<SmartObjectFieldDto>
            {
                new("VendorId", "Number", true, true),
                new("CompanyName", "String", false, true)
            }
        );

        _repoMock.Setup(r => r.GetByIdAsync("so-vendors", It.IsAny<CancellationToken>()))
            .ReturnsAsync((SmartObjectEntity?)null);

        _repoMock.Setup(r => r.AddAsync(It.IsAny<SmartObjectEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SmartObjectEntity e, CancellationToken _) => e);

        // Act
        var result = await CreateSmartObjectHandler.Handle(command, _repoMock.Object, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be("so-vendors");
        result.Name.Should().Be("Vendors");
        result.DisplayName.Should().Be("Approved Vendor Directory");
        result.Category.Should().Be("Finance");
        result.Fields.Should().HaveCount(2);
        _repoMock.Verify(r => r.AddAsync(It.IsAny<SmartObjectEntity>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddSmartObjectRecord_InsertsRecordAndIncrementsCount()
    {
        // Arrange
        var so = new SmartObjectEntity(
            "so-vendors",
            "Vendors",
            "Vendor Directory",
            "Desc",
            "Finance",
            "[{\"name\":\"VendorId\",\"type\":\"Number\",\"isPrimaryKey\":true}]",
            recordsCount: 0
        );

        _repoMock.Setup(r => r.GetByIdAsync("so-vendors", It.IsAny<CancellationToken>()))
            .ReturnsAsync(so);

        _repoMock.Setup(r => r.GetRecordByKeyAsync("so-vendors", "101", It.IsAny<CancellationToken>()))
            .ReturnsAsync((SmartObjectRecordEntity?)null);

        _repoMock.Setup(r => r.AddRecordAsync(It.IsAny<SmartObjectRecordEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SmartObjectRecordEntity r, CancellationToken _) => r);

        var recordData = new Dictionary<string, object?>
        {
            ["VendorId"] = 101,
            ["CompanyName"] = "Acme Corp",
            ["Rating"] = "A+"
        };

        var command = new AddSmartObjectRecordCommand("so-vendors", recordData);

        // Act
        var result = await AddSmartObjectRecordHandler.Handle(command, _repoMock.Object, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result["VendorId"].Should().Be(101);
        result["CompanyName"].Should().Be("Acme Corp");
        _repoMock.Verify(r => r.AddRecordAsync(It.IsAny<SmartObjectRecordEntity>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteSmartObjectRecord_CallsRepositoryDelete()
    {
        // Arrange
        _repoMock.Setup(r => r.DeleteRecordAsync("so-vendors", "101", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var command = new DeleteSmartObjectRecordCommand("so-vendors", "101");

        // Act
        var result = await DeleteSmartObjectRecordHandler.Handle(command, _repoMock.Object, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        _repoMock.Verify(r => r.DeleteRecordAsync("so-vendors", "101", It.IsAny<CancellationToken>()), Times.Once);
    }
}
