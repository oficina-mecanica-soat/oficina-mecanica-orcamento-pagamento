using System.Data;
using Microsoft.EntityFrameworkCore;
using OficinaMecanica.OrcamentoPagamento.Application.Mensagens;
using OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.Interfaces;
using OficinaMecanica.OrcamentoPagamento.Infrastructure.Mensagens;
using OficinaMecanica.OrcamentoPagamento.Infrastructure.Persistence;

namespace OficinaMecanica.OrcamentoPagamento.Infrastructure.Mensagens;

public sealed class SqlInbox : IInbox
{
    private readonly OrcamentoPagamentoDbContext _dbContext;

    public SqlInbox(OrcamentoPagamentoDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    public async Task ExecutarEmTransacaoAsync(Func<CancellationToken, Task> operacao, CancellationToken cancellationToken)
    {
        await using var transacao = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await operacao(cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transacao.CommitAsync(cancellationToken);
    }

    public async Task<bool> VerificarRepeticaoAsync(Guid mensagemId, string chave, string hash, CancellationToken cancellationToken)
    {
        var recebimentos = _dbContext.Set<RecebimentoMensagem>();
        var recebido = await recebimentos.FindAsync([mensagemId], cancellationToken);
        if (recebido is not null)
        {
            if (recebido.ChaveOperacao != chave || recebido.Hash != hash)
            {
                throw new ContratoMensagemException("MENSAGEM_DIVERGENTE");
            }
            return true;
        }
        var operacao = await recebimentos.FirstOrDefaultAsync(r => r.ChaveOperacao == chave, cancellationToken);
        if (operacao is null)
        {
            return false;
        }
        if (operacao.Hash != hash)
        {
            throw new ContratoMensagemException("OPERACAO_DIVERGENTE");
        }
        return true;
    }

    public async Task RegistrarAsync(Guid mensagemId, string chave, string hash, CancellationToken cancellationToken)
    {
        if (await _dbContext.Set<RecebimentoMensagem>().FindAsync([mensagemId], cancellationToken) is null)
        {
            await _dbContext.AddAsync(new RecebimentoMensagem { Id = mensagemId, ChaveOperacao = chave, Hash = hash }, cancellationToken);
        }
    }
}
