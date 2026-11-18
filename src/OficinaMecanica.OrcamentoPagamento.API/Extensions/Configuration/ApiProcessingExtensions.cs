using Amazon;
using OficinaMecanica.OrcamentoPagamento.Infrastructure.Hosting;

namespace OficinaMecanica.OrcamentoPagamento.API.Extensions.Configuration;

public static class ApiProcessingExtensions
{
    public static async Task<ClientesAwsAcademicos?> AddApiProcessingAsync(this WebApplicationBuilder builder, CancellationToken cancellationToken = default)
    {
        if (!builder.Configuration.GetValue<bool>("ProcessamentoRemoto:Habilitado"))
        {
            return null;
        }
        var secao = builder.Configuration.GetSection("ProcessamentoRemoto");
        var conta = secao["ContaEsperada"] ?? string.Empty;
        IdentidadeAcademica.ValidarConta(conta);
        var regiao = RegionEndpoint.EnumerableAllRegions.SingleOrDefault(endpoint => endpoint.SystemName == secao["Regiao"])
            ?? throw new ArgumentException("Configure a região AWS do processamento remoto.");
        var options = new ProcessamentoRemotoOptions(secao["FilaEntradaUrl"] ?? string.Empty, secao["FilaResultadosOsUrl"] ?? string.Empty);
        options.Validar();
        ValidarFila(options.FilaEntradaUrl, conta, regiao.SystemName);
        ValidarFila(options.FilaResultadosOsUrl, conta, regiao.SystemName);

        var clientes = await ClientesAwsAcademicos.CriarAsync(regiao, conta, cancellationToken);
        try
        {
            builder.Services.AddProcessamentoRemoto(options, clientes.Sqs);
            return clientes;
        }
        catch (Exception)
        {
            clientes.Dispose();
            throw;
        }
    }

    private static void ValidarFila(string url, string conta, string regiao)
    {
        var uri = new Uri(url);
        var partes = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (uri.Host != $"sqs.{regiao}.amazonaws.com" || !uri.IsDefaultPort || partes.Length != 2 || partes[0] != conta)
        {
            throw new ArgumentException("As filas devem pertencer à conta e região configuradas.");
        }
    }
}
