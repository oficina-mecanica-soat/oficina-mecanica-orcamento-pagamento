using System.Text.Json;
using OficinaMecanica.OrcamentoPagamento.Application.Mensagens;

namespace OficinaMecanica.OrcamentoPagamento.Infrastructure.Mensagens;

public sealed class RegistroOutbox
{
    private RegistroOutbox() { Tipo = string.Empty; Corpo = string.Empty; }
    public Guid Id { get; private set; }
    public Guid OrdemServicoId { get; private set; }
    public int VersaoNegocio { get; private set; }
    public string Tipo { get; private set; }
    public string Corpo { get; private set; }
    public DateTimeOffset CriadoEm { get; private set; }
    public DateTimeOffset? PublicadoEm { get; private set; }
    public Guid? Responsavel { get; private set; }
    public DateTimeOffset? LeaseAte { get; private set; }
    public int Tentativas { get; private set; }
    public DateTimeOffset? ProximaTentativaEm { get; private set; }

    public static RegistroOutbox Criar(MensagemIntegracao mensagem, int versaoNegocio)
    {
        return new RegistroOutbox
        {
            Id = mensagem.MensagemId,
            OrdemServicoId = mensagem.OrdemServicoId,
            VersaoNegocio = versaoNegocio,
            Tipo = mensagem.Tipo,
            CriadoEm = mensagem.OcorridoEm,
            Corpo = JsonSerializer.Serialize(mensagem, new JsonSerializerOptions(JsonSerializerDefaults.Web))
        };
    }
}
