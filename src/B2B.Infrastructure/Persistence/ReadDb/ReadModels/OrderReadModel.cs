using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace B2B.Infrastructure.Persistence.ReadDb.ReadModels;

/// <summary>
/// Read model for Order entity optimized for queries.
/// </summary>
public class OrderReadModel
{
    /// <summary>
    /// Gets or sets the unique identifier.
    /// </summary>
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the tenant identifier.
    /// </summary>
    [BsonElement("tenantId")]
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the order number.
    /// </summary>
    [BsonElement("orderNumber")]
    public string OrderNumber { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the customer information.
    /// </summary>
    [BsonElement("customer")]
    public CustomerInfo Customer { get; set; } = new();

    /// <summary>
    /// Gets or sets the order items.
    /// </summary>
    [BsonElement("items")]
    public List<OrderItemInfo> Items { get; set; } = new();

    /// <summary>
    /// Gets or sets the total amount.
    /// </summary>
    [BsonElement("totalAmount")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// Gets or sets the order status.
    /// </summary>
    [BsonElement("status")]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the creation timestamp.
    /// </summary>
    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the last update timestamp.
    /// </summary>
    [BsonElement("updatedAt")]
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets whether the order is deleted.
    /// </summary>
    [BsonElement("isDeleted")]
    public bool IsDeleted { get; set; }
}

/// <summary>
/// Embedded customer information for denormalized read model.
/// </summary>
public class CustomerInfo
{
    /// <summary>
    /// Gets or sets the customer identifier.
    /// </summary>
    [BsonElement("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the customer name.
    /// </summary>
    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the customer email.
    /// </summary>
    [BsonElement("email")]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the customer phone.
    /// </summary>
    [BsonElement("phone")]
    public string? Phone { get; set; }
}

/// <summary>
/// Embedded order item information for denormalized read model.
/// </summary>
public class OrderItemInfo
{
    /// <summary>
    /// Gets or sets the product identifier.
    /// </summary>
    [BsonElement("productId")]
    public string ProductId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the product name.
    /// </summary>
    [BsonElement("productName")]
    public string ProductName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the quantity.
    /// </summary>
    [BsonElement("quantity")]
    public int Quantity { get; set; }

    /// <summary>
    /// Gets or sets the unit price.
    /// </summary>
    [BsonElement("unitPrice")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// Gets or sets the line total.
    /// </summary>
    [BsonElement("lineTotal")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal LineTotal { get; set; }
}
