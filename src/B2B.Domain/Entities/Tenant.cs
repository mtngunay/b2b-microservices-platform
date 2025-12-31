using B2B.Domain.Interfaces;
using B2B.Domain.ValueObjects;

namespace B2B.Domain.Entities;

/// <summary>
/// Represents a tenant in the multi-tenant system.
/// </summary>
public class Tenant : IEntity<Guid>
{
    /// <summary>
    /// Gets the unique identifier of the tenant.
    /// </summary>
    public Guid Id { get; private set; } = Guid.NewGuid();

    /// <summary>
    /// Gets the name of the tenant/organization.
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the subdomain for this tenant.
    /// </summary>
    public string Subdomain { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the tenant-specific settings.
    /// </summary>
    public TenantSettings Settings { get; private set; } = TenantSettings.CreateDefault();

    /// <summary>
    /// Gets a value indicating whether the tenant is active.
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Gets the timestamp when the tenant was created.
    /// </summary>
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the timestamp when the tenant was last updated.
    /// </summary>
    public DateTime? UpdatedAt { get; private set; }

    /// <summary>
    /// Gets the contact email for the tenant.
    /// </summary>
    public string ContactEmail { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the contact phone for the tenant.
    /// </summary>
    public string? ContactPhone { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the tenant has been soft deleted.
    /// </summary>
    public bool IsDeleted { get; private set; }

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
    private Tenant() { }

    /// <summary>
    /// Creates a new tenant.
    /// </summary>
    /// <param name="name">The name of the tenant.</param>
    /// <param name="subdomain">The subdomain for the tenant.</param>
    /// <param name="contactEmail">The contact email.</param>
    /// <param name="settings">Optional custom settings.</param>
    /// <returns>A new Tenant instance.</returns>
    public static Tenant Create(
        string name,
        string subdomain,
        string contactEmail,
        TenantSettings? settings = null)
    {
        return new Tenant
        {
            Name = name,
            Subdomain = subdomain.ToLowerInvariant(),
            ContactEmail = contactEmail,
            Settings = settings ?? TenantSettings.CreateDefault(),
            IsActive = true
        };
    }

    /// <summary>
    /// Updates the tenant information.
    /// </summary>
    /// <param name="name">The new name.</param>
    /// <param name="contactEmail">The new contact email.</param>
    /// <param name="contactPhone">The new contact phone.</param>
    public void Update(string name, string contactEmail, string? contactPhone)
    {
        Name = name;
        ContactEmail = contactEmail;
        ContactPhone = contactPhone;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the tenant's subdomain.
    /// </summary>
    /// <param name="subdomain">The new subdomain.</param>
    public void UpdateSubdomain(string subdomain)
    {
        Subdomain = subdomain.ToLowerInvariant();
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Activates the tenant.
    /// </summary>
    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Deactivates the tenant.
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks the tenant as deleted (soft delete).
    /// </summary>
    public void MarkAsDeleted()
    {
        IsDeleted = true;
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the tenant settings.
    /// </summary>
    /// <param name="settings">The new settings.</param>
    public void UpdateSettings(TenantSettings settings)
    {
        Settings = settings;
        UpdatedAt = DateTime.UtcNow;
    }
}
