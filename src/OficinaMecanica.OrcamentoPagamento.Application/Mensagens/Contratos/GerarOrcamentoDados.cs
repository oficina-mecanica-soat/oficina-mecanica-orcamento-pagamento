namespace OficinaMecanica.OrcamentoPagamento.Application.Mensagens.Contratos;

public sealed record GerarOrcamentoDados(
    int Versao,
    Guid ClienteId,
    ServicoOrcamentoDados[] Servicos,
    ItemOrcamentoDados[] Itens);
