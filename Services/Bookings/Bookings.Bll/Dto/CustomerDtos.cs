using System.ComponentModel.DataAnnotations;

namespace Bookings.Bll.Dto;

// DTO — лише дані API-контракту, без логіки.
// DataAnnotations — перший рівень валідації: [ApiController] відхиляє некоректний запит (400)
// ще ДО виклику сервісу. Правила, яким потрібен стан БД, перевіряє BLL.

/// <summary>Клієнт (вихід).</summary>
public sealed class CustomerDto
{
    public long Id { get; init; }
    public string Email { get; init; } = "";
    public string FullName { get; init; } = "";
    public string? Phone { get; init; }
    public DateTime CreatedAt { get; init; }
    /// <summary>rowversion у Base64 — передайте його в PUT для оптимістичної конкурентності.</summary>
    public string RowVersion { get; init; } = "";
}

/// <summary>Створення клієнта (вхід).</summary>
public sealed class CreateCustomerDto
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = "";

    [Required, StringLength(200, MinimumLength = 2)]
    public string FullName { get; set; } = "";

    [Phone, MaxLength(20)]
    public string? Phone { get; set; }
}

/// <summary>Оновлення клієнта (вхід) — з RowVersion для перевірки конкурентних змін.</summary>
public sealed class UpdateCustomerDto
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = "";

    [Required, StringLength(200, MinimumLength = 2)]
    public string FullName { get; set; } = "";

    [Phone, MaxLength(20)]
    public string? Phone { get; set; }

    [Required]
    public string RowVersion { get; set; } = "";
}
