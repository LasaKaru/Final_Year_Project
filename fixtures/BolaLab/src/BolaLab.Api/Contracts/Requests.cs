namespace BolaLab.Api.Contracts;

public sealed record UpdateStatusRequest(string Status);
public sealed record UpdateOrderRequest(string Status, decimal Total);
public sealed record DeleteOrderRequest(string UserId);
public sealed record UpdateTotalRequest(decimal Total);
public sealed record RenameProjectRequest(string Name);
public sealed record UpdateNoteRequest(string Text);
public sealed record UpdateDocumentRequest(string Title);
public sealed record ChangeAmountRequest(decimal Amount);
