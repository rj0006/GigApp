namespace GigApp.Api.Models
{
    public class BankAccount
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public User? User { get; set; }

        public string AccountHolderName { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
        public string IfscCode { get; set; } = string.Empty;
        public string BankName { get; set; } = string.Empty;
        public string? BranchName { get; set; }
        public string? UpiId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public string MaskedAccountNumber =>
            AccountNumber.Length <= 4
                ? AccountNumber
                : new string('X', AccountNumber.Length - 4) + AccountNumber[^4..];
    }
}
