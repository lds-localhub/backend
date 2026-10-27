using LocalHub.Domain.Accounts;
using LocalHub.Domain.Categories;
using LocalHub.Domain.Common;

namespace LocalHub.UnitTests.Domain.Categories;

[Trait("Category", "Unit")]
public class ServiceCategoryTests
{
    [Fact]
    [Trait("Requisito", "RF-F003-01")]
    public void Create_NomeValido_CriaCategoriaAtivaSemEspacosNasPontas()
    {
        var category = ServiceCategory.Create("  Canalização  ");

        Assert.Equal("Canalização", category.Name);
        Assert.Equal(ActivationStatus.Active, category.Status);
    }

    [Fact]
    [Trait("Requisito", "RF-F003-01")]
    public void Create_NomeCom60Caracteres_EAceite()
    {
        string name = new('a', ServiceCategory.NameMaxLength);

        var category = ServiceCategory.Create(name);

        Assert.Equal(name, category.Name);
    }

    [Fact]
    [Trait("Requisito", "RF-F003-02")]
    public void Rename_NomeValido_AlteraApenasONome()
    {
        var category = ServiceCategory.Create("Serralharia");

        category.Rename(" Serralharia e portões ");

        Assert.Equal("Serralharia e portões", category.Name);
        Assert.Equal(ActivationStatus.Active, category.Status);
    }

    [Fact]
    [Trait("Requisito", "RF-F003-03")]
    public void Deactivate_CategoriaAtiva_FicaDesativada()
    {
        var category = ServiceCategory.Create("Jardinagem");

        category.Deactivate();

        Assert.Equal(ActivationStatus.Inactive, category.Status);
    }

    [Fact]
    [Trait("Requisito", "RF-F003-03")]
    public void Deactivate_CategoriaJaDesativada_LancaConflito()
    {
        var category = ServiceCategory.Create("Jardinagem");
        category.Deactivate();

        var exception = Assert.Throws<DomainException>(category.Deactivate);

        Assert.Equal(DomainErrorKind.Conflict, exception.Kind);
    }
}