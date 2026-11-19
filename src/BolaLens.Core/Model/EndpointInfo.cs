namespace BolaLens.Core.Model;

public enum HttpVerb { Get, Post, Put, Patch, Delete, Unknown }

public enum BindingSource { Route, Body, Query, Header, Service, Unknown }

/// <summary>An action parameter with its inferred model-binding source. <see cref="IsIdLike"/> marks
/// client-controlled object selectors; for body DTOs <see cref="IdPropertyPath"/> lists the id-like members.</summary>
public sealed record ParameterInfo(string Name, string Type, BindingSource Source, bool IsIdLike, string? IdPropertyPath = null);

/// <summary>Endpoint-level authorization attributes ([Authorize]/[AllowAnonymous]); method wins over class.</summary>
public sealed record AuthorizeInfo(bool HasAuthorize, bool AllowAnonymous, IReadOnlyList<string> Policies, IReadOnlyList<string> Roles);

/// <summary>One HTTP endpoint (MVC controller action). <see cref="Id"/> is "VERB normalised-route",
/// the unit key used by the evaluation harness.</summary>
public sealed record EndpointInfo(
    string Id,
    HttpVerb Verb,
    string RouteTemplate,
    string Controller,
    string Action,
    string ActionSymbol,
    IReadOnlyList<ParameterInfo> Parameters,
    AuthorizeInfo Authorization,
    SourceRef Location);
