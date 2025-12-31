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

namespace B2B.API.Controllers.V2;

/// <summary>
/// Controller for user management operations - Version 2.
/// This version includes enhanced response models and additional features.
/// </summary>
[ApiController]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/users")]
[Authorize]
public class UsersController : ApiControllerBase
{
    private readonly WriteDbContext _writeDbContext;
    private readonly MongoDbContext _readDbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICorrelationIdAccessor _correlationIdAccessor;
    private readonly IMapper _mapper;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        WriteDbContext writeDbContext,
        MongoDbContext readDbContext,
        ICurrentUserService currentUserService,
        ICorrelationIdAccessor correlationIdAccessor,
        IMapper mapper,
        ILogger<UsersController> logger)
    {
        _writeDbContext = writeDbContext ?? throw new ArgumentNullException(nameof(writeDbContext));
        _readDbContext = readDbContext ?? throw new ArgumentNullException(nameof(readDbContext));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _correlationIdAccessor = correlationIdAccessor ?? throw new ArgumentNullException(nameof(correlationIdAccessor));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets a paginated list of users with enhanced response model.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(EnhancedPagedResult<UserDtoV2>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<EnhancedPagedResult<UserDtoV2>>> GetUsers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] string? sortBy = "email",
        [FromQuery] bool sortDescending = false,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var tenantId = _currentUserService.TenantId;

        // Build filter for MongoDB
        var filterBuilder = Builders<UserReadModel>.Filter;
        var filters = new List<FilterDefinition<UserReadModel>>
        {
            filterBuilder.Eq(u => u.IsDeleted, false)
        };

        if (!string.IsNullOrEmpty(tenantId))
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

        // Build sort definition
        var sortDefinition = sortBy?.ToLowerInvariant() switch
        {
            "fullname" => sortDescending 
                ? Builders<UserReadModel>.Sort.Descending(u => u.FullName)
                : Builders<UserReadModel>.Sort.Ascending(u => u.FullName),
            "createdat" => sortDescending 
                ? Builders<UserReadModel>.Sort.Descending(u => u.CreatedAt)
                : Builders<UserReadModel>.Sort.Ascending(u => u.CreatedAt),
            _ => sortDescending 
                ? Builders<UserReadModel>.Sort.Descending(u => u.Email)
                : Builders<UserReadModel>.Sort.Ascending(u => u.Email)
        };

        var items = await _readDbContext.Users
            .Find(combinedFilter)
            .Sort(sortDefinition)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);

        var userDtos = items.Select(u => new UserDtoV2
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
            LastLoginAt = u.LastLoginAt,
            // V2 specific fields
            Status = u.IsActive ? "Active" : "Inactive",
            RoleCount = u.Roles?.Count ?? 0,
            PermissionCount = u.Permissions?.Count ?? 0
        }).ToList();

        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        return Ok(new EnhancedPagedResult<UserDtoV2>
        {
            Items = userDtos,
            TotalCount = (int)totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages,
            HasNextPage = page < totalPages,
            HasPreviousPage = page > 1,
            SortBy = sortBy ?? "email",
            SortDescending = sortDescending
        });
    }

    /// <summary>
    /// Gets a user by ID with enhanced response model.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(UserDtoV2), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserDtoV2>> GetUser(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId;

        var filterBuilder = Builders<UserReadModel>.Filter;
        var filters = new List<FilterDefinition<UserReadModel>>
        {
            filterBuilder.Eq(u => u.Id, id.ToString()),
            filterBuilder.Eq(u => u.IsDeleted, false)
        };

        if (!string.IsNullOrEmpty(tenantId))
        {
            filters.Add(filterBuilder.Eq(u => u.TenantId, tenantId));
        }

        var combinedFilter = filterBuilder.And(filters);

        var user = await _readDbContext.Users
            .Find(combinedFilter)
            .FirstOrDefaultAsync(cancellationToken);

        if (user == null)
        {
            throw new NotFoundException("User", id);
        }

        var userDto = new UserDtoV2
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
            LastLoginAt = user.LastLoginAt,
            // V2 specific fields
            Status = user.IsActive ? "Active" : "Inactive",
            RoleCount = user.Roles?.Count ?? 0,
            PermissionCount = user.Permissions?.Count ?? 0
        };

        return Ok(userDto);
    }
}

/// <summary>
/// Enhanced user DTO for API v2 with additional computed fields.
/// </summary>
public class UserDtoV2 : UserDto
{
    /// <summary>
    /// Human-readable status string.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Number of roles assigned to the user.
    /// </summary>
    public int RoleCount { get; set; }

    /// <summary>
    /// Number of permissions assigned to the user.
    /// </summary>
    public int PermissionCount { get; set; }
}

/// <summary>
/// Enhanced paged result with additional pagination metadata.
/// </summary>
/// <typeparam name="T">The type of items in the result.</typeparam>
public class EnhancedPagedResult<T>
{
    /// <summary>
    /// The items in the current page.
    /// </summary>
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();

    /// <summary>
    /// Total number of items across all pages.
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Current page number (1-based).
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    /// Number of items per page.
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// Total number of pages.
    /// </summary>
    public int TotalPages { get; set; }

    /// <summary>
    /// Whether there is a next page.
    /// </summary>
    public bool HasNextPage { get; set; }

    /// <summary>
    /// Whether there is a previous page.
    /// </summary>
    public bool HasPreviousPage { get; set; }

    /// <summary>
    /// The field used for sorting.
    /// </summary>
    public string SortBy { get; set; } = string.Empty;

    /// <summary>
    /// Whether sorting is in descending order.
    /// </summary>
    public bool SortDescending { get; set; }
}
