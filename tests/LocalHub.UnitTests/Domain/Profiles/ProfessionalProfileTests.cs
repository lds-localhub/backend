using LocalHub.Domain.Accounts;
using LocalHub.Domain.Categories;
using LocalHub.Domain.Common;
using LocalHub.Domain.Profiles;

namespace LocalHub.UnitTests.Domain.Profiles;

[Trait("Category", "Unit")]
public class ProfessionalProfileTests
{
    private static readonly User Provider = User.Register("Canalizações Silva", "silva@localhub.test", UserRole.Provider);

    // ---------- Create (RF-F004-01). The fields are validated by the DTO (integration tests) ----------

    [Fact]
    [Trait("Requisito", "RF-F004-01")]
    public void Create_DadosValidos_CriaPerfilSemEspacosNasPontasESemClassificacao()
    {
        var profile = ProfessionalProfile.Create(
            Provider, "  Canalizador no Porto  ", " 220 000 000 ", " 15 anos ", [Category(3, "Canalização")], 15);

        Assert.Equal("Canalizador no Porto", profile.Introduction);
        Assert.Equal("220 000 000", profile.ContactDetails);
        Assert.Equal("15 anos", profile.Experience);
        Assert.Equal((short)15, profile.ServiceRadiusKm);
        Assert.Null(profile.AverageRating);
        Assert.Same(Provider, profile.ServiceProvider);
        Assert.Equal([3], profile.ServiceAreas.Select(a => a.ServiceCategoryId));
    }

    [Theory]
    [InlineData(UserRole.Client)]
    [InlineData(UserRole.Admin)]
    [Trait("Requisito", "RF-F004-01")]
    public void Create_UtilizadorQueNaoEPrestador_LancaErroDeAutorizacao(UserRole role)
    {
        var user = role == UserRole.Admin
            ? User.CreateAdministrator("Ana", "ana@localhub.test")
            : User.Register("Carla", "carla@localhub.test", role);

        var exception = Assert.Throws<DomainException>(() => CreateValid(user));

        Assert.Equal(DomainErrorKind.Forbidden, exception.Kind);
    }

    // Limits of E12: 1–1000, 1–500 and 1–2000 characters.
    // ---------- Categories (RF-F004-04) ----------

    [Fact]
    [Trait("Requisito", "RF-F004-04")]
    public void Create_TresCategoriasAtivas_AssociaAsTres()
    {
        var profile = ProfessionalProfile.Create(Provider, "Apresentação", "Telefone", "Experiência",
            [Category(1, "A"), Category(2, "B"), Category(3, "C")], 10);

        Assert.Equal([1, 2, 3], profile.ServiceAreas.Select(a => a.ServiceCategoryId).Order());
    }

    [Fact]
    [Trait("Requisito", "RF-F004-04")]
    public void Create_CategoriaRepetida_AssociaUmaVez()
    {
        var category = Category(4, "Jardinagem");

        var profile = ProfessionalProfile.Create(Provider, "Apresentação", "Telefone", "Experiência",
            [category, category], 10);

        Assert.Single(profile.ServiceAreas);
    }

    [Fact]
    [Trait("Requisito", "RF-F004-04")]
    [Trait("Requisito", "RF-F004-02")]
    public void ReplaceCategories_NovaLista_RemoveAsQueSaemEAcrescentaAsNovas()
    {
        var profile = ProfessionalProfile.Create(Provider, "Apresentação", "Telefone", "Experiência",
            [Category(1, "A"), Category(2, "B")], 10);

        profile.ReplaceCategories([Category(2, "B"), Category(3, "C")]);

        Assert.Equal([2, 3], profile.ServiceAreas.Select(a => a.ServiceCategoryId).Order());
    }

    // An associated category that was deactivated in the meantime can stay (API documentation, E14). Adding a new
    // inactive category is refused by the ProfileService (integration tests).
    [Fact]
    [Trait("Requisito", "RF-F004-04")]
    public void ReplaceCategories_CategoriaJaAssociadaEntretantoDesativada_Mantem()
    {
        var category = Category(1, "Canalização");
        var profile = ProfessionalProfile.Create(Provider, "Apresentação", "Telefone", "Experiência", [category], 10);
        category.Deactivate();

        profile.ReplaceCategories([category, Category(2, "Pintura")]);

        Assert.Equal([1, 2], profile.ServiceAreas.Select(a => a.ServiceCategoryId).Order());
    }

    // ---------- UpdateDetails (RF-F004-02) and SetServiceRadius (RF-F015-01) ----------

    [Fact]
    [Trait("Requisito", "RF-F004-02")]
    public void UpdateDetails_DadosValidos_SubstituiOsTextos()
    {
        var profile = CreateValid(Provider);

        profile.UpdateDetails("Nova apresentação", "Novo contacto", "Nova experiência");

        Assert.Equal("Nova apresentação", profile.Introduction);
        Assert.Equal("Novo contacto", profile.ContactDetails);
        Assert.Equal("Nova experiência", profile.Experience);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(25)]
    [InlineData(100)]
    [Trait("Requisito", "RF-F015-01")]
    public void SetServiceRadius_InteiroEntre1E100_SubstituiORaio(int km)
    {
        var profile = CreateValid(Provider);

        profile.SetServiceRadius((short)km);

        Assert.Equal((short)km, profile.ServiceRadiusKm);
    }

    // ---------- RecalculateAverageRating (RF-F012-02) ----------

    // Criteria 1 and 2: 5, 4 and 4 give 4.3; with a new review of 1, 3.5.
    [Theory]
    [Trait("Requisito", "RF-F012-02")]
    [InlineData(new short[] { 5, 4, 4 }, 4.3)]
    [InlineData(new short[] { 5, 4, 4, 1 }, 3.5)]
    [InlineData(new short[] { 3 }, 3.0)]
    [InlineData(new short[] { 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 4 }, 5.0)]
    public void RecalculateAverageRating_ComAvaliacoes_MediaComUmaCasaDecimal(short[] ratings, double expected)
    {
        var profile = CreateValid(Provider);

        profile.RecalculateAverageRating(ratings);

        Assert.Equal((decimal)expected, profile.AverageRating);
    }

    // Rule 1: 0.05 rounds up (4.05 → 4.1, not 4.0 as with the rounding to even of Math.Round).
    [Fact]
    [Trait("Requisito", "RF-F012-02")]
    public void RecalculateAverageRating_MediaTerminadaEm005_ArredondaParaCima()
    {
        var profile = CreateValid(Provider);
        // 20 ratings with a sum of 81: 81 / 20 = 4.05.
        short[] ratings = [.. Enumerable.Repeat((short)4, 19), 5];

        profile.RecalculateAverageRating(ratings);

        Assert.Equal(4.1m, profile.AverageRating);
    }

    // Criterion 3: without reviews there is no average ("Sem avaliações", not 0).
    [Fact]
    [Trait("Requisito", "RF-F012-02")]
    public void RecalculateAverageRating_SemAvaliacoes_FicaSemMedia()
    {
        var profile = CreateValid(Provider);

        profile.RecalculateAverageRating([]);

        Assert.Null(profile.AverageRating);
    }

    private static ProfessionalProfile CreateValid(User provider) =>
        ProfessionalProfile.Create(provider, "Apresentação", "Telefone", "Experiência", [Category(1, "A")], 10);

    // The id is normally generated by the database, so it is set by reflection.
    private static ServiceCategory Category(int id, string name)
    {
        var category = ServiceCategory.Create(name);
        typeof(ServiceCategory).GetProperty(nameof(ServiceCategory.ServiceCategoryId))!.SetValue(category, id);
        return category;
    }

    private static ServiceCategory Inactive(int id, string name)
    {
        var category = Category(id, name);
        category.Deactivate();
        return category;
    }
}