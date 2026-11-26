using AutoMapper;
using Bookings.Bll.Dto;
using Bookings.Bll.Interfaces;
using Bookings.Dal.Interfaces;
using Bookings.Domain.Exceptions;
using Bookings.Domain.Models;
using Microsoft.Extensions.Logging;

namespace Bookings.Bll.Services;

/// <summary>CRUD клієнтів. Працює з репозиторієм на чистому ADO.NET через Unit of Work.</summary>
public sealed class CustomerService(IUnitOfWork uow, IMapper mapper, ILogger<CustomerService> logger) : ICustomerService
{
    private const string User = "bookings-api";

    public async Task<CustomerDto> GetAsync(long id, CancellationToken ct)
    {
        var customer = await uow.Customers.GetByIdAsync(id, ct)
                       ?? throw new NotFoundException($"Customer {id} not found");
        return mapper.Map<CustomerDto>(customer);
    }

    public async Task<IReadOnlyList<CustomerDto>> SearchAsync(string? name, int page, int pageSize, CancellationToken ct)
    {
        var customers = await uow.Customers.SearchAsync(name, page, pageSize, ct);
        return mapper.Map<IReadOnlyList<CustomerDto>>(customers);
    }

    public async Task<CustomerDto> CreateAsync(CreateCustomerDto input, CancellationToken ct)
    {
        // бізнес-правило зі станом БД: email унікальний (UNIQUE у БД — друга лінія захисту)
        if (await uow.Customers.GetByEmailAsync(input.Email, ct) is not null)
            throw new BusinessConflictException($"Customer with email {input.Email} already exists");

        var customer = mapper.Map<Customer>(input);
        var id = await uow.Customers.AddAsync(customer, User, ct);
        logger.LogInformation("Customer {CustomerId} created", id);   // без персональних даних у лозі
        return await GetAsync(id, ct);
    }

    public async Task<CustomerDto> UpdateAsync(long id, UpdateCustomerDto input, CancellationToken ct)
    {
        var existing = await uow.Customers.GetByIdAsync(id, ct)
                       ?? throw new NotFoundException($"Customer {id} not found");

        var other = await uow.Customers.GetByEmailAsync(input.Email, ct);
        if (other is not null && other.Id != id)
            throw new BusinessConflictException($"Customer with email {input.Email} already exists");

        var customer = mapper.Map(input, existing);
        customer.RowVersion = DecodeRowVersion(input.RowVersion);

        // оптимістична конкурентність: UPDATE ... WHERE RowVersion = @RowVersion
        if (!await uow.Customers.UpdateAsync(customer, User, ct))
            throw new BusinessConflictException(
                $"Customer {id} was modified by another user. Reload it and try again");

        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(long id, CancellationToken ct)
    {
        if (await uow.Customers.GetByIdAsync(id, ct) is null)
            throw new NotFoundException($"Customer {id} not found");
        if (await uow.Customers.HasActiveBookingsAsync(id, ct))
            throw new BusinessConflictException($"Customer {id} has active bookings and cannot be deleted");

        await uow.Customers.SoftDeleteAsync(id, User, ct);
    }

    private static byte[] DecodeRowVersion(string value)
    {
        try
        {
            var bytes = Convert.FromBase64String(value);
            if (bytes.Length == 8) return bytes;
        }
        catch (FormatException) { }
        throw new ValidationException(nameof(UpdateCustomerDto.RowVersion), "RowVersion must be an 8-byte Base64 value");
    }
}
