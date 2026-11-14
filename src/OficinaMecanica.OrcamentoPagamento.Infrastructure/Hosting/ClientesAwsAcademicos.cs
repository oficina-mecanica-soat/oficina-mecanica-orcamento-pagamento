using Amazon;
using Amazon.Runtime;
using Amazon.SecurityToken;
using Amazon.SQS;

namespace OficinaMecanica.OrcamentoPagamento.Infrastructure.Hosting;

public sealed class ClientesAwsAcademicos : IDisposable
{
    private readonly InstanceProfileAWSCredentials _credenciais;
    public IAmazonSQS Sqs { get; }

    private ClientesAwsAcademicos(InstanceProfileAWSCredentials credenciais, RegionEndpoint regiao)
    {
        _credenciais = credenciais;
        Sqs = new AmazonSQSClient(credenciais, new AmazonSQSConfig
        {
            RegionEndpoint = regiao,
            Timeout = TimeSpan.FromSeconds(30),
            MaxErrorRetry = 0
        });
    }

    public static async Task<ClientesAwsAcademicos> CriarAsync(RegionEndpoint regiao, string contaEsperada, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(regiao);
        IdentidadeAcademica.ValidarConta(contaEsperada);
        ct.ThrowIfCancellationRequested();
        // Fonte explícita: não usar a cadeia padrão que também aceita env/perfis do runner.
        var credenciais = new InstanceProfileAWSCredentials("LabRole");
        try
        {
            using var sts = new AmazonSecurityTokenServiceClient(credenciais, regiao);
            await IdentidadeAcademica.ValidarAsync(sts, contaEsperada, ct);
            ct.ThrowIfCancellationRequested();
            return new ClientesAwsAcademicos(credenciais, regiao);
        }
        catch (Exception)
        {
            credenciais.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        Sqs.Dispose();
        _credenciais.Dispose();
    }
}
