namespace OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.Messages;

public static class PagamentoApplicationErrorMessages
{
    public const string PagamentoNaoEncontrado = "Pagamento não encontrado.";
    public const string ConciliacaoManualNecessaria = "O estado informado pelo provedor exige conciliação manual.";
    public const string ProvedorIndisponivel = "Não foi possível conferir o pagamento no provedor. A notificação pode ser reenviada.";
    public const string NotificacaoDivergente = "O pagamento informado pelo provedor diverge da cobrança registrada.";
}
