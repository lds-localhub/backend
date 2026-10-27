using System.Globalization;
using System.Text;

using LocalHub.Domain.Accounts;

using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace LocalHub.Infrastructure.Security;

public class JwtTokenIssuer : ITokenIssuer
{
    private readonly JsonWebTokenHandler _handler = new();
    private readonly JwtOptions _options;

    public JwtTokenIssuer(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }

    public string Issue(User user, UserSession session)
    {
        SymmetricSecurityKey key = new(Encoding.UTF8.GetBytes(_options.SigningKey));

        SecurityTokenDescriptor descriptor = new()
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = session.CreatedAt.UtcDateTime,
            NotBefore = session.CreatedAt.UtcDateTime,
            Expires = session.ExpiresAt.UtcDateTime,
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = user.UserId.ToString(CultureInfo.InvariantCulture),
                ["role"] = user.Role.ToString().ToUpperInvariant(),
                ["sid"] = session.SessionId.ToString(CultureInfo.InvariantCulture)
            },
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        };

        return _handler.CreateToken(descriptor);
    }
}