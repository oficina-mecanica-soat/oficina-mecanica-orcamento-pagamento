namespace OficinaMecanica.OrcamentoPagamento.Application.Mensagens.Contratos;

public sealed record ServicoOrcamentoDados(
    Guid ServicoId,
    Guid ServicoCatalogoId,
    string Descricao,
    decimal Valor);
