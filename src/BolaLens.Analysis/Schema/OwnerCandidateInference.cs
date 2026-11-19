using System.Text.RegularExpressions;
using BolaLens.Analysis.Infrastructure;
using BolaLens.Core.Config;
using BolaLens.Core.Model;
using Microsoft.CodeAnalysis;

namespace BolaLens.Analysis.Schema;

/// <summary>Classifies entity properties as owner candidates by configured name patterns and by
/// navigation/foreign key to a user type. Input: entity type symbol. Limitation: a candidate is only
/// a likely ownership relation — e.g. an "OwnerId" used as business metadata is still a candidate.</summary>
public sealed class OwnerCandidateInference
{
    private readonly Regex[] _user;
    private readonly Regex[] _tenant;
    private readonly HashSet<string> _userTypes;

    public OwnerCandidateInference(OwnershipOptions options)
    {
        _user = options.UserOwnerPatterns.Select(p => new Regex(p, RegexOptions.CultureInvariant)).ToArray();
        _tenant = options.TenantOwnerPatterns.Select(p => new Regex(p, RegexOptions.CultureInvariant)).ToArray();
        _userTypes = new HashSet<string>(options.UserTypeNames, StringComparer.Ordinal);
    }

    public OwnerKind? KindOfName(string property) =>
        _user.Any(r => r.IsMatch(property)) ? OwnerKind.User
        : _tenant.Any(r => r.IsMatch(property)) ? OwnerKind.Tenant
        : null;

    public Dictionary<string, (OwnerKind Kind, List<OwnerEvidence> Evidence)> Infer(ITypeSymbol entity)
    {
        var result = new Dictionary<string, (OwnerKind, List<OwnerEvidence>)>(StringComparer.Ordinal);
        var props = Symbols.AllProperties(entity).ToList();
        foreach (var p in props)
        {
            if (KindOfName(p.Name) is { } kind)
                result[p.Name] = (kind, [OwnerEvidence.NamePattern]);
        }
        foreach (var nav in props.Where(p => IsUserType(p.Type)))
        {
            var fk = nav.Name + "Id";
            var target = props.Any(p => p.Name == fk) ? fk : nav.Name;
            if (result.TryGetValue(target, out var existing))
                existing.Item2.Add(OwnerEvidence.ForeignKeyToUserType);
            else
                result[target] = (OwnerKind.User, [OwnerEvidence.ForeignKeyToUserType]);
        }
        return result;
    }

    private bool IsUserType(ITypeSymbol type)
    {
        for (var t = type; t is not null; t = t.BaseType)
            if (_userTypes.Contains(t.Name)) return true;
        return false;
    }
}
