using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace OficinaMecanica.OrcamentoPagamento.API.IntegrationTests.Fixtures;

internal static class TokenTestDataFactory
{
    public static string Criar(string papel, Guid? clienteId = null, string? issuer = null,
        string? audience = null, DateTime? expires = null, string? chave = null)
    {
        var claims = new List<Claim> { new("sub", Guid.NewGuid().ToString()), new("role", papel), new("jti", Guid.NewGuid().ToString()) };
        if (clienteId.HasValue)
            claims.Add(new Claim("cliente_id", clienteId.Value.ToString()));
        var token = new JwtSecurityToken(issuer ?? "oficina-mecanica-auth", audience ?? "oficina-mecanica-api",
            claims, expires: expires ?? DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(chave ?? OrcamentoPagamentoApiFixture.ChaveTeste)), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
