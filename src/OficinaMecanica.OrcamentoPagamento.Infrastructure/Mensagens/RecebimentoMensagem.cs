using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace OficinaMecanica.OrcamentoPagamento.Infrastructure.Mensagens;

public sealed class RecebimentoMensagem
{
    public Guid Id
    { get; init; }
    public string ChaveOperacao { get; init; } = string.Empty;
    public string Hash { get; init; } = string.Empty;
}
