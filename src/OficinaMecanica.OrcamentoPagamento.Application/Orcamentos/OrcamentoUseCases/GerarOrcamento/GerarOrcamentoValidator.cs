using FluentValidation;
using OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.ValidationMessages;
using OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos;

namespace OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.OrcamentoUseCases.GerarOrcamento;

public sealed class GerarOrcamentoValidator : AbstractValidator<GerarOrcamentoRequest>
{
    public GerarOrcamentoValidator()
    {
        RuleFor(request => request).NotNull().WithMessage(OrcamentoValidationMessages.RequestObrigatorio);
        When(request => request is not null, () =>
        {
            RuleFor(request => request.OrdemServicoId).NotEmpty().WithMessage(OrcamentoValidationMessages.OrdemServicoObrigatoria);
            RuleFor(request => request.ClienteId).NotEmpty().WithMessage(OrcamentoValidationMessages.ClienteObrigatorio);
            RuleFor(request => request.Versao).GreaterThan(0).WithMessage(OrcamentoValidationMessages.VersaoInvalida);
            RuleFor(request => request.Contexto).NotNull().WithMessage(OrcamentoValidationMessages.ContextoObrigatorio);
            When(request => request.Contexto is not null, () =>
            {
                RuleFor(request => request.Contexto.SagaId).NotEmpty().WithMessage(OrcamentoValidationMessages.SagaObrigatoria);
                RuleFor(request => request.Contexto.MensagemId).NotEmpty().WithMessage(OrcamentoValidationMessages.MensagemObrigatoria);
                RuleFor(request => request.Contexto.CorrelationId).NotEmpty().WithMessage(OrcamentoValidationMessages.CorrelacaoObrigatoria);
                RuleFor(request => request.Contexto.CorrelationId).MaximumLength(128).WithMessage(OrcamentoValidationMessages.CorrelacaoInvalida)
                    .Must(valor => valor is not null && valor.All(c => !char.IsControl(c)))
                    .WithMessage(OrcamentoValidationMessages.CorrelacaoInvalida);
            });
            RuleFor(request => request.Servicos).NotEmpty().WithMessage(OrcamentoValidationMessages.ServicosObrigatorios);
            RuleFor(request => request.PecasInsumos).NotNull().WithMessage(OrcamentoValidationMessages.PecasObrigatorias);
            RuleForEach(request => request.Servicos).NotNull().WithMessage(OrcamentoValidationMessages.ItemObrigatorio);
            RuleForEach(request => request.PecasInsumos).NotNull().WithMessage(OrcamentoValidationMessages.ItemObrigatorio);
            RuleForEach(request => request.Servicos).Where(item => item is not null).SetValidator(new ServicoSnapshotValidator());
            RuleForEach(request => request.PecasInsumos).Where(item => item is not null).SetValidator(new PecaSnapshotValidator());
        });
    }
}
