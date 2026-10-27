using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using LocalHub.Application.Common;
using LocalHub.Domain.Services;

namespace LocalHub.Application.Services.Dtos;

// Body of E25. Fields that are not listed here are rejected with 400.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record SubmitProposalRequest(
    [Required(ErrorMessage = "Indique o valor da proposta.")]
    [Range(Proposal.AmountMin, Proposal.AmountMax,
        ErrorMessage = "O valor tem de ser maior que 0 e até 100 000,00 €.")]
    [MaxDecimalPlaces(2, ErrorMessage = "O valor tem no máximo duas casas decimais.")]
    decimal? Amount,

    [Required(ErrorMessage = "Indique as condições da proposta.")]
    [StringLength(Proposal.ConditionsMaxLength, ErrorMessage = "As condições são demasiado longas.")]
    string? Conditions,

    [StringLength(Proposal.ObsMaxLength, ErrorMessage = "As observações são demasiado longas.")]
    string? Obs);