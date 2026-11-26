using Bookings.Dal;
using Bookings.Domain.Exceptions;
using Bookings.Domain.Models;

namespace Bookings.Tests;

/// <summary>Integration tests DAL/UoW проти справжньої БД (див. BOOKINGS_TEST_DB).</summary>
public class UnitOfWorkIntegrationTests
{
    [DbFact]
    public async Task Error_in_second_repository_rolls_back_first_repository()
    {
        var key = $"rollback-{Guid.NewGuid():N}";
        long bookingId;

        // підготовка: нове бронювання в статусі Created (ще не передане на оплату)
        await using (var uow = new UnitOfWork(TestDb.ConnectionString!))
        {
            bookingId = await uow.Bookings.AddAsync(new Booking
            {
                CustomerId = 1, BookingCode = key[^8..].ToUpperInvariant(), Status = BookingStatus.Created, TotalAmount = 100
            });
            await uow.Bookings.AddDetailsAsync(new BookingDetails { BookingId = bookingId });
        }

        // транзакція: репозиторій 1 (Payments) пише, репозиторій 2 (процедура зміни статусу) відмовляє
        await using (var uow = new UnitOfWork(TestDb.ConnectionString!))
        {
            await uow.BeginTransactionAsync();
            try
            {
                await uow.Payments.AddAsync(new Payment
                {
                    BookingId = bookingId, Amount = 100, Method = "Card",
                    Status = PaymentStatus.Succeeded, ProviderTxnId = key, PaidAt = DateTime.UtcNow
                });
                // Created -> Paid недопустимо: THROW 50002 -> BusinessConflictException
                await Assert.ThrowsAsync<BusinessConflictException>(() =>
                    uow.Bookings.ChangeStatusAsync(bookingId, BookingStatus.Paid, null, null, "test"));
            }
            finally
            {
                await uow.RollbackAsync();
            }
        }

        // перевірка: оплата НЕ збереглася, статус не змінився
        await using (var uow = new UnitOfWork(TestDb.ConnectionString!))
        {
            Assert.Null(await uow.Payments.GetByProviderTxnIdAsync(key));
            Assert.Equal(BookingStatus.Created, (await uow.Bookings.GetByIdAsync(bookingId))!.Status);
        }
    }

    [DbFact]
    public async Task Parameters_handle_special_characters_like_OBrien()
    {
        await using var uow = new UnitOfWork(TestDb.ConnectionString!);
        var email = $"obrien.{Guid.NewGuid():N}@example.com";

        var id = await uow.Customers.AddAsync(new Customer { Email = email, FullName = "Conan O'Brien; DROP TABLE x--" }, "test");
        var found = await uow.Customers.SearchAsync("O'Brien", 1, 50);

        Assert.Contains(found, c => c.Id == id && c.FullName.StartsWith("Conan O'Brien"));
    }
}
