using System.Net;

namespace OficinaMecanica.OrcamentoPagamento.API.IntegrationTests.Pagamentos.Factories;

internal sealed class RespostaProvedorHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responder;

    public RespostaProvedorHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
    {
        _responder = responder;
    }

    public int QuantidadeRequisicoes { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        QuantidadeRequisicoes++;
        return _responder(request, cancellationToken);
    }

    public static HttpResponseMessage Json(string conteudo, HttpStatusCode status = HttpStatusCode.OK)
    {
        return new(status) { Content = new StringContent(conteudo, System.Text.Encoding.UTF8, "application/json") };
    }
}
