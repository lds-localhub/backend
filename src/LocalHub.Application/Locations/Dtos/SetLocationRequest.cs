using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using LocalHub.Domain.Accounts;

namespace LocalHub.Application.Locations.Dtos;

// Body of E16: only the address, converted into coordinates by the backend. The coordinates of the device are only
// received in E35, so a body with coordinates (or any other field) is rejected with 400 (rule R3; RF-F015-06).
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record SetLocationRequest(
    [Required(ErrorMessage = "Indique a morada.")]
    [StringLength(User.AddressMaxLength, ErrorMessage = "A morada é demasiado longa.")]
    string? Address);