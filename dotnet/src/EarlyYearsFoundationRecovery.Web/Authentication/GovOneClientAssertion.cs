using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace EarlyYearsFoundationRecovery.Web.Authentication;

public static class GovOneClientAssertion
{
    public static string Create(string clientId, string tokenEndpoint, string privateKeyPath)
    {
        if (string.IsNullOrWhiteSpace(clientId) ||
            string.IsNullOrWhiteSpace(tokenEndpoint) ||
            string.IsNullOrWhiteSpace(privateKeyPath))
        {
            throw new ArgumentException("Gov.UK One Login client assertion settings are incomplete.");
        }

        var pem = File.ReadAllText(privateKeyPath);
        using var rsa = RSA.Create();
        rsa.ImportFromPem(pem);

        var now = DateTime.UtcNow;
        var credentials = new SigningCredentials(
            new RsaSecurityKey(rsa),
            SecurityAlgorithms.RsaSha256);
        var token = new JwtSecurityToken(
            issuer: clientId,
            audience: tokenEndpoint,
            claims:
            [
                new System.Security.Claims.Claim("sub", clientId),
                new System.Security.Claims.Claim("jti", Guid.NewGuid().ToString("D")),
                new System.Security.Claims.Claim(
                    "iat",
                    new DateTimeOffset(now).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture),
                    System.Security.Claims.ClaimValueTypes.Integer64)
            ],
            notBefore: now,
            expires: now.AddMinutes(5),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
