using System.Text.Json.Serialization;
namespace OficinaMecanica.OrcamentoPagamento.Application.Mensagens;

public sealed record GerarOrcamentoDados(int Versao, Guid ClienteId, ServicoOrcamentoDados[] Servicos, ItemOrcamentoDados[] Itens);
public sealed record ServicoOrcamentoDados(Guid ServicoId, Guid ServicoCatalogoId, string Descricao, decimal Valor);
public sealed record ItemOrcamentoDados(Guid ItemId, string Descricao, int Quantidade, decimal ValorUnitario);
public sealed record CancelarOrcamentoDados(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? OrcamentoId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Versao, string Motivo);
public sealed record OrcamentoGeradoDados(Guid OrcamentoId, int Versao, decimal ValorTotal);
public sealed record OrcamentoDecididoDados(Guid OrcamentoId, int Versao, string Decisao);
public sealed record OrcamentoCanceladoDados(Guid? OrcamentoId, int Versao);
