namespace WorkflowEngine.Application.Features.Analytics.DTOs;

public record DepartmentVolumeDto(
    string Department,
    int Percentage,
    int InstancesCount
);

public record AnalyticsKpiDto(
    string AvgCompletionTime,
    string SlaComplianceRate,
    string TopBottleneckNode,
    string TopBottleneckQueueTime,
    int TotalInstancesProcessed,
    IReadOnlyList<DepartmentVolumeDto> VolumeByDepartment
);
