# 05 — Implementation Guide (component by component)

> **Ownership rule:** the skeletons below are *starting points*. Type them yourself, run them, break them, and add your own comments. In the Q&A the examiner can point at any line and ask "why?". If you cannot answer, rewrite it until you can. Record in `docs/ai-log.md` that this guide was AI-assisted.

Contents
1. Roslyn primer & kata · 2. Loading · 3. Endpoints · 4. Policy map · 5. Ownership schema · 6. DI resolution · 7. Caller identity · 8. Taint walker · 9. EF Core operation classifier · 10. Guard detector · 11. Symbolic rules · 12. Test infrastructure · 13. IR slicer · 14. ONNX validator · 15. Prompt · 16. Response parsing & fusion · 17. Evaluation maths · 18. CLI wiring · 19. Pitfalls & performance

---

## 1. Roslyn primer (the 5 concepts you must be able to explain)

| Concept | What it is | Get it with | Used for |
|---|---|---|---|
| **SyntaxTree / SyntaxNode** | Exact source structure (incl. whitespace/comments = *trivia*) | `document.GetSyntaxRootAsync()` | Locating code, line numbers, slicing text |
| **Compilation** | All trees + references of one project, type-checked | `project.GetCompilationAsync()` | Finding types by name, semantic models |
| **SemanticModel** | Answers "what does this syntax *mean*?" for one tree | `compilation.GetSemanticModel(tree)` | Resolve a call to the exact method, get types |
| **ISymbol** | A declared thing: type, method, property, parameter, local | `model.GetSymbolInfo(node).Symbol`, `GetDeclaredSymbol` | Identity of methods/properties across files |
| **IOperation** | Language-neutral, semantic tree of *what the code does* (invocation, assignment, binary op, conditional, return…) | `model.GetOperation(node)` | Data-flow (taint), guards, sinks — **your main tool** |

Also useful: `SymbolFinder` (find implementations/references across the solution), `ControlFlowGraph` (post-IPD dominance analysis).

### Kata (S0-06) — do these in `playground/RoslynKata`
```csharp
// 1) syntax: print method names
var tree = CSharpSyntaxTree.ParseText(code);
foreach (var m in tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
    Console.WriteLine(m.Identifier.Text);

// 2) semantic: types of locals
var refs = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
    .Split(Path.PathSeparator).Select(p => MetadataReference.CreateFromFile(p));
var comp = CSharpCompilation.Create("Kata", new[] { tree }, refs);
var model = comp.GetSemanticModel(tree);
foreach (var v in tree.GetRoot().DescendantNodes().OfType<VariableDeclaratorSyntax>())
    Console.WriteLine($"{v.Identifier} : {((ILocalSymbol)model.GetDeclaredSymbol(v)!).Type}");

// 3) resolved invocations
foreach (var inv in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
    if (model.GetSymbolInfo(inv).Symbol is IMethodSymbol ms)
        Console.WriteLine($"{ms.ContainingType.ToDisplayString()}.{ms.Name}");

// 4) operation tree
class Printer : OperationWalker
{
    int _depth;
    public override void DefaultVisit(IOperation op)
    { Console.WriteLine($"{new string(' ', _depth * 2)}{op.Kind}: {op.Syntax.ToString().Split('\n')[0]}");
      _depth++; base.DefaultVisit(op); _depth--; }
}
var method = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().First();
new Printer().Visit(model.GetOperation(method));

// 5) find implementations (needs a Solution, e.g. AdhocWorkspace - see §12)
var impls = await SymbolFinder.FindImplementationsAsync(interfaceMethodSymbol, solution);
```
Run #4 on a BolaLab action and **keep the printout** — it is the best way to learn which `OperationKind`s you must handle.

---

## 2. Loading solutions (S0-08)

```csharp
// BolaLens.Analysis/Loading/MsBuildBootstrap.cs
using Microsoft.Build.Locator;

/// <summary>Registers the installed .NET SDK's MSBuild exactly once.
/// MUST run before any type from Microsoft.Build / MSBuildWorkspace is touched.</summary>
public static class MsBuildBootstrap
{
    private static readonly object Gate = new();
    public static void EnsureRegistered()
    {
        lock (Gate)
        {
            if (!MSBuildLocator.IsRegistered) MSBuildLocator.RegisterDefaults();
        }
    }
}
```

```csharp
// BolaLens.Analysis/Loading/WorkspaceLoader.cs
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

public sealed class LoadedSolution : IDisposable
{
    public required MSBuildWorkspace Workspace { get; init; }
    public required Solution Solution { get; init; }
    public required string RootDirectory { get; init; }
    public required IReadOnlyList<UnresolvedItem> Diagnostics { get; init; }
    public void Dispose() => Workspace.Dispose();
}

/// <summary>Opens a .sln/.slnx/.csproj with MSBuildWorkspace and records load failures.
/// Limitation: the target must be restorable (run `dotnet restore` first).</summary>
public sealed class WorkspaceLoader
{
    public async Task<LoadedSolution> LoadAsync(string path, CancellationToken ct)
    {
        var full = Path.GetFullPath(path);
        if (!File.Exists(full)) throw new FileNotFoundException("Target not found", full);

        var workspace = MSBuildWorkspace.Create();
        var diags = new List<UnresolvedItem>();
        // Newer Roslyn: RegisterWorkspaceFailedHandler. Older: workspace.WorkspaceFailed += (_, e) => ...
        workspace.RegisterWorkspaceFailedHandler(e =>
            diags.Add(new UnresolvedItem($"{e.Diagnostic.Kind}: {e.Diagnostic.Message}", null)));

        Solution solution = Path.GetExtension(full).ToLowerInvariant() switch
        {
            ".csproj"          => (await workspace.OpenProjectAsync(full, cancellationToken: ct)).Solution,
            ".sln" or ".slnx"  => await workspace.OpenSolutionAsync(full, cancellationToken: ct),
            var ext            => throw new NotSupportedException($"Unsupported input '{ext}'")
        };

        return new LoadedSolution
        {
            Workspace = workspace, Solution = solution,
            RootDirectory = Path.GetDirectoryName(full)!, Diagnostics = diags
        };
    }
}
```

`inventory` command prints, per project: name, file path, document count, and `compilation.GetDiagnostics().Count(d => d.Severity == DiagnosticSeverity.Error)`. **Many compile errors = unresolved references = unreliable analysis** → print a warning and record it in the coverage manifest.

Helper you will use everywhere:
```csharp
public static class SourceRefs
{
    public static SourceRef From(Location loc, string root)
    {
        var span = loc.GetLineSpan();
        return new SourceRef(Path.GetRelativePath(root, span.Path),
                             span.StartLinePosition.Line + 1, span.EndLinePosition.Line + 1);
    }
    public static SourceRef From(SyntaxNode node, string root) => From(node.GetLocation(), root);
}
```

---

## 3. Endpoint enumerator (S1-02)

**Algorithm**
1. For each C# project → compilation. Resolve `Microsoft.AspNetCore.Mvc.ControllerBase` and `Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute` with `GetTypeByMetadataName`. If `ControllerBase` is null → not an MVC project → skip (record in coverage).
2. Walk all **source** types of the assembly (`compilation.Assembly.GlobalNamespace`, recursive). Keep non-abstract classes that inherit `ControllerBase`.
3. Class route: `[Route("api/[controller]")]` → replace `[controller]` with class name minus `Controller`, `[action]` with method name.
4. For each public, non-static, ordinary method: for each attribute whose class inherits `HttpMethodAttribute` → verb from class name (`HttpPutAttribute` → Put), template from `ConstructorArguments[0]` (may be absent). Combine with class route unless template starts with `/` or `~/`. Also honour a method-level `[Route]`.
5. Normalise for matching: lower-case, trim `/`, strip constraints `{id:int}` → `{id}`.
6. Parameters → binding source: explicit `[FromRoute|FromBody|FromQuery|FromHeader|FromServices]`; else name appears in template → Route; else simple type (primitive, string, Guid, DateTime, enum) → Query; else → Body.
7. ID-like: route/query parameter whose name matches `idParameterPatterns`; body DTO → inspect its properties for id-like names and record `IdPropertyPath` (e.g. `request.OrderId`).
8. Authorization: `[Authorize]` / `[AllowAnonymous]` on method or class (method wins). Read `Policy` and `Roles` from `NamedArguments`.

```csharp
static bool InheritsFrom(ITypeSymbol? type, ITypeSymbol? baseType)
{
    for (var t = type; t is not null; t = t.BaseType)
        if (SymbolEqualityComparer.Default.Equals(t.OriginalDefinition, baseType)) return true;
    return false;
}

static IEnumerable<INamedTypeSymbol> SourceTypes(INamespaceSymbol ns)
{
    foreach (var member in ns.GetMembers())
    {
        if (member is INamespaceSymbol child)
            foreach (var t in SourceTypes(child)) yield return t;
        else if (member is INamedTypeSymbol type && type.Locations.Any(l => l.IsInSource))
        {
            yield return type;
            foreach (var nested in type.GetTypeMembers()) yield return nested;
        }
    }
}

static HttpVerb VerbOf(INamedTypeSymbol attrClass) => attrClass.Name switch
{
    "HttpGetAttribute" => HttpVerb.Get,   "HttpPostAttribute" => HttpVerb.Post,
    "HttpPutAttribute" => HttpVerb.Put,   "HttpPatchAttribute" => HttpVerb.Patch,
    "HttpDeleteAttribute" => HttpVerb.Delete, _ => HttpVerb.Unknown
};
```
**Tests:** `[controller]` replacement; absolute template; `{id:int}` normalisation; `[FromBody] UpdateOrderRequest` with `OrderId` property; class-level `[Authorize]` + method-level `[AllowAnonymous]`.

**Known limitation (write it down):** Minimal APIs (`app.MapPut("/x/{id}", …)`) are not covered before the IPD → counted as "unsupported endpoint style" in coverage.

---

## 4. Global Policy Mapper (S1-03)

Scan **all** invocations in the solution (cheap enough) and match by resolved method name + namespace prefix `Microsoft.`:

| Pattern | Record |
|---|---|
| `AddAuthentication(...)`, `AddJwtBearer`, `AddCookie`, `AddOpenIdConnect` | authentication schemes |
| `AddAuthorization(o => { o.FallbackPolicy = …; o.DefaultPolicy = …; o.AddPolicy("n", b => …) })` | fallback/default/policies |
| `AddAuthorizationBuilder().AddPolicy(…).SetFallbackPolicy(…)` | same, builder style |
| `…MapControllers().RequireAuthorization(…)` | global endpoint authorization |
| `AddControllers(o => o.Filters.Add(new AuthorizeFilter(…)))` | global authorize filter |
| class deriving `AuthorizationHandler<TReq, TResource>` | **resource handler** (TC09) — record requirement + resource type |

For each `AddPolicy("name", builder => …)`: walk the lambda's operations, collect invoked method names on the builder (`RequireAuthenticatedUser`, `RequireClaim`, `RequireRole`, `AddRequirements` + requirement type). Detect "requires authenticated user" in a fallback assignment by looking for `RequireAuthenticatedUser` anywhere in the assigned expression.

Anything you cannot interpret (policy name from a variable, policies loaded from config) → `UnresolvedItem("dynamic policy registration", location)`. That honesty is a feature.

Resource handlers go into `PolicyMap.ResourceHandlers` (`ResourceHandlerInfo` record, see `04` §5): for each source class whose base type's `OriginalDefinition` is `AuthorizationHandler<TRequirement, TResource>`, record both type arguments.

Note: the policy map mostly tells you **authentication/coarse** protection. It almost never proves object ownership — that is exactly the point of the project (your slide 4 story). It is used to (a) mark endpoints that are anonymous (higher severity), (b) find resource handlers.

---

## 5. DbContext Ownership Schema Mapper (S1-04)

1. **Contexts:** source classes inheriting `Microsoft.EntityFrameworkCore.DbContext`.
2. **Entities:** properties of type `DbSet<T>` (compare `property.Type.OriginalDefinition` with `compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.DbSet`1")`) → `T`. Also `modelBuilder.Entity<T>()` type arguments.
3. **Query filters:** find **every** invocation named `HasQueryFilter` (covers `OnModelCreating` *and* `IEntityTypeConfiguration<T>.Configure`). Entity = type argument of the containing `EntityTypeBuilder<T>` (`((INamedTypeSymbol)target.ContainingType).TypeArguments[0]`). Get the lambda with `invocation.Arguments.SelectMany(a => a.Value.DescendantsAndSelf()).OfType<IAnonymousFunctionOperation>().First()` (works for both `HasQueryFilter(lambda)` and EF 10 named filters `HasQueryFilter("key", lambda)`).
4. **Referenced properties:** inside the lambda, every `IPropertyReferenceOperation` whose `Instance` is an `IParameterReferenceOperation` to the lambda's parameter → entity property name. Classify filter scope: property matches tenant pattern → Tenant; user pattern → User. Note what the other side references (`_currentUser.TenantId`, a context field) — record its text.
5. **Owner candidates** (per entity, including inherited properties):
   - name matches `userOwnerPatterns` / `tenantOwnerPatterns` → `NamePattern`;
   - navigation property whose type name is in `userTypeNames` (+ its FK `XId`) → `ForeignKeyToUserType`;
   - referenced in a query filter → `QueryFilter`.
6. **Keys:** property `Id`, `{TypeName}Id`, `[Key]`, or `HasKey(e => e.X)`.

Expected BolaLab result is in `06` §7. A test per entity.

**Limitation to state:** ownership is a *candidate* relation — the schema cannot prove intended policy (e.g. a field called `OwnerId` used as business metadata). That is why labels are human-audited and why the SLM exists.

---

## 6. DI registration map & implementation resolver (S2-01)

```csharp
/// <summary>Maps service interfaces to implementation types from AddScoped/AddTransient/AddSingleton.</summary>
public sealed class DiRegistrationMap
{
    private readonly Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>> _map = new(SymbolEqualityComparer.Default);

    public void Collect(IInvocationOperation inv)
    {
        var m = inv.TargetMethod;
        if (m.Name is not ("AddScoped" or "AddTransient" or "AddSingleton" or "TryAddScoped" or "TryAddTransient" or "TryAddSingleton")) return;
        if (m.ContainingNamespace?.ToDisplayString().StartsWith("Microsoft.Extensions.DependencyInjection") != true) return;

        if (m.TypeArguments.Length == 2 &&
            m.TypeArguments[0] is INamedTypeSymbol svc && m.TypeArguments[1] is INamedTypeSymbol impl)
            Add(svc, impl);
        else
        {
            var typeofs = inv.Arguments.Select(a => a.Value).OfType<ITypeOfOperation>().ToList();
            if (typeofs.Count == 2 && typeofs[0].TypeOperand is INamedTypeSymbol s && typeofs[1].TypeOperand is INamedTypeSymbol i)
                Add(s, i); // open generics: typeof(IRepository<>), typeof(Repository<>)
        }
    }
    private void Add(INamedTypeSymbol s, INamedTypeSymbol i)
    {
        var key = s.OriginalDefinition;
        if (!_map.TryGetValue(key, out var list)) _map[key] = list = new();
        list.Add(i.OriginalDefinition);
    }
    public IReadOnlyList<INamedTypeSymbol> ImplementationsOf(INamedTypeSymbol service)
        => _map.TryGetValue(service.OriginalDefinition, out var l) ? l : Array.Empty<INamedTypeSymbol>();
}
```

**Resolver algorithm** for a call target `IMethodSymbol target`:
1. If `target` has a body in source (`DeclaringSyntaxReferences.Length > 0`, not abstract) → use it.
2. If `target.ContainingType.TypeKind == Interface` (or abstract/virtual):
   a. DI map → implementation type(s); for each `impl.FindImplementationForInterfaceMember(target)`;
   b. else `SymbolFinder.FindImplementationsAsync(target, solution)` filtered to source symbols.
3. Exactly one → resolved. More than one and DI map does not disambiguate → `Unresolved("ambiguous dispatch: N implementations")` (TC10). Zero → `Unresolved("no source implementation (external library?)")`.
4. Normalise symbols across projects with `SymbolFinder.FindSourceDefinitionAsync(symbol, solution)` before using them as dictionary keys.

Get the body to analyse: `var syntax = await method.DeclaringSyntaxReferences[0].GetSyntaxAsync(ct); var doc = solution.GetDocument(syntax.SyntaxTree); var model = await doc!.GetSemanticModelAsync(ct); var body = model!.GetOperation(syntax);` — cache `SemanticModel` per `SyntaxTree`.

---

## 7. Caller-identity recogniser (S2-02)

Answers: *is this expression derived from the authenticated, server-side identity?*

Trusted if (recursively):
- `IPropertyReferenceOperation` to `User` of type `System.Security.Claims.ClaimsPrincipal` on a `ControllerBase`/`HttpContext`/`PageModel`;
- `IInvocationOperation` named `FindFirst`, `FindFirstValue`, `HasClaim`, or a configured extension (`GetUserId`) whose receiver/first argument is trusted;
- `.Value` of a `Claim` obtained from a trusted principal;
- member of a configured current-user type (`ICurrentUser.UserId`, `.TenantId`) — from `bolalens.json`;
- `IConversionOperation` / `int.Parse(x)` / `Guid.Parse(x)` / `Convert.ToInt32(x)` / null-forgiving / `?? ` whose input is trusted;
- local/parameter symbol already in `TaintState.Identity`.

**Not** trusted: anything from action parameters, DTO properties, headers, query strings — even if named `UserId` (TC03). Keep a `IdentityKind` (User vs Tenant) on the recognised value: `TenantId` members → Tenant.

---

## 8. Taint walker & path extractor (S2-03, S2-04)

### 8.1 State
```csharp
public sealed class TaintState
{
    public HashSet<ISymbol> Tainted  { get; } = new(SymbolEqualityComparer.Default); // client-controlled id
    public Dictionary<ISymbol, OwnerKind> Identity { get; } = new(SymbolEqualityComparer.Default); // trusted caller id
    public Dictionary<ISymbol, EntityRef> Selected { get; } = new(SymbolEqualityComparer.Default); // entity chosen by tainted id
}

public sealed record EntityRef(
    ITypeSymbol EntityType, bool Detached, bool BypassedFilters,
    bool OwnerPredicate, OwnerKind? OwnerPredicateKind, SourceRef LoadedAt);

public sealed record MethodSummary(
    bool ReturnsTainted, EntityRef? ReturnsEntity,
    IReadOnlyList<SinkInfo> Sinks, IReadOnlyList<GuardEvidence> Guards,
    IReadOnlyList<PathStep> Steps, IReadOnlyList<UnresolvedItem> Unresolved);
```

### 8.2 Intra-procedural walk
Subclass `OperationWalker` and visit the method body (`model.GetOperation(methodSyntax)` → `IMethodBodyOperation`). Statement order inside blocks = visit order, so straight-line "load → check → save" is naturally ordered. The analysis is **path-insensitive** (branches merged) — state that as a limitation; S6 adds CFG dominance.

| Operation | Action |
|---|---|
| `IVariableDeclaratorOperation` with initializer | if `IsTainted(init)` → add local to `Tainted`; if `IsIdentity(init)` → `Identity`; if `TryEntityLoad(init, out e)` → `Selected[local] = e`; if `new T { Id = tainted }` where T is an entity → `Selected[local] = e with Detached = true` |
| `ISimpleAssignmentOperation` | same propagation to the target local/field; if target is a property of a `Selected` local → record `PathStep(Assignment, "mutates tracked entity")` and remember the entity is *modified* |
| `IInvocationOperation` | (1) EF classifier (§9) → sink/load; (2) `IAuthorizationService.AuthorizeAsync` → candidate resource guard; (3) source method with any tainted/selected/identity argument → **inter-procedural** (§8.4); (4) otherwise, if a tainted argument flows into an unresolved call → `Unresolved` |
| `IInvocationOperation` with **no** relevant arguments (e.g. `repo.SaveAsync()`) | still resolve it and ask a cached, depth-bounded "commit summary": does the callee (transitively) call `SaveChanges*`? If yes → record a `Commit` step. Without this, TC13 (save hidden in a repository method) is missed |
| `IConditionalOperation` | hand to `GuardDetector` (§10) with current state, then continue visiting both branches |
| `IReturnOperation` | record whether the returned value is tainted / a selected entity (feeds the summary) |

```csharp
bool IsTainted(IOperation op) => op switch
{
    IParameterReferenceOperation p => _state.Tainted.Contains(p.Parameter),
    ILocalReferenceOperation l     => _state.Tainted.Contains(l.Local),
    IConversionOperation c         => IsTainted(c.Operand),
    IAwaitOperation a              => IsTainted(a.Operation),
    IPropertyReferenceOperation pr => pr.Instance is not null && IsTainted(pr.Instance) && IsIdLike(pr.Property.Name),
    IInvocationOperation inv when IsParseLike(inv.TargetMethod) => inv.Arguments.Any(a => IsTainted(a.Value)),
    _ => false
};
```
(For a `[FromBody]` DTO source, add the DTO parameter to `Tainted`; `IsIdLike` restricts which of its properties carry the ID.)

### 8.3 Recognising "entity selected by a tainted ID" (`TryEntityLoad`)
1. Unwrap `IAwaitOperation`, `IConversionOperation`, `ICoalesceOperation` (`?? throw …`, TC13) and null-forgiving/parenthesised wrappers.
2. Expect an `IInvocationOperation` classified as **Load** (`Find/FindAsync/First*/Single*/Last*` …).
3. Walk the **receiver chain** to its root. For extension methods the receiver is `Arguments[0].Value` (Instance is null); for instance methods it is `Instance`. Along the chain collect: predicates (lambdas passed to `Where/First*/Single*/Any`), `IgnoreQueryFilters` (→ `BypassedFilters`), `Include` (ignore), root `DbSet<T>` property or `Set<T>()` (→ entity type).
4. Tainted key? `Find(id)` → any argument tainted. Predicate lambda → contains `IBinaryOperation(Equals)` with one side a key property of the lambda parameter and the other side `IsTainted` (captured outer locals/params appear as `ILocalReferenceOperation`/`IParameterReferenceOperation` inside the lambda — same symbols).
5. Owner predicate? predicate *also* contains `Equals` between an owner-candidate property and an `IsIdentity` expression → `OwnerPredicate = true`, kind = that owner kind.

### 8.4 Inter-procedural propagation
```
Analyse(method, taintedParams, selectedParams, identityParams, depth):
    if depth > maxCallDepth: return Unresolved("max call depth")
    key = (method, masks); if cache has key: return cache[key]
    state = new TaintState seeded from masks
    walk body; on call to source method M with relevant args:
        target = Resolve(M)                       // §6
        sub = Analyse(target, mapped masks, depth+1)
        append sub.Steps (as Call step + nested), sub.Sinks, sub.Guards, sub.Unresolved
        if call result assigned: propagate sub.ReturnsTainted / sub.ReturnsEntity to the local
    cache[key] = summary; return summary
```
Argument → parameter mapping: `IArgumentOperation.Parameter.Ordinal` gives you the callee parameter. Primary-constructor services (`class OrderService(AppDbContext db)`) appear as `IParameterReferenceOperation` — fine, their *type* drives resolution.

`PathExtractor` = for each endpoint with an id-like parameter: seed `Tainted` with it, call `Analyse(action, …, depth: 0)`, wrap into `ExecutionPath` (stable `Id` = hash of endpoint + source param).

---

## 9. EF Core operation classifier (S2-05)

Classify by the resolved `IMethodSymbol` (never by text):

```csharp
static bool IsEf(IMethodSymbol m) =>
    m.ContainingNamespace?.ToDisplayString().StartsWith("Microsoft.EntityFrameworkCore") == true;
static bool IsQueryable(IMethodSymbol m) =>
    m.ContainingType?.ToDisplayString() == "System.Linq.Queryable";
```

| Category | Methods (resolved symbol name) | Notes |
|---|---|---|
| Load (single object) | `Find`, `FindAsync`, `First*`, `Single*`, `Last*` (+ `OrDefault`, `Async`) | `Queryable.*` sync versions + EF `*Async` extensions |
| Chain | `Where`, `Include`, `ThenInclude`, `AsTracking`, `AsNoTracking`, `OrderBy` | `AsNoTracking` + later `Update(entity)` = detached-style write |
| Bypass | `IgnoreQueryFilters` | sets `BypassedFilters` |
| Write | `Update`, `UpdateRange`, `Remove`, `RemoveRange`, `Attach`, `AttachRange` on `DbSet<T>` or `DbContext` | sink entity = argument's selected entity |
| Entry state | assignment to `EntityEntry.State` (`_db.Entry(x).State = EntityState.Modified/Deleted`) | Update/Remove sink |
| Commit | `SaveChanges`, `SaveChangesAsync` | state-changing if preceded by Write or a mutation of a tracked selected entity |
| Bulk | `ExecuteUpdate`, `ExecuteUpdateAsync`, `ExecuteDelete`, `ExecuteDeleteAsync` | sink entity = chain root; owner predicate from chain `Where`; query filters apply unless bypassed |
| Create | `Add`, `AddAsync`, `AddRange` | **out of primary scope** (no existing object selected) — record but do not flag |

Store the symbol display string in `SinkInfo.Symbol` (shown in the report and slice).

---

## 10. Guard detector (S2-06)

A guard is **valid** only if it binds **(trusted caller identity) ↔ (owner property) ↔ (the same object that reaches the sink)** and **precedes the sink with an exit**.

| Kind | Detection | Validity checks |
|---|---|---|
| `OwnerEquality` | `IConditionalOperation` whose condition contains `IBinaryOperation` (`Equals`/`NotEquals`, incl. string `==` operator, or `string.Equals(a,b)` / `a.Equals(b)` invocation) with one side `IPropertyReferenceOperation(owner prop)` whose instance is a `Selected` local, other side `IsIdentity` | `NotEquals` → `WhenTrue` contains `IReturnOperation`/`IThrowOperation`; `Equals` → sink lies inside `WhenTrue`. Same symbol as sink's entity local. Guard line < sink line (same method) or guard in a caller step before the call (path step order). Owner kind matches identity kind. |
| `LinqOwnerPredicate` | `EntityRef.OwnerPredicate == true` on the loaded/bulk entity | identity kind matches the owner kind required |
| `ResourceAuthorization` | `IAuthorizationService.AuthorizeAsync(User, resource, policyOrRequirement)` where `resource` is the selected entity, and its result (`.Succeeded`) gates an exit before the sink | record handler (from PolicyMap) → `IsValid = true`, flagged "delegated" |
| `QueryFilter` | from schema (§5): entity has filter whose scope kind = required owner kind; not bypassed; not detached | added by the classifier, not the walker |

Record **invalid** guards too, with `InvalidReason`:
- `"comparand is client-controlled"` (TC03),
- `"guard checks a different object"` (TC04),
- `"guard does not precede the state change"` (TC05),
- `"tenant-level check but object has a user owner"` (TC11).

Invalid guards make *great* explanations in the report and slides.

**Required owner kind:** if the entity has any `User` owner candidate → `User`; else if it has a `Tenant` candidate → `Tenant`; else none (→ SYM-09).

---

## 11. Symbolic verdict rules (S3-01) — explain this table at the IPD

Evaluated **in this order**; first match wins; the rule ID is written to the report.

| Order | Rule | Condition | Verdict |
|---|---|---|---|
| 1 | SYM-01 | No state-changing sink **and** no unresolved call that received tainted data | OutOfScope (read-only / no-op) |
| 2 | SYM-02 | No state-changing sink found **but** tainted data entered an unresolved/ambiguous/too-deep call | Unknown |
| 3 | SYM-03 | A valid `OwnerEquality` or `LinqOwnerPredicate` guard of the required owner kind protects the sink entity | Protected |
| 4 | SYM-05 | A valid `ResourceAuthorization` guard gates the sink | Protected (delegated → also sent to SLM) |
| 5 | SYM-06 | Sink entity loaded with `IgnoreQueryFilters` and no valid guard | Vulnerable |
| 6 | SYM-07 | Detached write (`Attach/Update` on an entity constructed from request data) or bulk write without owner predicate, and no valid guard | Vulnerable |
| 7 | SYM-04 | Applicable query filter of the required owner kind, not bypassed, not detached | Protected |
| 8 | SYM-09 | Sink entity has no owner candidate | Unknown ("no ownership model") |
| 9 | SYM-08 | Tainted ID selects an owned entity that reaches a state-changing sink with no valid guard / filter | Vulnerable |

Expected outcomes on BolaLab are in `06` §6 — write one unit test per rule with a minimal snippet.

---

## 12. Test infrastructure (S1-07)

```csharp
// tests/BolaLens.Analysis.Tests/Infrastructure/CompilationFactory.cs
public static class CompilationFactory
{
    private static readonly Lazy<MetadataReference[]> Refs = new(() =>
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .ToArray());

    /// <summary>Builds an in-memory solution with ASP.NET Core + EF Core references available.</summary>
    public static Solution Create(params string[] sources)
    {
        var ws = new AdhocWorkspace();
        var pid = ProjectId.CreateNewId();
        var solution = ws.CurrentSolution
            .AddProject(pid, "Fixture", "Fixture", LanguageNames.CSharp)
            .WithProjectCompilationOptions(pid, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReferences(pid, Refs.Value);
        for (var i = 0; i < sources.Length; i++)
            solution = solution.AddDocument(DocumentId.CreateNewId(pid), $"F{i}.cs", sources[i]);
        return solution;
    }

    public static async Task AssertCompilesAsync(Solution s)
    {
        var errors = (await s.Projects.Single().GetCompilationAsync())!
            .GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        errors.ShouldBeEmpty(string.Join("\n", errors));
    }
}
```
The test project has `<FrameworkReference Include="Microsoft.AspNetCore.App" />` and a package reference to `Microsoft.EntityFrameworkCore.Relational`, so their assemblies are in the trusted-platform list. If a type is still missing, add `MetadataReference.CreateFromFile(typeof(DbContext).Assembly.Location)` explicitly.

**Always call `AssertCompilesAsync` first** in each test — otherwise a typo in the test snippet silently produces error symbols and a misleading result.

Integration tests (`BolaLens.Integration.Tests`) load `fixtures/BolaLab/BolaLab.sln` through the real `WorkspaceLoader` (call `MsBuildBootstrap.EnsureRegistered()` in a static constructor / module initializer) and compare the report with `expected.json`. Use **Verify** snapshots for `paths.json` so changes are reviewed consciously.

---

## 13. IR slice generator (S3-04)

Purpose: a compact, faithful, *label-free* textual view of one path that fits a small model's context.

```
L1  [ENDPOINT] PUT api/orders/{id}/status  -> OrdersController.UpdateStatus   (Controllers/OrdersController.cs:31)
L2  [POLICY]   [Authorize] default; fallback=authenticated-user; scheme=JwtBearer
L3  [SOURCE]   id : int  (route, client-controlled)
L4  [CALLER]   var userId = _currentUser.UserId;   (trusted identity: User)            (OrdersController.cs:33)
L5  [LOAD]     var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == id);     (OrdersController.cs:34)
L6  [SCHEMA]   Order owners: UserId(User), TenantId(Tenant); query filters: none
L7  [CHECK]    if (order == null) return NotFound();                                    (OrdersController.cs:35)
L8  [MUTATE]   order.Status = request.Status;                                          (OrdersController.cs:36)
L9  [SINK]     await _db.SaveChangesAsync();   (state-changing: Order)                  (OrdersController.cs:37)
```
Rules:
- One line per `PathStep`; text = the **statement** containing the step (`node.FirstAncestorOrSelf<StatementSyntax>()`), whitespace-normalised.
- **Remove comments** (`CSharpSyntaxRewriter` dropping comment/doc trivia) and shorten string literals > 40 chars.
- Provenance annotations are *facts* (`client-controlled`, `trusted identity`) — **never** include the symbolic verdict or rule IDs (keeps the semantic stage independent → fair ablation).
- Include every condition on the path (`[CHECK]`), valid or invalid, without saying which is which.
- Include `[UNRESOLVED]` lines with reasons.
- Keep a `SourceMap: L# → SourceRef` for evidence checking and report anchoring.
- Budget: Phi-3-mini-4k → keep the slice ≤ ~700 tokens; if longer, drop `[CALL]` hops without effects first, then truncate with `[TRUNCATED n lines]` (counted in coverage).

---

## 14. Local SLM validator with ONNX Runtime GenAI (S3-05, S4-01)

```csharp
using Microsoft.ML.OnnxRuntimeGenAI;

/// <summary>Runs a local ONNX small language model over a prompt and returns the raw text.
/// Greedy decoding for repeatability; stops early when a complete JSON object is produced.</summary>
public sealed class OnnxTextGenerator : IDisposable
{
    private readonly OgaHandle _oga = new();   // keeps native runtime alive for app lifetime
    private readonly Model _model;
    private readonly Tokenizer _tokenizer;

    public OnnxTextGenerator(string modelDir)
    {
        _model = new Model(modelDir);          // folder containing genai_config.json + *.onnx
        _tokenizer = new Tokenizer(_model);
    }

    public (string Text, int PromptTokens, int OutputTokens) Generate(string prompt, int maxLength, CancellationToken ct)
    {
        using var sequences = _tokenizer.Encode(prompt);
        using var gp = new GeneratorParams(_model);
        gp.SetSearchOption("max_length", maxLength);   // prompt + output tokens
        gp.SetSearchOption("do_sample", false);        // greedy
        using var generator = new Generator(_model, gp);
        generator.AppendTokenSequences(sequences);

        using var stream = _tokenizer.CreateStream();
        var sb = new StringBuilder();
        int outTokens = 0, depth = 0; bool started = false;
        while (!generator.IsDone())
        {
            ct.ThrowIfCancellationRequested();
            generator.GenerateNextToken();
            var seq = generator.GetSequence(0);
            var piece = stream.Decode(seq[^1]);
            outTokens++;
            sb.Append(piece);
            foreach (var ch in piece)                    // early stop at end of first JSON object
            {
                if (ch == '{') { depth++; started = true; }
                else if (ch == '}') depth--;
            }
            if (started && depth == 0) break;
        }
        return (sb.ToString(), sequences[0].Length, outTokens);
    }

    public void Dispose() { _tokenizer.Dispose(); _model.Dispose(); _oga.Dispose(); }
}
```
API notes: this matches the current GenAI C# API (`AppendTokenSequences`, `GenerateNextToken`); older 0.4/0.5 samples used `SetInputSequences`/`ComputeLogits` — ignore those. Check the package's README for the exact version you pinned. Newer versions may support **constrained (JSON-schema) decoding** via a guidance option — if your version has it, use it and note it as an improvement; otherwise rely on §16 validation.

`OnnxSemanticValidator : ISemanticValidator` wraps it: build prompt (§15) → generate → parse (§16) → `SemanticVerdict` with `LatencyMs` (Stopwatch) and `ModelId`. Load the model **once** per scan (cold load time recorded separately).

---

## 15. Prompt template v1 (S3-06)

Store as an embedded resource `prompt-v1.txt`; record `PromptVersion` in the report. Phi-3 chat format:

```
<|user|>
You are a security code reviewer. Decide whether this ASP.NET Core API path lets an authenticated user change an object that belongs to ANOTHER user or tenant (horizontal, state-changing Broken Object Level Authorization).

Facts:
- "trusted identity" values come from the server-side authenticated user.
- "client-controlled" values come from the HTTP request and can be forged.
- [SCHEMA] lists owner properties (User or Tenant) and EF Core global query filters.

Decide:
- "protected": before the change, an effective check or query predicate/filter binds the caller's trusted identity to the SAME object at the owner level shown in [SCHEMA] (a User owner needs a user-level check; a tenant filter alone is not enough).
- "vulnerable": a client-controlled id selects the object and it is changed without such a binding.
- "unknown": evidence is missing (e.g. [UNRESOLVED] lines).

Reply with ONLY one JSON object:
{"status":"vulnerable|protected|unknown","confidence":0.0,"reason":"max 40 words","evidence":["L#"]}

### Example A
{calibration slice A}
### Answer A
{"status":"vulnerable","confidence":0.9,"reason":"...","evidence":["L3","L5","L8"]}

### Example B
{calibration slice B}
### Answer B
{"status":"protected","confidence":0.9,"reason":"...","evidence":["L4","L6"]}

### Task
{slice}
### Answer
<|end|>
<|assistant|>
```
Few-shot examples come **only** from `fixtures/Calibration/` (separate tiny code, different entity names) — never from TC01–TC16 or evaluation repos.

---

## 16. Response parsing, evidence check & fusion (S4-01, S4-02)

**Parser** (`SemanticResponseParser`):
1. Take substring from the first `{` to its matching `}` (count braces, ignore braces inside string literals).
2. `JsonDocument.Parse`; require `status` ∈ {vulnerable, protected, unknown}; `confidence` number in [0,1]; `reason` non-empty ≤ 400 chars; `evidence` array of `L\d+`.
3. **Grounding check:** keep only evidence IDs that exist in the slice. If status ≠ unknown and no valid evidence remains → status = Unknown, reason "ungrounded answer".
4. Failure → retry once with the suffix "Return only the JSON object." → still failing → `ParseOk = false`, status Unknown.

**Fusion** (`FusionPolicy`, τ = `suppressThreshold`, default 0.80, tuned later on the calibration split only):

| Symbolic | Semantic | Final | Final reason |
|---|---|---|---|
| Vulnerable | Vulnerable | Vulnerable | agree |
| Vulnerable | Protected, conf ≥ τ, grounded | **Protected** | "suppressed by SLM" ← the false-positive reduction under test (RQ2) |
| Vulnerable | Protected < τ / Unknown / parse fail | Vulnerable | "SLM not confident" |
| Unknown | Vulnerable ≥ τ | Vulnerable | "escalated by SLM" |
| Unknown | anything else | Unknown | — |
| Protected (SYM-05 delegated) | Vulnerable ≥ τ | Vulnerable | "SLM disputes delegated check" |
| Protected (other) | not invoked | Protected | — |
| OutOfScope | not invoked | OutOfScope | — |

Symbolic-only mode = `NullSemanticValidator` → Final = Symbolic. Same candidates, same labels → clean ablation.

---

## 17. Evaluation maths (S4-04)

Unit key = `VERB normalised-route` (+ sink entity if one endpoint has several sinks). Positive prediction = Final == Vulnerable.

| Label \ Prediction | Vulnerable | Protected / OutOfScope | Unknown |
|---|---|---|---|
| Vulnerable | TP | FN | U_vuln (counted as FN in *operational* recall) |
| Protected | FP | TN | U_prot |

- Precision = TP / (TP + FP)
- Recall (decided) = TP / (TP + FN); **Operational recall** = TP / (TP + FN + U_vuln)
- F1 = 2TP / (2TP + FP + FN)
- FPR = FP / (FP + TN) — undefined (print "n/a") if FP + TN = 0
- FDR = FP / (TP + FP) (so you never repeat the BolaRay FPR/FDR confusion)
- Coverage = decided units / eligible units
- Labels `Unknown` / `OutOfScope` are excluded from the matrix and listed separately.

Print one Markdown table per (dataset, mode). Never print 0 % when the denominator is 0.

---

## 18. CLI wiring (System.CommandLine 2.0 GA)

```csharp
// src/BolaLens.Cli/Program.cs
using System.CommandLine;
using BolaLens.Analysis.Loading;

MsBuildBootstrap.EnsureRegistered();   // FIRST LINE - before any MSBuild type is loaded

var root = new RootCommand("BolaLens - context-aware BOLA detection for ASP.NET Core + EF Core");
root.Subcommands.Add(InventoryCommand.Create());
root.Subcommands.Add(ScanCommand.Create());
// ... endpoints, policy, schema, paths, slice, eval
return await root.Parse(args).InvokeAsync();
```
```csharp
public static class ScanCommand
{
    public static Command Create()
    {
        var path = new Argument<FileInfo>("path") { Description = ".sln, .slnx or .csproj to analyse" };
        var mode = new Option<string>("--mode") { Description = "symbolic | hybrid", DefaultValueFactory = _ => "symbolic" };
        mode.AcceptOnlyFromAmong("symbolic", "hybrid");
        var output = new Option<FileInfo?>("--out") { Description = "Write report JSON here" };
        var config = new Option<FileInfo?>("--config") { Description = "bolalens.json" };

        var cmd = new Command("scan", "Run the full pipeline") { path, mode, output, config };
        cmd.SetAction(async (ParseResult r, CancellationToken ct) =>
        {
            var sw = Stopwatch.StartNew();
            // 1 load -> 2 symbolic pipeline -> 3 slices -> 4 validator (Null or Onnx) -> 5 fusion -> 6 report
            // record sw.Elapsed and Process.GetCurrentProcess().PeakWorkingSet64 in RunInfo
            return 0; // 1 if any Final == Vulnerable, 2 on load error
        });
        return cmd;
    }
}
```
If an API name differs in your pinned version, check the System.CommandLine docs for 2.0 (GA, Nov 2025) — beta-era samples (`SetHandler`, `CommandLineBuilder`) are obsolete.

---

## 19. Pitfalls & performance (read before you get stuck)

1. **"MSBuild assemblies were already loaded"** → `MsBuildBootstrap` must run first; don't reference `Microsoft.Build*` packages directly (if you must, use `ExcludeAssets="runtime"`).
2. **0 documents / hundreds of errors** → target not restored: run `dotnet restore <target>` before scanning; check the workspace diagnostics; compare `inventory` error counts.
3. **Multi-targeted projects** load once per TFM (`Proj(net8.0)`, `Proj(net9.0)`) → de-duplicate by file path, analyse one TFM.
4. **Symbol equality** → always `SymbolEqualityComparer.Default`; for cross-project keys normalise with `FindSourceDefinitionAsync`.
5. **Expression-bodied members** → `GetOperation` on the method declaration still gives `IMethodBodyOperation` (`ExpressionBody`).
6. **Extension-method receivers** live in `Arguments[0]`, not `Instance`.
7. **Lambdas inside LINQ** → walk `IAnonymousFunctionOperation.Body`; captured variables are the outer symbols.
8. **Async** → `await x` is `IAwaitOperation(x)`; unwrap everywhere.
9. **Performance** → cache `SemanticModel` per tree and `MethodSummary` per (method, masks); skip generated files (`*.g.cs`, `obj/`); analyse only projects referencing ASP.NET Core or EF Core.
10. **Large repos** → add `--project <name>` filter; log per-phase timings (load / extract / slice / infer / report) — you need them for RQ3 anyway.
11. **Model memory** → load once; dispose properly; int4 CPU Phi-3-mini needs a few GB — close other heavy apps during measurement runs.
12. **Determinism** → greedy decoding, fixed prompt version, fixed model hash; still run 3 repetitions and report differences.
