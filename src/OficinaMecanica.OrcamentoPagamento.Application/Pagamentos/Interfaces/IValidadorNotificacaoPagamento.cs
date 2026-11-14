namespace OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.Interfaces;

public interface IValidadorNotificacaoPagamento
{
    bool Validar(string? assinatura, string? requisicaoId, string? pagamentoId);
}
