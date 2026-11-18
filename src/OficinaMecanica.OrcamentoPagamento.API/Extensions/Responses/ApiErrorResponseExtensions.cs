using System.Text.Json;
using System.Text.Json.Serialization;
using OficinaMecanica.OrcamentoPagamento.API.Responses;
using OficinaMecanica.OrcamentoPagamento.Application.Common;

namespace OficinaMecanica.OrcamentoPagamento.API.Extensions.Responses;

public static class ApiErrorResponseExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static Task WriteApiErrorResponseAsJsonAsync(this HttpResponse response, ErrorResponse error)
    {
        response.ContentType = ApiResponseContentTypes.Json;

        return response.WriteAsJsonAsync(error, JsonOptions);
    }
}
