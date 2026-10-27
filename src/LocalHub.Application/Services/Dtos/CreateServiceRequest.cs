using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using LocalHub.Domain.Services;

namespace LocalHub.Application.Services.Dtos;

// Body of E20, with the two variants of the API documentation: DIRECT_REQUEST requires professionalProfileIds and
// PUBLIC does not accept it. Without location, the client's registered location is used. Fields that are not listed
// here are rejected with 400 (unevaluatedProperties: false in the schema of E20). The rules that compare fields or
// need the database are checked by the ServiceWorkflowService (rule R17).
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record CreateServiceRequest(
    // EnumDataType: the JSON reader also accepts numbers, such as 7, that are not a modality.
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

    [StringLength(Service.LocationMaxLength, ErrorMessage = "A morada é demasiado longa.")]
    string? Location,

    IReadOnlyList<int>? ProfessionalProfileIds);