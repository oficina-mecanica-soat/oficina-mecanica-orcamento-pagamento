using OficinaMecanica.OrcamentoPagamento.Infrastructure.Mensagens;

namespace OficinaMecanica.OrcamentoPagamento.Infrastructure.Hosting;

public sealed record ProcessamentoRemotoOptions(string FilaEntradaUrl, string FilaResultadosOsUrl)
{
    public void Validar()
    {
        ContratoFilaSqs.ValidarUrl(FilaEntradaUrl);
        ContratoFilaSqs.ValidarUrl(FilaResultadosOsUrl);
        if (new Uri(FilaEntradaUrl) == new Uri(FilaResultadosOsUrl))
        {
            throw new ArgumentException("A entrada de Orçamento/Pagamento não pode receber os resultados destinados à OS.");
        }
    }
}
