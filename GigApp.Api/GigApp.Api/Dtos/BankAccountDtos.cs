using System.ComponentModel.DataAnnotations;
using GigApp.Api.Models;

namespace GigApp.Api.Dtos
{
    public class BankAccountDto
    {
        public int Id { get; set; }
        public string AccountHolderName { get; set; } = string.Empty;
        public string MaskedAccountNumber { get; set; } = string.Empty;
        public string IfscCode { get; set; } = string.Empty;
        public string BankName { get; set; } = string.Empty;
        public string? BranchName { get; set; }
        public string? UpiId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public static BankAccountDto From(BankAccount account) => new()
        {
            Id = account.Id,
            AccountHolderName = account.AccountHolderName,
            MaskedAccountNumber = account.MaskedAccountNumber,
            IfscCode = account.IfscCode,
            BankName = account.BankName,
            BranchName = account.BranchName,
            UpiId = account.UpiId,
            CreatedAt = account.CreatedAt,
            UpdatedAt = account.UpdatedAt,
        };
    }

    public class SaveBankAccountRequest
    {
        [Required, StringLength(100, MinimumLength = 2)]
        [Display(Name = "Account holder name")]
        public string AccountHolderName { get; set; } = string.Empty;

        [Required]
        [RegularExpression(ValidationPatterns.BankAccountNumber,
            ErrorMessage = ValidationPatterns.BankAccountNumberMessage)]
        [Display(Name = "Account number")]
        public string AccountNumber { get; set; } = string.Empty;

        [Required]
        [RegularExpression(ValidationPatterns.Ifsc, ErrorMessage = ValidationPatterns.IfscMessage)]
        [Display(Name = "IFSC code")]
        public string IfscCode { get; set; } = string.Empty;

        [Required, StringLength(100, MinimumLength = 2)]
        [Display(Name = "Bank name")]
        public string BankName { get; set; } = string.Empty;

        [StringLength(100)]
        [Display(Name = "Branch")]
        public string? BranchName { get; set; }

        [RegularExpression(ValidationPatterns.Upi, ErrorMessage = ValidationPatterns.UpiMessage)]
        [Display(Name = "UPI ID")]
        public string? UpiId { get; set; }
    }
}
