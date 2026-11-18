namespace OficinaMecanica.OrcamentoPagamento.Application.Mensagens.Contratos;

public sealed record OrcamentoCanceladoDados(
    Guid? OrcamentoId,
    int Versao);
