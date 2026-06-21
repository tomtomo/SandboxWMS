using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wms.BuildingBlocks.Domain.Results;
using Wms.BuildingBlocks.Web.ErrorHandling;
using Wms.Inbound.Application.DependencyInjection;
using Wms.Inbound.Application.Features.CreateGoodsReceipt;
using Wms.Inbound.Domain;
using Wms.Inbound.Infrastructure.DependencyInjection;
using Wms.Inbound.Infrastructure.Persistence;
using Wms.TestSupport;

namespace Wms.Inbound.IntegrationTests;

// What: behavioral test pipeline (DoD Phase 02a) — failing command → ProblemDetails + no write
// Why: membuktikan jaminan end-to-end di atas Postgres NYATA: command yang gagal validasi
// mengalir lewat pipeline penuh (ISender) → Result(Validation) → RFC 7807 ProblemDetails 400
// TANPA exception bocor, dan UoW tak menulis parsial (nol GoodsReceipt) — ValidationBehavior
// short-circuit SEBELUM TransactionBehavior membuka transaksi.
[Collection(PostgresCollection.Name)]
public sealed class PipelineBehaviorTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Invalid_command_maps_to_problemdetails_and_persists_nothing()
    {
        await using var provider = new ServiceCollection()
            .AddLogging()
            .AddInboundApplication()
            .AddInboundProductCatalogStub()
            .AddInboundInfrastructure(await fixture.CreateDatabaseAsync())
            .BuildServiceProvider();

        using (var schemaScope = provider.CreateScope())
            await schemaScope.ServiceProvider.GetRequiredService<InboundDbContext>()
                .Database.EnsureCreatedAsync();

        Result<Guid> result;
        using (var scope = provider.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            // warehouse kosong + nol line → gagal di ValidationBehavior, sebelum transaksi/handler
            result = await sender.Send(new CreateGoodsReceiptCommand(string.Empty, []));
        }

        // failure jadi NILAI (no-throw), bertipe Validation
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);

        // dipetakan ke RFC 7807 ProblemDetails 400 — tanpa exception bocor
        var problem = Assert.IsType<ProblemHttpResult>(result.ToProblemDetails());
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);

        // UoW tak commit parsial: nol GoodsReceipt ter-persist
        using var assertScope = provider.CreateScope();
        var db = assertScope.ServiceProvider.GetRequiredService<InboundDbContext>();
        Assert.Equal(0, await db.Set<GoodsReceipt>().CountAsync());
    }
}
