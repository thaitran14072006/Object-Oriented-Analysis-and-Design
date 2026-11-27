using System.ComponentModel.DataAnnotations;

namespace CarRental.Web.Areas.Maintenance.Data;

public sealed class MaintenanceRecord
{
    public string Id { get; set; } = "";
    public string VehicleId { get; set; } = "";
    public string Creator { get; set; } = "";
    public string Kind { get; set; } = "";
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public DateTime? ActualStart { get; set; }
    public DateTime? ActualEnd { get; set; }
    public string Content { get; set; } = "";
    public string Status { get; set; } = "ChuaThucHien";
}

public sealed class MaintenanceProgress
{
    public long Id { get; set; }
    public string MaintenanceId { get; set; } = "";
    public string Recorder { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTime Created { get; set; }
}

public sealed class MaintenanceCost
{
    public long Id { get; set; }
    public string MaintenanceId { get; set; } = "";
    public string Item { get; set; } = "";
    public string Provider { get; set; } = "";
    public decimal Amount { get; set; }
    public DateOnly Date { get; set; }
}

public sealed class SafetyCheck
{
    public long Id { get; set; }
    public string MaintenanceId { get; set; } = "";
    public string Recorder { get; set; } = "";
    public string Inspector { get; set; } = "";
    public DateTime Created { get; set; }
    public bool Passed { get; set; }
    public string? Note { get; set; }
}

public sealed record MaintenanceInput(
    [Required, StringLength(20)] string VehicleId,
    [Required, RegularExpression("^(DinhKy|SuaChua|NangCap)$")] string Kind,
    DateTime Start, DateTime End,
    [Required, StringLength(1000)] string Content);
public sealed record ProgressInput([Required, StringLength(1000)] string Content);
public sealed record MaintenanceCostInput(
    [Required, StringLength(255)] string Item,
    [Required, StringLength(255)] string Provider,
    [Range(typeof(decimal), "0", "9999999999999999.99")] decimal Amount, DateOnly Date);
public sealed record SafetyInput(
    [Required, StringLength(100)] string Inspector, bool Passed, [StringLength(1000)] string? Note);
