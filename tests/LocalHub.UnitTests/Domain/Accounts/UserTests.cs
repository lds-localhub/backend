using LocalHub.Domain.Accounts;
using LocalHub.Domain.Common;

namespace LocalHub.UnitTests.Domain.Accounts;

[Trait("Category", "Unit")]
public class UserTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    [Trait("Requisito", "RF-F002-02")]
    public void Deactivate_Administrator_ThrowsConflictAndKeepsAccountActive()
    {
        var user = TestUsers.CreateAdministrator("Rui Rocha", "rui.rocha@localhub.test");

        var exception = Assert.Throws<DomainException>(() => user.Deactivate(Now));

        Assert.Equal(DomainErrorKind.Conflict, exception.Kind);
        Assert.Equal(ActivationStatus.Active, user.Status);
    }
}