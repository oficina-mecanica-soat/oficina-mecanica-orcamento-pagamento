namespace OficinaMecanica.OrcamentoPagamento.Application.Mensagens.Contratos;

public sealed record OrcamentoGeradoDados(
    Guid OrcamentoId,
    int Versao,
    decimal ValorTotal);
