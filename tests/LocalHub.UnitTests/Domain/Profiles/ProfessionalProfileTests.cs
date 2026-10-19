using LocalHub.Domain.Accounts;
using LocalHub.Domain.Categories;
using LocalHub.Domain.Common;
using LocalHub.Domain.Profiles;

namespace LocalHub.UnitTests.Domain.Profiles;

[Trait("Category", "Unit")]
public class ProfessionalProfileTests
{
    private static readonly User Provider = User.Register("Canalizações Silva", "silva@localhub.test", UserRole.Provider);

    // ---------- Create (RF-F004-01) ----------

    [Fact]
    [Trait("Requisito", "RF-F004-01")]
    public void Create_DadosValidos_CriaPerfilSemEspacosNasPontasESemClassificacao()
    {
        var profile = ProfessionalProfile.Create(
            Provider, "  Canalizador no Porto  ", " 220 000 000 ", " 15 anos ", [Category(3, "Canalização")], 15m);

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

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("Requisito", "RF-F004-01")]
    public void Create_SemApresentacao_LancaErroNoCampo(string? introduction)
    {
        var exception = Assert.Throws<DomainException>(() => ProfessionalProfile.Create(
            Provider, introduction, "Telefone", "Experiência", [Category(1, "A")], 10m));

        AssertValidation(exception, "introduction");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [Trait("Requisito", "RF-F004-01")]
    public void Create_SemContacto_LancaErroNoCampo(string? contactDetails)
    {
        var exception = Assert.Throws<DomainException>(() => ProfessionalProfile.Create(
            Provider, "Apresentação", contactDetails, "Experiência", [Category(1, "A")], 10m));

        AssertValidation(exception, "contactDetails");
    }

    [Fact]
    [Trait("Requisito", "RF-F004-01")]
    public void Create_SemCategorias_LancaErroNasCategorias()
    {
        var exception = Assert.Throws<DomainException>(() => ProfessionalProfile.Create(
            Provider, "Apresentação", "Telefone", "Experiência", [], 10m));

        AssertValidation(exception, "serviceCategoryIds");
    }

    // Limits of E12: 1–1000, 1–500 and 1–2000 characters.
    [Theory]
    [InlineData(ProfessionalProfile.IntroductionMaxLength, true)]
    [InlineData(ProfessionalProfile.IntroductionMaxLength + 1, false)]
    [Trait("Requisito", "RF-F004-01")]
    public void ValidateIntroduction_ValorLimite_AceitaAte1000Caracteres(int length, bool valid)
    {
        AssertLimit(() => ProfessionalProfile.ValidateIntroduction(new string('a', length)), valid, "introduction");
    }

    [Theory]
    [InlineData(ProfessionalProfile.ContactDetailsMaxLength, true)]
    [InlineData(ProfessionalProfile.ContactDetailsMaxLength + 1, false)]
    [Trait("Requisito", "RF-F004-01")]
    public void ValidateContactDetails_ValorLimite_AceitaAte500Caracteres(int length, bool valid)
    {
        AssertLimit(() => ProfessionalProfile.ValidateContactDetails(new string('a', length)), valid, "contactDetails");
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(ProfessionalProfile.ExperienceMaxLength, true)]
    [InlineData(ProfessionalProfile.ExperienceMaxLength + 1, false)]
    [Trait("Requisito", "RF-F004-01")]
    public void ValidateExperience_ValorLimite_AceitaDe1A2000Caracteres(int length, bool valid)
    {
        AssertLimit(() => ProfessionalProfile.ValidateExperience(new string('a', length)), valid, "experience");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    [InlineData(25.5)]
    [Trait("Requisito", "RF-F015-01")]
    public void Create_RaioInvalido_LancaErroNoRaio(double km)
    {
        var exception = Assert.Throws<DomainException>(() => ProfessionalProfile.Create(
            Provider, "Apresentação", "Telefone", "Experiência", [Category(1, "A")], (decimal)km));

        AssertValidation(exception, "serviceRadiusKm");
    }

    // ---------- Categories (RF-F004-04) ----------

    [Fact]
    [Trait("Requisito", "RF-F004-04")]
    public void Create_TresCategoriasAtivas_AssociaAsTres()
    {
        var profile = ProfessionalProfile.Create(Provider, "Apresentação", "Telefone", "Experiência",
            [Category(1, "A"), Category(2, "B"), Category(3, "C")], 10m);

        Assert.Equal([1, 2, 3], profile.ServiceAreas.Select(a => a.ServiceCategoryId).Order());
    }

    [Fact]
    [Trait("Requisito", "RF-F004-04")]
    public void Create_CategoriaRepetida_AssociaUmaVez()
    {
        var category = Category(4, "Jardinagem");

        var profile = ProfessionalProfile.Create(Provider, "Apresentação", "Telefone", "Experiência",
            [category, category], 10m);

        Assert.Single(profile.ServiceAreas);
    }

    [Fact]
    [Trait("Requisito", "RF-F004-04")]
    public void Create_CategoriaDesativada_LancaErroNasCategorias()
    {
        var exception = Assert.Throws<DomainException>(() => ProfessionalProfile.Create(
            Provider, "Apresentação", "Telefone", "Experiência", [Category(1, "A"), Inactive(2, "Antiga")], 10m));

        AssertValidation(exception, "serviceCategoryIds");
        Assert.Contains("Antiga", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requisito", "RF-F004-04")]
    [Trait("Requisito", "RF-F004-02")]
    public void ReplaceCategories_NovaLista_RemoveAsQueSaemEAcrescentaAsNovas()
    {
        var profile = ProfessionalProfile.Create(Provider, "Apresentação", "Telefone", "Experiência",
            [Category(1, "A"), Category(2, "B")], 10m);

        profile.ReplaceCategories([Category(2, "B"), Category(3, "C")]);

        Assert.Equal([2, 3], profile.ServiceAreas.Select(a => a.ServiceCategoryId).Order());
    }

    [Fact]
    [Trait("Requisito", "RF-F004-04")]
    public void ReplaceCategories_ListaVazia_LancaErroEMantemAsCategorias()
    {
        var profile = CreateValid(Provider);

        var exception = Assert.Throws<DomainException>(() => profile.ReplaceCategories([]));

        AssertValidation(exception, "serviceCategoryIds");
        Assert.Single(profile.ServiceAreas);
    }

    [Fact]
    [Trait("Requisito", "RF-F004-04")]
    public void ReplaceCategories_CategoriaNovaDesativada_LancaErroEMantemAsCategorias()
    {
        var profile = CreateValid(Provider);

        var exception = Assert.Throws<DomainException>(() => profile.ReplaceCategories([Inactive(9, "Antiga")]));

        AssertValidation(exception, "serviceCategoryIds");
        Assert.Equal([1], profile.ServiceAreas.Select(a => a.ServiceCategoryId));
    }

    // An associated category that was deactivated in the meantime can stay (API documentation, E14).
    [Fact]
    [Trait("Requisito", "RF-F004-04")]
    public void ReplaceCategories_CategoriaJaAssociadaEntretantoDesativada_Mantem()
    {
        var category = Category(1, "Canalização");
        var profile = ProfessionalProfile.Create(Provider, "Apresentação", "Telefone", "Experiência", [category], 10m);
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

    [Fact]
    [Trait("Requisito", "RF-F004-02")]
    public void UpdateDetails_UmCampoInvalido_NaoAlteraNenhumCampo()
    {
        var profile = CreateValid(Provider);

        Assert.Throws<DomainException>(() => profile.UpdateDetails("Nova apresentação", "Novo contacto", ""));

        Assert.Equal("Apresentação", profile.Introduction);
        Assert.Equal("Telefone", profile.ContactDetails);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(25)]
    [InlineData(100)]
    [Trait("Requisito", "RF-F015-01")]
    public void SetServiceRadius_InteiroEntre1E100_SubstituiORaio(int km)
    {
        var profile = CreateValid(Provider);

        profile.SetServiceRadius(km);

        Assert.Equal((short)km, profile.ServiceRadiusKm);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(150)]
    [Trait("Requisito", "RF-F015-01")]
    public void SetServiceRadius_ForaDoIntervalo_LancaErroEMantemORaio(int km)
    {
        var profile = CreateValid(Provider);

        var exception = Assert.Throws<DomainException>(() => profile.SetServiceRadius(km));

        AssertValidation(exception, "serviceRadiusKm");
        Assert.Equal((short)10, profile.ServiceRadiusKm);
    }

    private static ProfessionalProfile CreateValid(User provider) =>
        ProfessionalProfile.Create(provider, "Apresentação", "Telefone", "Experiência", [Category(1, "A")], 10m);

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

    private static void AssertValidation(DomainException exception, string field)
    {
        Assert.Equal(DomainErrorKind.Validation, exception.Kind);
        Assert.Equal(field, exception.Field);
    }

    private static void AssertLimit(Func<string> validate, bool valid, string field)
    {
        if (valid)
        {
            validate();
            return;
        }

        AssertValidation(Assert.Throws<DomainException>(validate), field);
    }
}