using System.Diagnostics;
using Datadog.Trace;
using Microsoft.Extensions.Logging;
using OficinaMecanica.OrcamentoPagamento.Application.Mensagens;

namespace OficinaMecanica.OrcamentoPagamento.Infrastructure.Observabilidade;

public sealed class ObservabilidadeMensagens
{
    private readonly ILogger<ObservabilidadeMensagens> _logger;

    public ObservabilidadeMensagens(ILogger<ObservabilidadeMensagens> logger)
    {
        _logger = logger;
    }

    public Dictionary<string, string>? CapturarContexto(IReadOnlyDictionary<string, string>? origem)
    {
        try
        {
            if (Tracer.Instance.ActiveScope is { } scope)
            {
                var carrier = new Dictionary<string, string>(StringComparer.Ordinal);
                new SpanContextInjector().Inject(carrier, (contexto, key, value) => contexto[key] = value, scope.Span.Context);
                if (ContextoTrace.Filtrar(carrier) is { } contexto)
                {
                    return contexto;
                }
            }
        }
        catch (Exception)
        {
            // Sem tracer ativo, preservar somente o carrier de origem validado.
        }
        return ContextoTrace.Filtrar(origem);
    }

    public Escopo Iniciar(string operacao, string corpo)
    {
        IScope? trace = null;
        IDisposable? logScope = null;
        try
        {
            var mensagem = JsonContrato.LerEnvelope(corpo);
            var contexto = ContextoTrace.Filtrar(mensagem.TraceContext);
            var parent = contexto is null ? null : new SpanContextExtractor().Extract(contexto,
                (carrier, key) => carrier.TryGetValue(key, out var value) ? [value] : Array.Empty<string>());
            trace = Tracer.Instance.StartActive(operacao, new SpanCreationSettings { Parent = parent });
            trace.Span.ResourceName = operacao;
            trace.Span.Type = "queue";
            trace.Span.SetTag("messaging.system", "aws_sqs");
            trace.Span.SetTag("ordem_servico_id", mensagem.OrdemServicoId.ToString("D"));
            trace.Span.SetTag("saga_id", mensagem.SagaId.ToString("D"));
            var correlacao = mensagem.CorrelationId;
            logScope = _logger.BeginScope(new Dictionary<string, object?>
            {
                ["operation"] = operacao,
                ["bounded_context"] = "OrcamentoPagamento",
                ["mensagem_id"] = mensagem.MensagemId,
                ["ordem_servico_id"] = mensagem.OrdemServicoId,
                ["saga_id"] = mensagem.SagaId,
                ["x_correlation_id"] = !string.IsNullOrWhiteSpace(correlacao) && correlacao.Length <= 128
                    && correlacao.All(caractere => !char.IsControl(caractere)) ? correlacao : null
            });
        }
        catch (Exception)
        {
            // Falha de telemetria ou envelope inválido não substitui a validação do processador.
        }
        return new Escopo(trace, logScope, _logger);
    }

    public sealed class Escopo : IDisposable
    {
        private readonly IScope? _trace;
        private readonly IDisposable? _logScope;
        private readonly ILogger _logger;
        private readonly long _inicio = Stopwatch.GetTimestamp();
        private bool _concluido;
        private bool _descartado;

        internal Escopo(IScope? trace, IDisposable? logScope, ILogger logger)
        {
            _trace = trace;
            _logScope = logScope;
            _logger = logger;
        }

        public void Concluir()
        {
            _concluido = true;
        }

        public void Dispose()
        {
            if (_descartado)
            {
                return;
            }
            _descartado = true;
            try
            {
                if (_trace is not null)
                {
                    _trace.Span.Error = !_concluido;
                }
                var duracao = Stopwatch.GetElapsedTime(_inicio).TotalMilliseconds;
                if (_concluido)
                {
                    _logger.LogInformation("Operação de mensagem concluída; resultado {outcome}, duração {duration_ms} ms.", "concluida", duracao);
                }
                else
                {
                    _logger.LogWarning("Operação de mensagem pendente de recuperação; resultado {outcome}, duração {duration_ms} ms.", "falha", duracao);
                }
            }
            catch (Exception)
            {
                // Telemetria não pode confirmar, repetir ou desfazer a transação de negócio.
            }
            finally
            {
                try
                {
                    _trace?.Dispose();
                }
                catch (Exception)
                {
                    // Preservar o resultado de negócio mesmo sem o tracer disponível.
                }
                try
                {
                    _logScope?.Dispose();
                }
                catch (Exception)
                {
                    // Falha no provedor de logs não altera o processamento da mensagem.
                }
            }
        }
    }
}
