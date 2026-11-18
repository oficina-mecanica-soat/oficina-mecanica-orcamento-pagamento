using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OficinaMecanica.OrcamentoPagamento.Application.Mensagens;

public static class JsonContrato
{
    private static readonly JsonSerializerOptions OpcoesHash = new(JsonSerializerDefaults.Web)
    {
        Converters = { new DecimalNormalizadoConverter() }
    };
    public static JsonSerializerOptions Options
    { get; } = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        NumberHandling = JsonNumberHandling.Strict
    };
    public static string Serializar<T>(T valor) => JsonSerializer.Serialize(valor, Options);
    public static T Ler<T>(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, Options) ?? throw new ContratoMensagemException("JSON_NULO");
        }
        catch (JsonException)
        {
            throw new ContratoMensagemException("JSON_INVALIDO");
        }
    }

    public static MensagemIntegracao LerEnvelope(string json)
    {
        var mensagem = Ler<MensagemIntegracao>(json);
        if (mensagem.MensagemId == Guid.Empty || mensagem.SagaId == Guid.Empty || mensagem.OrdemServicoId == Guid.Empty
            || mensagem.CausationId == Guid.Empty || mensagem.Versao != 1 || string.IsNullOrWhiteSpace(mensagem.Tipo)
            || string.IsNullOrWhiteSpace(mensagem.CorrelationId) || mensagem.CorrelationId.Length > 128
            || mensagem.CorrelationId.Any(char.IsControl) || mensagem.OcorridoEm == default
            || mensagem.OcorridoEm.Offset != TimeSpan.Zero || mensagem.Dados.ValueKind != JsonValueKind.Object)
        {
            throw new ContratoMensagemException("ENVELOPE_INVALIDO");
        }
        return mensagem with
        {
            TraceContext = ContextoTrace.Filtrar(mensagem.TraceContext)
        };
    }

    public static string Hash<T>(MensagemIntegracao mensagem, T dados) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            mensagem.Tipo,
            mensagem.SagaId,
            mensagem.OrdemServicoId,
            mensagem.CorrelationId,
            Dados = dados
        }, OpcoesHash))));

    private sealed class DecimalNormalizadoConverter : JsonConverter<decimal>
    {
        public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.GetDecimal();
        public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) =>
            writer.WriteRawValue(value.ToString("G29", CultureInfo.InvariantCulture));
    }
}
