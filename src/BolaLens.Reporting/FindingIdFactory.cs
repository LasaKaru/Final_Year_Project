using System.Security.Cryptography;
using System.Text;
using BolaLens.Core.Model;

namespace BolaLens.Reporting;

/// <summary>Stable finding IDs: first 12 hex of SHA-256 over method | route | sink symbol | sink span
/// (falls back to the taint source for paths without sinks). Stable across runs of the same code.</summary>
public static class FindingIdFactory
{
    public static string Create(ExecutionPath path)
    {
        var sink = path.Sinks.FirstOrDefault(s => s.IsStateChanging);
        var sinkPart = sink is null ? $"source:{path.TaintSource.Name}" : $"{sink.Symbol}|{sink.Location.FilePath}:{sink.Location.StartLine}";
        var input = $"{path.Endpoint.ActionSymbol}|{path.Endpoint.Id}|{path.TaintSource.Name}|{sinkPart}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)))[..12].ToLowerInvariant();
    }
}
