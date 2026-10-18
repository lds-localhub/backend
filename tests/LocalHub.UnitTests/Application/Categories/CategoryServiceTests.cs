using LocalHub.Application.Categories;
using LocalHub.Infrastructure.Categories;

namespace LocalHub.UnitTests.Application.Categories;

// Reason shown by the web application when a category cannot be deactivated (RF-F003-03, rule 2; API documentation, E11).
[Trait("Category", "Unit")]
public class CategoryServiceTests
{
    [Theory]
    [InlineData(3, 1, "A categoria está associada a 3 Prestadores ativos e a 1 serviço em curso.")]
    [InlineData(1, 0, "A categoria está associada a 1 Prestador ativo.")]
    [InlineData(0, 2, "A categoria está associada a 2 serviços em curso.")]
    [InlineData(1, 1, "A categoria está associada a 1 Prestador ativo e a 1 serviço em curso.")]
    [Trait("Requisito", "RF-F003-03")]
    public void DescribeUsage_CategoriaEmUtilizacao_IndicaOMotivo(int providers, int services, string expected)
    {
        var usage = new CategoryUsageCount(providers, services);

        Assert.True(usage.IsInUse);
        Assert.Equal(expected, CategoryService.DescribeUsage(usage));
    }

    [Fact]
    [Trait("Requisito", "RF-F003-03")]
    public void CategoryUsageCount_SemPrestadoresNemServicos_NaoEstaEmUtilizacao()
    {
        Assert.False(new CategoryUsageCount(0, 0).IsInUse);
    }
}