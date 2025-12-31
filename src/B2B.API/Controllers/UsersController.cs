using System.Security.Cryptography;
using System.Text;
using Asp.Versioning;
using AutoMapper;
using B2B.Application.DTOs;
using B2B.Application.Interfaces.Services;
using B2B.Domain.Aggregates;
using B2B.Domain.Entities;
using B2B.Domain.Exceptions;
using B2B.Infrastructure.Persistence.ReadDb;
using B2B.Infrastructure.Persistence.ReadDb.ReadModels;
using B2B.Infrastructure.Persistence.WriteDb;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;

namespace B2B.API.Controllers;

/// <summary>
/// Controller for user management operations.
/// Uses Cache-Aside pattern with fallback: Redis → MongoDB → MSSQL
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly WriteDbContext _writeDbContext;
    private readonly MongoDbContext _readDbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICorrelationIdAccessor _correlationIdAccessor;
    private readonly ICacheService _cacheService;
    private readonly IMapper _mapper;
    private readonly ILogger<UsersController> _logger;

    private const string UsersCacheKeyPrefix = "users:list";
    private const string UserCacheKeyPrefix = "users:single";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public UsersController(
        WriteDbContext writeDbContext,
        MongoDbContext readDbContext,
        ICurrentUserService currentUserService,
        ICorrelationIdAccessor correlationIdAccessor,
        ICacheService cacheService,
        IMapper mapper,
        ILogger<UsersController> logger)
    {
        _writeDbContext = writeDbContext ?? throw new ArgumentNullException(nameof(writeDbContext));
        _readDbContext = readDbContext ?? throw new ArgumentNullException(nameof(readDbContext));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _correlationIdAccessor = correlationIdAccessor ?? throw new ArgumentNullException(nameof(correlationIdAccessor));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets a paginated list of users.
    /// Uses fallback pattern: Redis Cache → MongoDB → MSSQL
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResult<UserDto>>> GetUsers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var tenantId = _currentUserService.TenantId ?? "default";
        var cacheKey = $"{UsersCacheKeyPrefix}:{tenantId}:{page}:{pageSize}:{search ?? "all"}";

        // Step 1: Try Redis Cache
        var cachedResult = await _cacheService.GetAsync<PagedResult<UserDto>>(cacheKey, cancellationToken);
        if (cachedResult != null)
        {
            _logger.LogDebug("Users list retrieved from Redis cache");
            return Ok(cachedResult);
        }

        // Step 2: Try MongoDB
        var mongoResult = await GetUsersFromMongoDbAsync(tenantId, page, pageSize, search, cancellationToken);
        if (mongoResult.TotalCount > 0)
        {
            _logger.LogDebug("Users list retrieved from MongoDB");
            // Cache the result in Redis
            await _cacheService.SetAsync(cacheKey, mongoResult, CacheDuration, cancellationToken);
            return Ok(mongoResult);
        }

        // Step 3: Fallback to MSSQL and sync to MongoDB
        _logger.LogInformation("MongoDB empty, falling back to MSSQL and syncing data");
        var sqlResult = await GetUsersFromSqlServerAsync(tenantId, page, pageSize, search, cancellationToken);
        
        if (sqlResult.TotalCount > 0)
        {
            // Sync to MongoDB synchronously (using CancellationToken.None to ensure completion)
            // This ensures data is synced before response, making subsequent reads faster
            await SyncUsersToMongoDbAsync(tenantId, CancellationToken.None);
            
            // Cache the result in Redis
            await _cacheService.SetAsync(cacheKey, sqlResult, CacheDuration, cancellationToken);
        }

        return Ok(sqlResult);
    }

    /// <summary>
    /// Gets users from MongoDB.
    /// </summary>
    private async Task<PagedResult<UserDto>> GetUsersFromMongoDbAsync(
        string tenantId, int page, int pageSize, string? search, CancellationToken cancellationToken)
    {
        var filterBuilder = Builders<UserReadModel>.Filter;
        var filters = new List<FilterDefinition<UserReadModel>>
        {
            filterBuilder.Eq(u => u.IsDeleted, false)
        };

        if (!string.IsNullOrEmpty(tenantId) && tenantId != "default")
        {
            filters.Add(filterBuilder.Eq(u => u.TenantId, tenantId));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchFilter = filterBuilder.Or(
                filterBuilder.Regex(u => u.Email, new MongoDB.Bson.BsonRegularExpression(search, "i")),
                filterBuilder.Regex(u => u.FullName, new MongoDB.Bson.BsonRegularExpression(search, "i"))
            );
            filters.Add(searchFilter);
        }

        var combinedFilter = filterBuilder.And(filters);

        var totalCount = await _readDbContext.Users.CountDocumentsAsync(combinedFilter, cancellationToken: cancellationToken);

        var items = await _readDbContext.Users
            .Find(combinedFilter)
            .SortBy(u => u.Email)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);

        var userDtos = items.Select(u => new UserDto
        {
            Id = Guid.Parse(u.Id),
            Email = u.Email,
            FirstName = u.FirstName,
            LastName = u.LastName,
            FullName = u.FullName,
            IsActive = u.IsActive,
            TenantId = u.TenantId,
            Roles = u.Roles,
            Permissions = u.Permissions,
            CreatedAt = u.CreatedAt,
            LastLoginAt = u.LastLoginAt
        }).ToList();

        return new PagedResult<UserDto>
        {
            Items = userDtos,
            TotalCount = (int)totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <summary>
    /// Gets users from SQL Server (fallback).
    /// </summary>
    private async Task<PagedResult<UserDto>> GetUsersFromSqlServerAsync(
        string tenantId, int page, int pageSize, string? search, CancellationToken cancellationToken)
    {
        var query = _writeDbContext.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r!.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
            .Where(u => !u.IsDeleted);

        if (!string.IsNullOrEmpty(tenantId) && tenantId != "default")
        {
            query = query.Where(u => u.TenantId == tenantId);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(u => 
                u.Email.Contains(search) || 
                u.FirstName.Contains(search) || 
                u.LastName.Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var users = await query
            .OrderBy(u => u.Email)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var userDtos = users.Select(u => new UserDto
        {
            Id = u.Id,
            Email = u.Email,
            FirstName = u.FirstName,
            LastName = u.LastName,
            FullName = u.FullName,
            IsActive = u.IsActive,
            TenantId = u.TenantId,
            Roles = u.UserRoles.Where(ur => ur.Role != null).Select(ur => ur.Role!.Name).ToList(),
            Permissions = u.UserRoles
                .Where(ur => ur.Role?.RolePermissions != null)
                .SelectMany(ur => ur.Role!.RolePermissions)
                .Where(rp => rp.Permission != null)
                .Select(rp => rp.Permission!.Name)
                .Distinct()
                .ToList(),
            CreatedAt = u.CreatedAt,
            LastLoginAt = u.LastLoginAt
        }).ToList();

        return new PagedResult<UserDto>
        {
            Items = userDtos,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <summary>
    /// Syncs users from SQL Server to MongoDB (background operation).
    /// </summary>
    private async Task SyncUsersToMongoDbAsync(string tenantId, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting background sync of users to MongoDB for tenant {TenantId}", tenantId);

            var query = _writeDbContext.Users
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                        .ThenInclude(r => r!.RolePermissions)
                            .ThenInclude(rp => rp.Permission)
                .Where(u => !u.IsDeleted);

            if (!string.IsNullOrEmpty(tenantId) && tenantId != "default")
            {
                query = query.Where(u => u.TenantId == tenantId);
            }

            var users = await query.ToListAsync(cancellationToken);

            foreach (var user in users)
            {
                var roles = user.UserRoles
                    .Where(ur => ur.Role != null)
                    .Select(ur => ur.Role!.Name)
                    .ToList();

                var permissions = user.UserRoles
                    .Where(ur => ur.Role?.RolePermissions != null)
                    .SelectMany(ur => ur.Role!.RolePermissions)
                    .Where(rp => rp.Permission != null)
                    .Select(rp => rp.Permission!.Name)
                    .Distinct()
                    .ToList();

                var userReadModel = new UserReadModel
                {
                    Id = user.Id.ToString(),
                    TenantId = user.TenantId,
                    Email = user.Email,
                    FullName = user.FullName,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    IsActive = user.IsActive,
                    Roles = roles,
                    Permissions = permissions,
                    CreatedAt = user.CreatedAt,
                    UpdatedAt = user.UpdatedAt,
                    LastLoginAt = user.LastLoginAt,
                    IsDeleted = user.IsDeleted
                };

                var filter = Builders<UserReadModel>.Filter.Eq(u => u.Id, userReadModel.Id);
                await _readDbContext.Users.ReplaceOneAsync(
                    filter,
                    userReadModel,
                    new ReplaceOptions { IsUpsert = true },
                    cancellationToken);
            }

            _logger.LogInformation("Background sync completed. Synced {Count} users to MongoDB", users.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during background sync to MongoDB");
        }
    }

    /// <summary>
    /// Gets a user by ID.
    /// Uses fallback pattern: Redis Cache → MongoDB → MSSQL
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserDto>> GetUser(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId ?? "default";
        var cacheKey = $"{UserCacheKeyPrefix}:{tenantId}:{id}";

        // Step 1: Try Redis Cache
        var cachedUser = await _cacheService.GetAsync<UserDto>(cacheKey, cancellationToken);
        if (cachedUser != null)
        {
            _logger.LogDebug("User {UserId} retrieved from Redis cache", id);
            return Ok(cachedUser);
        }

        // Step 2: Try MongoDB
        var mongoUser = await GetUserFromMongoDbAsync(id, tenantId, cancellationToken);
        if (mongoUser != null)
        {
            _logger.LogDebug("User {UserId} retrieved from MongoDB", id);
            await _cacheService.SetAsync(cacheKey, mongoUser, CacheDuration, cancellationToken);
            return Ok(mongoUser);
        }

        // Step 3: Fallback to MSSQL
        _logger.LogInformation("User {UserId} not found in MongoDB, falling back to MSSQL", id);
        var sqlUser = await GetUserFromSqlServerAsync(id, tenantId, cancellationToken);
        
        if (sqlUser == null)
        {
            throw new NotFoundException("User", id);
        }

        // Sync this user to MongoDB
        await SyncSingleUserToMongoDbAsync(id, cancellationToken);
        
        // Cache in Redis
        await _cacheService.SetAsync(cacheKey, sqlUser, CacheDuration, cancellationToken);

        return Ok(sqlUser);
    }

    /// <summary>
    /// Gets a single user from MongoDB.
    /// </summary>
    private async Task<UserDto?> GetUserFromMongoDbAsync(Guid id, string tenantId, CancellationToken cancellationToken)
    {
        var filterBuilder = Builders<UserReadModel>.Filter;
        var filters = new List<FilterDefinition<UserReadModel>>
        {
            filterBuilder.Eq(u => u.Id, id.ToString()),
            filterBuilder.Eq(u => u.IsDeleted, false)
        };

        if (!string.IsNullOrEmpty(tenantId) && tenantId != "default")
        {
            filters.Add(filterBuilder.Eq(u => u.TenantId, tenantId));
        }

        var combinedFilter = filterBuilder.And(filters);

        var user = await _readDbContext.Users
            .Find(combinedFilter)
            .FirstOrDefaultAsync(cancellationToken);

        if (user == null) return null;

        return new UserDto
        {
            Id = Guid.Parse(user.Id),
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            FullName = user.FullName,
            IsActive = user.IsActive,
            TenantId = user.TenantId,
            Roles = user.Roles,
            Permissions = user.Permissions,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt
        };
    }

    /// <summary>
    /// Gets a single user from SQL Server.
    /// </summary>
    private async Task<UserDto?> GetUserFromSqlServerAsync(Guid id, string tenantId, CancellationToken cancellationToken)
    {
        var query = _writeDbContext.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r!.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
            .Where(u => u.Id == id && !u.IsDeleted);

        if (!string.IsNullOrEmpty(tenantId) && tenantId != "default")
        {
            query = query.Where(u => u.TenantId == tenantId);
        }

        var user = await query.FirstOrDefaultAsync(cancellationToken);

        if (user == null) return null;

        return new UserDto
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            FullName = user.FullName,
            IsActive = user.IsActive,
            TenantId = user.TenantId,
            Roles = user.UserRoles.Where(ur => ur.Role != null).Select(ur => ur.Role!.Name).ToList(),
            Permissions = user.UserRoles
                .Where(ur => ur.Role?.RolePermissions != null)
                .SelectMany(ur => ur.Role!.RolePermissions)
                .Where(rp => rp.Permission != null)
                .Select(rp => rp.Permission!.Name)
                .Distinct()
                .ToList(),
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt
        };
    }

    /// <summary>
    /// Syncs a single user to MongoDB.
    /// </summary>
    private async Task SyncSingleUserToMongoDbAsync(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _writeDbContext.Users
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                        .ThenInclude(r => r!.RolePermissions)
                            .ThenInclude(rp => rp.Permission)
                .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

            if (user == null) return;

            var roles = user.UserRoles
                .Where(ur => ur.Role != null)
                .Select(ur => ur.Role!.Name)
                .ToList();

            var permissions = user.UserRoles
                .Where(ur => ur.Role?.RolePermissions != null)
                .SelectMany(ur => ur.Role!.RolePermissions)
                .Where(rp => rp.Permission != null)
                .Select(rp => rp.Permission!.Name)
                .Distinct()
                .ToList();

            var userReadModel = new UserReadModel
            {
                Id = user.Id.ToString(),
                TenantId = user.TenantId,
                Email = user.Email,
                FullName = user.FullName,
                FirstName = user.FirstName,
                LastName = user.LastName,
                IsActive = user.IsActive,
                Roles = roles,
                Permissions = permissions,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt,
                LastLoginAt = user.LastLoginAt,
                IsDeleted = user.IsDeleted
            };

            var filter = Builders<UserReadModel>.Filter.Eq(u => u.Id, userReadModel.Id);
            await _readDbContext.Users.ReplaceOneAsync(
                filter,
                userReadModel,
                new ReplaceOptions { IsUpsert = true },
                cancellationToken);

            _logger.LogDebug("User {UserId} synced to MongoDB", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing user {UserId} to MongoDB", userId);
        }
    }

    /// <summary>
    /// Creates a new user.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserDto>> CreateUser(
        [FromBody] CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            throw new ValidationException("Email", "Email is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            throw new ValidationException("Password", "Password is required.");
        }

        var email = request.Email.ToLowerInvariant();
        var tenantId = _currentUserService.TenantId ?? "";
        var correlationId = _correlationIdAccessor.CorrelationId ?? "";

        // Check if email already exists using EF Core
        var existingUser = await EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
            _writeDbContext.Set<User>().Where(u => u.Email == email && !u.IsDeleted),
            cancellationToken);

        if (existingUser != null)
        {
            throw new ConflictException($"A user with email '{email}' already exists.");
        }

        var passwordHash = ComputeHash(request.Password);

        var user = Domain.Aggregates.User.Create(email, passwordHash, request.FirstName, request.LastName, tenantId, correlationId);

        if (request.RoleIds.Any())
        {
            var roles = await EntityFrameworkQueryableExtensions.ToListAsync(
                _writeDbContext.Set<Role>().Where(r => request.RoleIds.Contains(r.Id) && !r.IsDeleted),
                cancellationToken);

            foreach (var role in roles)
            {
                user.AssignRole(role, _currentUserService.UserId ?? "", correlationId);
            }
        }

        await _writeDbContext.Set<User>().AddAsync(user, cancellationToken);
        await _writeDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} created by {CreatedBy}", user.Id, _currentUserService.UserId);

        var userDto = _mapper.Map<UserDto>(user);

        return CreatedAtAction(nameof(GetUser), new { id = user.Id }, userDto);
    }

    /// <summary>
    /// Updates an existing user.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserDto>> UpdateUser(
        Guid id,
        [FromBody] UpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId;

        var query = _writeDbContext.Set<User>()
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .Where(u => u.Id == id && !u.IsDeleted);

        if (!string.IsNullOrEmpty(tenantId))
        {
            query = query.Where(u => u.TenantId == tenantId);
        }

        var user = await EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(query, cancellationToken);

        if (user == null)
        {
            throw new NotFoundException("User", id);
        }

        user.UpdateProfile(request.FirstName, request.LastName);

        if (request.IsActive && !user.IsActive)
        {
            user.Activate();
        }
        else if (!request.IsActive && user.IsActive)
        {
            user.Deactivate();
        }

        await _writeDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} updated by {UpdatedBy}", user.Id, _currentUserService.UserId);

        var userDto = _mapper.Map<UserDto>(user);

        return Ok(userDto);
    }

    /// <summary>
    /// Deletes a user (soft delete).
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeleteUser(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId;

        var query = _writeDbContext.Set<User>().Where(u => u.Id == id && !u.IsDeleted);

        if (!string.IsNullOrEmpty(tenantId))
        {
            query = query.Where(u => u.TenantId == tenantId);
        }

        var user = await EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(query, cancellationToken);

        if (user == null)
        {
            throw new NotFoundException("User", id);
        }

        user.MarkAsDeleted();

        await _writeDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} deleted by {DeletedBy}", user.Id, _currentUserService.UserId);

        return NoContent();
    }

    /// <summary>
    /// Synchronizes users from SQL Server to MongoDB (admin operation).
    /// </summary>
    [HttpPost("sync")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SyncUsersToReadDb(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting user sync from SQL Server to MongoDB");

        // Get all users from SQL Server (ignore tenant filter for sync)
        var users = await _writeDbContext.Users
            .IgnoreQueryFilters()
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .Include(u => u.UserPermissions)
                .ThenInclude(up => up.Permission)
            .ToListAsync(cancellationToken);

        var syncedCount = 0;

        foreach (var user in users)
        {
            var roles = user.UserRoles
                .Where(ur => ur.Role != null)
                .Select(ur => ur.Role!.Name)
                .ToList();

            var permissions = user.UserPermissions
                .Where(up => up.Permission != null)
                .Select(up => up.Permission!.Name)
                .ToList();

            // Add permissions from roles
            var rolePermissions = user.UserRoles
                .Where(ur => ur.Role?.RolePermissions != null)
                .SelectMany(ur => ur.Role!.RolePermissions)
                .Where(rp => rp.Permission != null)
                .Select(rp => rp.Permission!.Name)
                .Distinct();

            permissions.AddRange(rolePermissions);
            permissions = permissions.Distinct().ToList();

            var userReadModel = new UserReadModel
            {
                Id = user.Id.ToString(),
                TenantId = user.TenantId,
                Email = user.Email,
                FullName = user.FullName,
                FirstName = user.FirstName,
                LastName = user.LastName,
                IsActive = user.IsActive,
                Roles = roles,
                Permissions = permissions,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt,
                LastLoginAt = user.LastLoginAt,
                IsDeleted = user.IsDeleted
            };

            var filter = Builders<UserReadModel>.Filter.Eq(u => u.Id, userReadModel.Id);
            await _readDbContext.Users.ReplaceOneAsync(
                filter,
                userReadModel,
                new ReplaceOptions { IsUpsert = true },
                cancellationToken);

            syncedCount++;
        }

        _logger.LogInformation("User sync completed. Synced {Count} users", syncedCount);

        return Ok(new { message = $"Synced {syncedCount} users to MongoDB", count = syncedCount });
    }

    private static string ComputeHash(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(bytes);
    }
}
