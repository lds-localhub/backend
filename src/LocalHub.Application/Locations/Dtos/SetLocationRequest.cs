using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using LocalHub.Domain.Accounts;

namespace LocalHub.Application.Locations.Dtos;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record SetLocationRequest(
    [Required(ErrorMessage = "Indique a morada.")]
    [StringLength(User.AddressMaxLength, ErrorMessage = "A morada é demasiado longa.")]
    string? Address);