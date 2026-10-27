using LocalHub.Domain.Accounts;
using LocalHub.Domain.Categories;
using LocalHub.Domain.Locations;
using LocalHub.Domain.Profiles;
using LocalHub.Domain.Services;

namespace LocalHub.UnitTests.Domain.Services;

// E19 and E38: the published jobs that the providers can find and consult (RF-F006-01, RF-F011-01).
[Trait("Category", "Unit")]
public class PublishedJobTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 27);
    private static readonly GeoPoint Porto = GeoPoint.Create(41.1579m, -8.6291m);
    private static readonly ServiceCategory Plumbing = ServiceCategory.Create("Canalização");
    private static readonly Func<Service, bool> IsOpenPublishedJob = Service.IsOpenPublishedJob.Compile();

    [Fact]
    [Trait("Requisito", "RF-F006-01")]
    [Trait("Requisito", "RF-F011-01")]
    public void IsOpenPublishedJob_TrabalhoPublicadoAbertoDeClienteAtivo_DevolveTrue()
    {
        Assert.True(IsOpenPublishedJob(CreatePublic(NewClient())));
    }

    [Fact]
    [Trait("Requisito", "RF-F006-01")]
    public void IsOpenPublishedJob_ServicoComSolicitacoesDiretas_DevolveFalse()
    {
        var service = Service.Create(NewClient(), Plumbing, ProposalModel.DirectRequest, "Torneira com fuga de água.",
            Today, null, "Rua do Almada 10, Porto", Porto, [Profile()], Now);

        Assert.False(IsOpenPublishedJob(service));
    }

    [Theory]
    [InlineData(ServiceStatus.Assigned)]
    [InlineData(ServiceStatus.Completed)]
    [InlineData(ServiceStatus.Cancelled)]
    [Trait("Requisito", "RF-F006-01")]
    [Trait("Requisito", "RF-F011-01")]
    public void IsOpenPublishedJob_TrabalhoQueJaNaoEstaAberto_DevolveFalse(ServiceStatus status)
    {
        var service = CreatePublic(NewClient());
        // The transitions to these states are later issues, so the state is set by reflection.
        typeof(Service).GetProperty(nameof(Service.Status))!.SetValue(service, status);

        Assert.False(IsOpenPublishedJob(service));
    }

    // RF-F002-02: the jobs of a deactivated Client are not shown, but keep their state.
    [Fact]
    [Trait("Requisito", "RF-F006-01")]
    [Trait("Requisito", "RF-F002-02")]
    public void IsOpenPublishedJob_ClienteComAContaDesativada_DevolveFalse()
    {
        var client = NewClient();
        var service = CreatePublic(client);

        client.Deactivate(Now);

        Assert.False(IsOpenPublishedJob(service));
        Assert.Equal(ServiceStatus.Open, service.Status);
    }

    private static User NewClient() => User.Register("Carla Costa", "carla@localhub.test", UserRole.Client);

    private static Service CreatePublic(User client) =>
        Service.Create(client, Plumbing, ProposalModel.Public, "Substituir torneira.", Today, null,
            "Rua do Almada 10, Porto", Porto, [], Now);

    private static ProfessionalProfile Profile()
    {
        var provider = User.Register("Prestador", "prestador@localhub.test", UserRole.Provider);
        return ProfessionalProfile.Create(provider, "Apresentação", "Telefone", "Experiência", [Plumbing], 10m);
    }
}