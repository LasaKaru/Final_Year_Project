namespace BolaLab.Api.Domain;

public class Order
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public int TenantId { get; set; }
    public string Status { get; set; } = "New";
    public decimal Total { get; set; }
}

public class Project
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public string Name { get; set; } = "";
}

public class Note
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public string OwnerUserId { get; set; } = "";
    public string Text { get; set; } = "";
}

public class Document
{
    public int Id { get; set; }
    public string OwnerUserId { get; set; } = "";
    public string Title { get; set; } = "";
    public List<DocumentShare> Shares { get; set; } = new();
}

public class DocumentShare
{
    public int Id { get; set; }
    public int DocumentId { get; set; }
    public string UserId { get; set; } = "";
    public bool CanEdit { get; set; }
}

public class Invoice
{
    public int Id { get; set; }
    public string CustomerId { get; set; } = "";
    public decimal Amount { get; set; }
    public bool IsVoid { get; set; }
}
