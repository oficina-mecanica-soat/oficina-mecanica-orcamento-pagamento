using FluentValidation;
using OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.ValidationMessages;
using OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos;

namespace OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.OrcamentoUseCases.GerarOrcamento;

public sealed class ServicoSnapshotValidator : AbstractValidator<ServicoOrcamentoRequest>
{
    public ServicoSnapshotValidator()
    {
        RuleFor(item => item.ServicoId).NotEmpty().WithMessage(OrcamentoValidationMessages.ServicoIdObrigatorio);
        RuleFor(item => item.ServicoCatalogoId).NotEmpty().WithMessage(OrcamentoValidationMessages.ServicoCatalogoIdObrigatorio);
        RuleFor(item => item.Descricao).NotEmpty().WithMessage(OrcamentoValidationMessages.DescricaoInvalida).MaximumLength(RegrasSnapshot.DescricaoMaxima).WithMessage(OrcamentoValidationMessages.DescricaoInvalida);
        RuleFor(item => item.Valor).Must(valor => valor > 0 && valor <= RegrasSnapshot.ValorMaximo && decimal.Round(valor, 2) == valor)
            .WithMessage(OrcamentoValidationMessages.ValorInvalido);
    }
}
