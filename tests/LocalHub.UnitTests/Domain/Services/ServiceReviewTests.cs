using LocalHub.Domain.Accounts;
using LocalHub.Domain.Categories;
using LocalHub.Domain.Common;
using LocalHub.Domain.Profiles;
using LocalHub.Domain.Services;

namespace LocalHub.UnitTests.Domain.Services;

// E32: the review of a completed service (RF-F012-01).
[Trait("Category", "Unit")]
public class ServiceReviewTests
{
    private const int ClientId = 45;
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = Now.AddHours(2);
    private static readonly DateTimeOffset End = Now.AddDays(3);
    private static readonly DateTimeOffset ReviewDate = Now.AddDays(4);
    private static readonly DateOnly Today = new(2026, 10, 19);
    private static readonly ServiceCategory Plumbing = Category(3, "Canalização");

    // Criterion 1: the client reviews a completed service; the service stays COMPLETED.
    [Fact]
    [Trait("Requisito", "RF-F012-01")]
    public void AddReview_ServicoRealizado_RegistaAAvaliacao()
    {
        var service = CompletedService();

        var review = service.AddReview(ClientId, 4, "  Rápida e cuidadosa.  ", ReviewDate);

        Assert.Same(review, service.Review);
        Assert.Same(service, review.Service);
        Assert.Equal((short)4, review.Rating);
        Assert.Equal("Rápida e cuidadosa.", review.Comment);
        Assert.Equal(ReviewDate, review.CreationDate);
        Assert.Equal(ServiceStatus.Completed, service.Status);
    }

    // Criterion 7 and API documentation (E32): without a comment, or with a blank one, the comment is null.
    [Theory]
    [Trait("Requisito", "RF-F012-01")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddReview_SemComentario_GuardaComentarioNulo(string? comment)
    {
        var service = CompletedService();

        var review = service.AddReview(ClientId, 5, comment, ReviewDate);

        Assert.Null(review.Comment);
    }

    // Rule 1: only the client that created the service (another client or the provider get 403).
    [Fact]
    [Trait("Requisito", "RF-F012-01")]
    public void AddReview_OutroUtilizador_LancaForbiddenSemAvaliar()
    {
        var service = CompletedService();

        var exception = Assert.Throws<DomainException>(() => service.AddReview(99, 4, null, ReviewDate));

        Assert.Equal(DomainErrorKind.Forbidden, exception.Kind);
        Assert.Null(service.Review);
    }

    // Criterion 2: a service that is not COMPLETED cannot be reviewed.
    [Theory]
    [Trait("Requisito", "RF-F012-01")]
    [InlineData(ServiceStatus.Open)]
    [InlineData(ServiceStatus.Assigned)]
    [InlineData(ServiceStatus.Cancelled)]
    public void AddReview_ServicoNaoRealizado_LancaInvalidTransition(ServiceStatus state)
    {
        var service = ServiceIn(state);

        var exception = Assert.Throws<DomainException>(() => service.AddReview(ClientId, 4, null, ReviewDate));

        Assert.Equal(DomainErrorKind.InvalidTransition, exception.Kind);
        Assert.Null(service.Review);
    }

    // Criterion 3: a second review is refused with avaliacao-existente and the first one does not change.
    [Fact]
    [Trait("Requisito", "RF-F012-01")]
    public void AddReview_SegundaAvaliacao_LancaConflictAvaliacaoExistente()
    {
        var service = CompletedService();
        var first = service.AddReview(ClientId, 4, "Bom trabalho.", ReviewDate);

        var exception = Assert.Throws<DomainException>(() => service.AddReview(ClientId, 1, null, ReviewDate));

        Assert.Equal(DomainErrorKind.Conflict, exception.Kind);
        Assert.Equal(Review.AlreadyExistsErrorType, exception.ErrorType);
        Assert.Same(first, service.Review);
        Assert.Equal((short)4, service.Review!.Rating);
    }

    // ---------- Helpers ----------

    private static Service CompletedService() => ServiceIn(ServiceStatus.Completed);

    // A published job with the proposal of the profile 12 accepted, moved to the given state.
    private static Service ServiceIn(ServiceStatus state)
    {
        var service = Service.Create(NewClient(), Plumbing, ProposalModel.Public, "Reparar torneira",
            "Substituir torneira.", Today, null, "Rua do Almada 10, Porto", 41.1579m, -8.6291m, [], Now);
        if (state == ServiceStatus.Open)
        {
            return service;
        }

        var proposal = service.SubmitProposal(Profile(12), 80m, "Material incluído.", null, Later);
        typeof(Proposal).GetProperty(nameof(Proposal.ProposalId))!.SetValue(proposal, 101);
        service.AcceptProposal(101, ClientId, Later);
        if (state == ServiceStatus.Completed)
        {
            service.Complete(ClientId, End);
        }
        else if (state == ServiceStatus.Cancelled)
        {
            service.CancelByClient(ClientId, End);
        }

        return service;
    }

    private static User NewClient()
    {
        var client = User.Register("Carla Costa", "carla@localhub.test", UserRole.Client);
        typeof(User).GetProperty(nameof(User.UserId))!.SetValue(client, ClientId);
        return client;
    }

    // The ids are normally generated by the database, so they are set by reflection.
    private static ServiceCategory Category(int id, string name)
    {
        var category = ServiceCategory.Create(name);
        typeof(ServiceCategory).GetProperty(nameof(ServiceCategory.ServiceCategoryId))!.SetValue(category, id);
        return category;
    }

    private static ProfessionalProfile Profile(int id)
    {
        var provider = User.Register("Prestador", $"prestador{id}@localhub.test", UserRole.Provider);
        var profile = ProfessionalProfile.Create(provider, "Apresentação", "Telefone", "Experiência", [Plumbing], 10);
        typeof(ProfessionalProfile).GetProperty(nameof(ProfessionalProfile.ProfessionalProfileId))!
            .SetValue(profile, id);
        return profile;
    }
}