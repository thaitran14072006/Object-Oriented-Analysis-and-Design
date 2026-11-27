namespace CarRental.Web.Shared.Contracts;

public sealed record RentalCustomer(string Id, string Name, string? GroupId, string LicenseClass,
    DateOnly LicenseExpires, bool Verified, bool LicenseExpired, short? Rating);

public interface IRentalCustomerDirectory
{
    Task<RentalCustomer?> FindForRentalAsync(string customerId, CancellationToken cancellationToken);
}
