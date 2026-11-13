namespace OficinaMecanica.OrcamentoPagamento.Application.Mensagens;

public interface IRecebimentoMensagemRepository
{
    Task ExecutarEmTransacaoAsync(Func<CancellationToken, Task> operacao, CancellationToken cancellationToken);
    Task<bool> VerificarRepeticaoAsync(Guid mensagemId, string chave, string hash, CancellationToken cancellationToken);
    Task RegistrarAsync(Guid mensagemId, string chave, string hash, CancellationToken cancellationToken);
}
