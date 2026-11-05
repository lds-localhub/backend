using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using LocalHub.Domain.Services;

namespace LocalHub.Application.Services.Dtos;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record CreateServiceRequest(
    // EnumDataType: the JSON reader also accepts numbers that are not a modality.
    [Required(ErrorMessage = "Indique a modalidade: DIRECT_REQUEST ou PUBLIC.")]
    [EnumDataType(typeof(ProposalModel), ErrorMessage = "Indique a modalidade: DIRECT_REQUEST ou PUBLIC.")]
    ProposalModel? ProposalModel,

    [Required(ErrorMessage = "Indique a categoria do serviço.")]
    int? ServiceCategoryId,

    [Required(ErrorMessage = "Indique o título do serviço.")]
    [StringLength(Service.TitleMaxLength, ErrorMessage = "O título é demasiado longo.")]
    string? Title,

    [Required(ErrorMessage = "Indique a descrição do serviço.")]
    [StringLength(Service.DescriptionMaxLength, ErrorMessage = "A descrição é demasiado longa.")]
    string? Description,

    [Required(ErrorMessage = "Indique a data pretendida para o serviço.")]
    DateOnly? RequestedStartDate,

    DateOnly? RequestedEndDate,

    [Required(ErrorMessage = "Indique o local de realização.")]
    [StringLength(Service.LocationMaxLength, ErrorMessage = "A morada é demasiado longa.")]
    string? Location,

    IReadOnlyList<int>? ProfessionalProfileIds);