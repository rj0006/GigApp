namespace GigApp.Api.Dtos
{
    public static class ValidationPatterns
    {
        /// <summary>Indian mobile number: 10 digits starting 6-9.</summary>
        public const string Phone = @"^[6-9]\d{9}$";
        public const string PhoneMessage = "Phone must be a 10-digit Indian mobile number.";

        /// <summary>Aadhaar: exactly 12 digits, first digit 2-9.</summary>
        public const string Aadhaar = @"^[2-9]\d{11}$";
        public const string AadhaarMessage = "Aadhaar number must be 12 digits.";

        /// <summary>At least one lowercase, uppercase, digit and symbol; 8+ characters.</summary>
        public const string Password = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^\w\s]).{8,}$";
        public const string PasswordMessage =
            "Password must be at least 8 characters and include an uppercase letter, a lowercase letter, a digit and a symbol.";
    }
}
