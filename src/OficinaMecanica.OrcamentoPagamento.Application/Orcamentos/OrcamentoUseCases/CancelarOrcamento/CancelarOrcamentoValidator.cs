using FluentValidation;
using OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.ValidationMessages;

namespace OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.OrcamentoUseCases.CancelarOrcamento;

public sealed class CancelarOrcamentoValidator : AbstractValidator<CancelarOrcamentoRequest>
{
    public CancelarOrcamentoValidator()
    {
        RuleFor(request => request).NotNull().WithMessage(OrcamentoValidationMessages.RequestObrigatorio);
        When(request => request is not null, () =>
        {
            RuleFor(request => request.OrcamentoId).NotEmpty().WithMessage(OrcamentoValidationMessages.OrcamentoIdObrigatorio);
            RuleFor(request => request.Versao).GreaterThan(0).WithMessage(OrcamentoValidationMessages.VersaoInvalida);
            RuleFor(request => request.OperacaoId).NotEmpty().WithMessage(OrcamentoValidationMessages.MensagemObrigatoria);
            RuleFor(request => request.Motivo).NotEmpty().WithMessage(OrcamentoValidationMessages.MotivoObrigatorio).MaximumLength(160).WithMessage(OrcamentoValidationMessages.MotivoObrigatorio);
        });
    }
}
