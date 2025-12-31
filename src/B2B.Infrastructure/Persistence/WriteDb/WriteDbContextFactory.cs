using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace B2B.Infrastructure.Persistence.WriteDb;

/// <summary>
/// Design-time factory for creating WriteDbContext instances for EF Core migrations.
/// </summary>
public class WriteDbContextFactory : IDesignTimeDbContextFactory<WriteDbContext>
{
    /// <summary>
    /// Creates a new instance of WriteDbContext for design-time operations.
    /// </summary>
    public WriteDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<WriteDbContext>();
        
        // Try to get connection string from environment variable first, then fall back to default
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__WriteDb") 
            ?? "Server=localhost;Database=B2BWriteDb;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True;";
        
        optionsBuilder.UseSqlServer(connectionString, options =>
        {
            options.MigrationsAssembly(typeof(WriteDbContext).Assembly.FullName);
            options.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null);
        });

        return new WriteDbContext(optionsBuilder.Options);
    }
}
