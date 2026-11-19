using BolaLens.Analysis.Taint;
using BolaLens.Core.Model;

namespace BolaLens.Analysis.Tests;

public class CallerIdentityTests
{
    [Theory]
    [InlineData("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier", OwnerKind.User)] // "org" in the host!
    [InlineData("sub", OwnerKind.User)]
    [InlineData("tenant_id", OwnerKind.Tenant)]
    [InlineData("tid", OwnerKind.Tenant)]
    [InlineData("org_id", OwnerKind.Tenant)]
    [InlineData("http://schemas.microsoft.com/identity/claims/tenantid", OwnerKind.Tenant)]
    [InlineData(null, OwnerKind.User)]
    public void Claim_type_to_identity_level(string? claim, OwnerKind expected) =>
        CallerIdentityRecognizer.ClaimKind(claim).ShouldBe(expected);
}
