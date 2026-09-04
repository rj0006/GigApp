using GigApp.Api.Data;
using GigApp.Api.Dtos;
using GigApp.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GigApp.Api.Services.Banking
{
    public interface IBankAccountService
    {
        Task<BankAccountDto?> GetAsync(int userId, CancellationToken ct = default);
        Task<BankAccountResult> SaveAsync(
            int userId, SaveBankAccountRequest request, CancellationToken ct = default);
    }

    public class BankAccountService : IBankAccountService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<BankAccountService> _logger;

        public BankAccountService(AppDbContext context, ILogger<BankAccountService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<BankAccountDto?> GetAsync(int userId, CancellationToken ct = default)
        {
            var account = await _context.BankAccounts
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.UserId == userId, ct);

            return account is null ? null : BankAccountDto.From(account);
        }

        public async Task<BankAccountResult> SaveAsync(
            int userId, SaveBankAccountRequest request, CancellationToken ct = default)
        {
            var account = await _context.BankAccounts.FirstOrDefaultAsync(a => a.UserId == userId, ct);
            var isNew = account is null;

            if (account is null)
            {
                account = new BankAccount { UserId = userId, CreatedAt = DateTime.UtcNow };
                _context.BankAccounts.Add(account);
            }
            else
            {
                account.UpdatedAt = DateTime.UtcNow;
            }

            account.AccountHolderName = request.AccountHolderName.Trim();
            account.AccountNumber = request.AccountNumber.Trim();
            account.IfscCode = request.IfscCode.Trim().ToUpperInvariant();
            account.BankName = request.BankName.Trim();
            account.BranchName = Blank(request.BranchName);
            account.UpiId = Blank(request.UpiId)?.ToLowerInvariant();

            await _context.SaveChangesAsync(ct);

            _logger.LogInformation(
                "{Action} bank account for user {UserId}", isNew ? "Added" : "Updated", userId);

            return BankAccountResult.Ok(BankAccountDto.From(account));
        }

        private static string? Blank(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public class BankAccountResult
    {
        public bool Succeeded { get; private init; }
        public string? Error { get; private init; }
        public BankAccountDto? Account { get; private init; }

        public static BankAccountResult Ok(BankAccountDto account) =>
            new() { Succeeded = true, Account = account };

        public static BankAccountResult Fail(string error) =>
            new() { Succeeded = false, Error = error };
    }
}
