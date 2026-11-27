using System.ComponentModel.DataAnnotations;

namespace CarRental.Web.Areas.Fleet.Data;

public sealed class VehicleType
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string LicenseClass { get; set; } = "";
}

public sealed class Vehicle
{
    public string Id { get; set; } = "";
    public string TypeId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Brand { get; set; } = "";
    public string Plate { get; set; } = "";
    public string Color { get; set; } = "";
    public short Year { get; set; }
    public short Seats { get; set; }
    public string Transmission { get; set; } = "";
    public string Status { get; set; } = "SanSang";
    public string? Parking { get; set; }
    public bool Archived { get; set; }
}

public sealed class VehicleImage
{
    public long Id { get; set; }
    public string VehicleId { get; set; } = "";
    public string Url { get; set; } = "";
    public int Order { get; set; }
}

public sealed record VehicleInput(
    [Required, RegularExpression("^[A-Za-z0-9_-]{1,20}$")] string Id,
    [Required, StringLength(20)] string TypeId,
    [Required, StringLength(100)] string Name,
    [Required, StringLength(80)] string Brand,
    [Required, StringLength(20)] string Plate,
    [Required, StringLength(40)] string Color,
    short Year,
    [Range(1, 100)] short Seats,
    [Required, StringLength(30)] string Transmission,
    [StringLength(255)] string? Parking,
    [Required, RegularExpression("^(SanSang|NgungKhaiThac)$")] string Status,
    string[] Images);
