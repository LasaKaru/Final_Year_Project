using System.CommandLine;

namespace BolaLens.Cli.Commands;

/// <summary>`endpoints`, `policy`, `schema`, `di` and `paths` — inspect each intermediate Stage-1 artefact as JSON (FR19).</summary>
public static class ContextCommands
{
    public static Command Endpoints() => Make("endpoints", "Endpoint inventory (verb, route, parameters, binding, [Authorize]) as JSON", r => r.Endpoints);

    public static Command Policy() => Make("policy", "Global authentication/authorization policy map as JSON", r => r.Policy);

    public static Command Schema() => Make("schema", "EF Core ownership schema (entities, keys, owner candidates, query filters) as JSON", r => r.Schema);

    public static Command Di() => Make("di", "Dependency-injection registration map (service → implementations) as JSON", r => r.DiMap);

    public static Command Paths() => Make("paths", "Annotated execution paths (id → service → EF sink, guards, spans) with symbolic verdicts as JSON",
        r => r.Paths.Select(p => new { path = p.Path, symbolic = p.Verdict }).ToList(), withEndpointFilter: true);

    private static Command Make(string name, string description, Func<Analysis.Pipeline.SymbolicResult, object> select, bool withEndpointFilter = false)
    {
        var path = Common.PathArgument();
        var output = Common.OutOption();
        var config = Common.ConfigOption();
        var verbose = Common.VerboseOption();
        var endpoint = Common.EndpointOption();
        var cmd = new Command(name, description) { path, output, config, verbose };
        if (withEndpointFilter) cmd.Options.Add(endpoint);
        cmd.SetAction(async (r, ct) =>
        {
            var result = await Common.AnalyseAsync(r.GetValue(path)!, Common.LoadConfig(r.GetValue(config)),
                withEndpointFilter ? r.GetValue(endpoint) : null, r.GetValue(verbose), ct);
            if (result is null) return Common.LoadError;
            await Common.WriteJsonAsync(select(result), r.GetValue(output), ct);
            return Common.Ok;
        });
        return cmd;
    }
}
