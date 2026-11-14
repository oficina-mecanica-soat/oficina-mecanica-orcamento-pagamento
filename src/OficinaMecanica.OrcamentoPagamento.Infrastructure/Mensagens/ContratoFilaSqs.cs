namespace OficinaMecanica.OrcamentoPagamento.Infrastructure.Mensagens;

internal static class ContratoFilaSqs
{
    internal static void ValidarUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps
            || !uri.AbsolutePath.EndsWith(".fifo", StringComparison.Ordinal) || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment) || !string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new ArgumentException("Informe a URL HTTPS de uma fila FIFO provisionada pelo owner.", nameof(url));
        }
    }
}
