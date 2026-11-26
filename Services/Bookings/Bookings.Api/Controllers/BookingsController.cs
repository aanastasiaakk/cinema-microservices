using System.ComponentModel.DataAnnotations;
using Bookings.Bll.Dto;
using Bookings.Bll.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Bookings.Api.Controllers;

/// <summary>Бронювання квитків. Помилки перетворює на статуси глобальний DomainExceptionHandler.</summary>
[ApiController]
[Route("api/bookings")]
[Produces("application/json")]
public sealed class BookingsController(IBookingService bookings, IPaymentService payments) : ControllerBase
{
    /// <summary>Бронювання з квитками.</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType<BookingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingDto>> GetById(long id, CancellationToken ct)
        => Ok(await bookings.GetAsync(id, ct));

    /// <summary>Створити бронювання з квитками (одна транзакція: Bookings + BookingDetails + Tickets).</summary>
    [HttpPost]
    [ProducesResponseType<BookingDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BookingDto>> Create(CreateBookingDto input, CancellationToken ct)
    {
        var created = await bookings.CreateAsync(input, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Передати на оплату: Created → AwaitingPayment.</summary>
    [HttpPost("{id:long}/submit")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Submit(long id, CancellationToken ct)
    {
        await bookings.SubmitAsync(id, ct);
        return NoContent();
    }

    /// <summary>
    /// Оплатити бронювання (одна транзакція: Payments + Bookings.Status = Paid).
    /// Заголовок Idempotency-Key обов'язковий: повтор з тим самим ключем не списує гроші вдруге (200 замість 201).
    /// </summary>
    [HttpPost("{id:long}/payments")]
    [ProducesResponseType<PaymentDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<PaymentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PaymentDto>> Pay(
        long id,
        [FromHeader(Name = "Idempotency-Key"), Required, MaxLength(100)] string idempotencyKey,
        PayBookingDto input,
        CancellationToken ct)
    {
        var result = await payments.PayAsync(id, idempotencyKey, input, ct);
        return result.Replayed
            ? Ok(result.Payment)
            : CreatedAtAction(nameof(GetPayments), new { id }, result.Payment);
    }

    /// <summary>Оплати бронювання.</summary>
    [HttpGet("{id:long}/payments")]
    [ProducesResponseType<IReadOnlyList<PaymentDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<PaymentDto>>> GetPayments(long id, CancellationToken ct)
        => Ok(await payments.GetByBookingAsync(id, ct));

    /// <summary>Пропустити в зал: Paid → CheckedIn (не раніше ніж за годину до сеансу).</summary>
    [HttpPost("{id:long}/check-in")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CheckIn(long id, CancellationToken ct)
    {
        await bookings.CheckInAsync(id, ct);
        return NoContent();
    }

    /// <summary>Скасувати (лише до оплати; інакше 409).</summary>
    [HttpPost("{id:long}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(long id, CancelBookingDto input, CancellationToken ct)
    {
        await bookings.CancelAsync(id, input.Reason, ct);
        return NoContent();
    }

    /// <summary>М'яко видалити скасоване бронювання.</summary>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await bookings.DeleteAsync(id, ct);
        return NoContent();
    }
}
