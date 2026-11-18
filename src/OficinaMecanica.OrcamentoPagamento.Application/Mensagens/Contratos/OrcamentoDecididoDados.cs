namespace OficinaMecanica.OrcamentoPagamento.Application.Mensagens.Contratos;

public sealed record OrcamentoDecididoDados(
    Guid OrcamentoId,
    int Versao,
    string Decisao);
