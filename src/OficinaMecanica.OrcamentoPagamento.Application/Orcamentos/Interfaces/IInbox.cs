using OficinaMecanica.OrcamentoPagamento.Application.Mensagens;

namespace OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.Interfaces;

public interface IInbox
{
    Task ExecutarEmTransacaoAsync(Func<CancellationToken, Task> operacao, CancellationToken cancellationToken);
    Task<bool> VerificarRepeticaoAsync(Guid mensagemId, string chave, string hash, CancellationToken cancellationToken);
    Task RegistrarAsync(Guid mensagemId, string chave, string hash, CancellationToken cancellationToken);
}
