using Catalog.Bll.Dto;
using Catalog.Bll.Interfaces;
using Catalog.Domain.Exceptions;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using CatalogGrpc = Catalog.Grpc;

namespace Catalog.Api.Grpc;

/// <summary>
/// gRPC-адаптер над BLL (як контролер): валідує вхідні дані, викликає ті самі сервіси, що й REST
/// (тож дворівневий кеш працює для обох протоколів), і перетворює DTO на protobuf-повідомлення.
/// Доменні винятки → RpcException з коректним статусом (NotFound / InvalidArgument / FailedPrecondition),
/// а не загальний Internal.
/// </summary>
public sealed class CatalogGrpcService(IMovieService movies, IShowtimeService showtimes)
    : CatalogGrpc.CatalogService.CatalogServiceBase
{
    private const int MaxIdsPerCall = 100;

    public override Task<CatalogGrpc.MovieReply> GetMovie(CatalogGrpc.GetMovieRequest request, ServerCallContext context)
        => GrpcErrors.Guard(async () =>
        {
            GrpcErrors.RequirePositive(request.Id, "id");
            return ToReply(await movies.GetByIdAsync(request.Id, context.CancellationToken));
        });

    public override Task<CatalogGrpc.ShowtimeReply> GetShowtime(CatalogGrpc.GetShowtimeRequest request, ServerCallContext context)
        => GrpcErrors.Guard(async () =>
        {
            GrpcErrors.RequirePositive(request.Id, "id");
            return ToReply(await showtimes.GetByIdAsync(request.Id, context.CancellationToken));
        });

    public override Task<CatalogGrpc.ShowtimesReply> GetShowtimesByIds(CatalogGrpc.GetShowtimesByIdsRequest request, ServerCallContext context)
        => GrpcErrors.Guard(async () =>
        {
            if (request.Ids.Count is 0 or > MaxIdsPerCall)
                throw new RpcException(new Status(StatusCode.InvalidArgument, $"ids must contain 1–{MaxIdsPerCall} values"));
            foreach (var id in request.Ids) GrpcErrors.RequirePositive(id, "ids");

            var reply = new CatalogGrpc.ShowtimesReply();
            foreach (var id in request.Ids.Distinct())
            {
                try
                {
                    reply.Showtimes.Add(ToReply(await showtimes.GetByIdAsync(id, context.CancellationToken)));
                }
                catch (EntityNotFoundException)
                {
                    // відсутні сеанси пропускаються (контракт GetShowtimesByIds)
                }
            }
            return reply;
        });

    private static CatalogGrpc.MovieReply ToReply(MovieDto m)
    {
        var reply = new CatalogGrpc.MovieReply
        {
            Id = m.Id,
            Title = m.Title,
            ReleaseYear = m.ReleaseYear,
            DurationMin = m.DurationMin,
            AgeRating = m.AgeRating,
            BasePrice = Money(m.BasePrice),
            IsActive = m.IsActive,
            Director = m.Detail?.Director ?? "",
            Country = m.Detail?.Country ?? ""
        };
        reply.Genres.AddRange(m.Genres.Select(g => new CatalogGrpc.GenreReply { Id = g.GenreId, Name = g.Name, IsPrimary = g.IsPrimary }));
        return reply;
    }

    private static CatalogGrpc.ShowtimeReply ToReply(ShowtimeDto s) => new()
    {
        Id = s.Id,
        MovieId = s.MovieId,
        MovieTitle = s.MovieTitle,
        HallId = s.HallId,
        HallName = s.HallName,
        StartsAt = Timestamp.FromDateTime(DateTime.SpecifyKind(s.StartsAt, DateTimeKind.Utc)),
        Price = Money(s.Price),
        Language = s.Language,
        IsCancelled = s.IsCancelled
    };

    private static CatalogGrpc.DecimalValue Money(decimal value)
    {
        var units = decimal.ToInt64(decimal.Truncate(value));
        return new CatalogGrpc.DecimalValue { Units = units, Nanos = decimal.ToInt32((value - units) * 1_000_000_000m) };
    }
}

/// <summary>Перетворення доменних винятків каталогу на gRPC-статуси.</summary>
internal static class GrpcErrors
{
    public static void RequirePositive(long value, string field)
    {
        if (value <= 0) throw new RpcException(new Status(StatusCode.InvalidArgument, $"{field} must be positive"));
    }

    public static async Task<T> Guard<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (EntityNotFoundException ex) { throw new RpcException(new Status(StatusCode.NotFound, ex.Message)); }
        catch (ValidationException ex) { throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message)); }
        catch (ConflictException ex) { throw new RpcException(new Status(StatusCode.FailedPrecondition, ex.Message)); }
        catch (DomainException ex) { throw new RpcException(new Status(StatusCode.FailedPrecondition, ex.Message)); }
    }
}
