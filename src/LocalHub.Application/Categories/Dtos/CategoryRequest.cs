using System.ComponentModel.DataAnnotations;

using LocalHub.Domain.Categories;

namespace LocalHub.Application.Categories.Dtos;

public record CategoryRequest(
    [Required(ErrorMessage = "Indique o nome da categoria.")]
    [StringLength(ServiceCategory.NameMaxLength, ErrorMessage = "O nome é demasiado longo.")]
    string? Name);