using AutoMapper;
using OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.PagamentoUseCases.Responses;
using OficinaMecanica.OrcamentoPagamento.Domain.Pagamentos.Aggregates;
using OficinaMecanica.OrcamentoPagamento.Domain.Pagamentos.Enums;

namespace OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.Mappings;

public sealed class PagamentoProfile : Profile
{
    public PagamentoProfile()
    {
        CreateMap<Pagamento, PagamentoResponse>()
            .ForMember(destino => destino.Status, configuracao => configuracao.MapFrom(origem => origem.Status.ToString()))
            .ForMember(destino => destino.EnderecoCheckout, configuracao => configuracao.MapFrom(origem =>
                origem.Status == StatusPagamento.Confirmado ? null : origem.EnderecoCheckout));
    }
}
