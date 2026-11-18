namespace OficinaMecanica.OrcamentoPagamento.Application.Mensagens;

public sealed record CancelamentoRecebido(
    Guid OrdemServicoId,
    int Versao,
    Guid SagaId,
    string Motivo);
