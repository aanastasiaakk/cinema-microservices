using MediatR;

namespace Reviews.Application.Common.Messaging;

/// <summary>Команда без результату (запис/зміна/видалення).</summary>
public interface ICommand : IRequest;

/// <summary>Команда з результатом.</summary>
public interface ICommand<out TResponse> : IRequest<TResponse>;

/// <summary>Запит на читання (нічого не змінює).</summary>
public interface IQuery<out TResponse> : IRequest<TResponse>;
