using System.Diagnostics;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Серверний interceptor gRPC: для кожного виклику — метод, peer (адреса клієнта), статус і тривалість
/// структурованими властивостями. RpcException (NotFound, InvalidArgument…) — Warning зі статусом,
/// неочікуваний виняток — Error зі stack trace (gRPC поверне клієнту Internal/Unknown).
/// Підключення: builder.Services.AddGrpc(o => o.Interceptors.Add&lt;GrpcLoggingInterceptor&gt;()).
/// </summary>
public sealed class GrpcLoggingInterceptor(ILogger<GrpcLoggingInterceptor> logger) : Interceptor
{
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request, ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
    {
        var sw = Stopwatch.StartNew();
        Activity.Current?.SetTag("rpc.grpc.peer", context.Peer);
        try
        {
            var response = await continuation(request, context);
            logger.LogInformation("gRPC {GrpcMethod} from {GrpcPeer}: {GrpcStatus} in {ElapsedMs} ms",
                context.Method, context.Peer, StatusCode.OK, sw.ElapsedMilliseconds);
            return response;
        }
        catch (RpcException ex)
        {
            logger.LogWarning("gRPC {GrpcMethod} from {GrpcPeer}: {GrpcStatus} in {ElapsedMs} ms, {GrpcDetail}",
                context.Method, context.Peer, ex.StatusCode, sw.ElapsedMilliseconds, ex.Status.Detail);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "gRPC {GrpcMethod} from {GrpcPeer}: unhandled {ExceptionType} in {ElapsedMs} ms",
                context.Method, context.Peer, ex.GetType().Name, sw.ElapsedMilliseconds);
            throw;
        }
    }
}
