using System.Text;
using OficinaMecanica.OrcamentoPagamento.Application.Mensagens;
namespace OficinaMecanica.OrcamentoPagamento.Infrastructure.Mensagens;
/// <summary>Transporte de desenvolvimento local; não emula garantias FIFO/DLQ do SQS.</summary>
public sealed class TransporteArquivos : ITransporteMensagem
{
    private readonly string _diretorio;
    public TransporteArquivos(string diretorio) { _diretorio = Path.GetFullPath(diretorio); }
    public async Task PublicarAsync(string destino, string corpo, CancellationToken cancellationToken)
    {
        if (destino is not ("OrcamentoPagamento" or "OrdemServico" or "Execucao")) { throw new ContratoMensagemException("DESTINO_INVALIDO"); }
        var mensagem = JsonContrato.LerEnvelope(corpo);
        var pasta = Path.Combine(_diretorio, destino, "entrada");
        Directory.CreateDirectory(pasta);
        var caminho = Path.Combine(pasta, $"{mensagem.MensagemId:D}.json");
        if (File.Exists(caminho))
        {
            if (await File.ReadAllTextAsync(caminho, cancellationToken) != corpo) { throw new IOException("Mensagem local com conteúdo divergente."); }
            return;
        }
        var temporario = caminho + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporario, corpo, Encoding.UTF8, cancellationToken);
            try { File.Move(temporario, caminho, false); }
            catch (IOException) when (File.Exists(caminho))
            {
                if (await File.ReadAllTextAsync(caminho, cancellationToken) != corpo) { throw; }
            }
        }
        finally { File.Delete(temporario); }
    }
}
