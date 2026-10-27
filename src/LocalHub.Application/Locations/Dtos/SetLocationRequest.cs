using System.ComponentModel.DataAnnotations;

using LocalHub.Domain.Accounts;

namespace LocalHub.Application.Locations.Dtos;

// E16: either the address or the pair latitude/longitude, never both (checked by the LocationResolver).
public record SetLocationRequest(
    [StringLength(User.AddressMaxLength, ErrorMessage = "A morada é demasiado longa.")]
    string? Address,

    [Range(-90.0, 90.0, ErrorMessage = "A latitude tem de estar entre -90 e 90.")]
    decimal? Latitude,

    [Range(-180.0, 180.0, ErrorMessage = "A longitude tem de estar entre -180 e 180.")]
    decimal? Longitude);