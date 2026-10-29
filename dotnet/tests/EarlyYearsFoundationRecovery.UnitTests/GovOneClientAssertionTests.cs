using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using EarlyYearsFoundationRecovery.Web.Authentication;
using Microsoft.IdentityModel.Tokens;

namespace EarlyYearsFoundationRecovery.UnitTests;

public sealed class GovOneClientAssertionTests
{
    [Fact]
    public void CreateReturnsSignedClientAssertionForTokenEndpoint()
    {
        using var rsa = RSA.Create(2048);
        var keyPath = Path.Combine(Path.GetTempPath(), $"eyfr-key-{Guid.NewGuid():N}.pem");
        File.WriteAllText(keyPath, rsa.ExportPkcs8PrivateKeyPem());

        try
        {
            var token = GovOneClientAssertion.Create(
                "test-client",
                "https://identity.example.test/token",
                keyPath);
            var handler = new JwtSecurityTokenHandler();
            var validation = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = "test-client",
                ValidateAudience = true,
                ValidAudience = "https://identity.example.test/token",
                ValidateLifetime = true,
                IssuerSigningKey = new RsaSecurityKey(rsa),
                ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                ClockSkew = TimeSpan.FromSeconds(1)
            };

            handler.ValidateToken(token, validation, out var validatedToken);

            var jwt = Assert.IsType<JwtSecurityToken>(validatedToken);
            Assert.Equal("test-client", jwt.Subject);
            Assert.Equal("https://identity.example.test/token", jwt.Audiences.Single());
            Assert.False(string.IsNullOrWhiteSpace(jwt.Id));
            Assert.Contains(jwt.Claims, claim => claim.Type == "iat");
            Assert.Equal(SecurityAlgorithms.RsaSha256, jwt.Header.Alg);
        }
        finally
        {
            File.Delete(keyPath);
        }
    }
}
