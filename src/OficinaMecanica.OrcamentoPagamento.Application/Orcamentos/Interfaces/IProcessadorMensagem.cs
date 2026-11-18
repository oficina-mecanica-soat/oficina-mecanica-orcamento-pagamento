namespace OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.Interfaces;

public interface IProcessadorMensagem
{
    Task ProcessarAsync(string corpo, CancellationToken cancellationToken = default);
}
