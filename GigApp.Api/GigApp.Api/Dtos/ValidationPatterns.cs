namespace GigApp.Api.Dtos
{
    public static class ValidationPatterns
    {
        /// <summary>Indian mobile number: 10 digits starting 6-9.</summary>
        public const string Phone = @"^[6-9]\d{9}$";
        public const string PhoneMessage = "Phone must be a 10-digit Indian mobile number.";

        /// <summary>Indian pincode: 6 digits, first digit 1-9.</summary>
        public const string Pincode = @"^[1-9]\d{5}$";
        public const string PincodeMessage = "Pincode must be 6 digits.";

        /// <summary>Aadhaar: exactly 12 digits, first digit 2-9.</summary>
        public const string Aadhaar = @"^[2-9]\d{11}$";
        public const string AadhaarMessage = "Aadhaar number must be 12 digits.";

        /// <summary>At least one lowercase, uppercase, digit and symbol; 8+ characters.</summary>
        public const string Password = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^\w\s]).{8,}$";
        public const string PasswordMessage =
            "Password must be at least 8 characters and include an uppercase letter, a lowercase letter, a digit and a symbol.";

        public const string Ifsc = @"^[A-Z]{4}0[A-Z0-9]{6}$";
        public const string IfscMessage = "IFSC must be 11 characters, for example HDFC0001234.";

        public const string BankAccountNumber = @"^\d{9,18}$";
        public const string BankAccountNumberMessage = "Account number must be 9 to 18 digits.";

        public const string Upi = @"^[\w.\-]{2,}@[a-zA-Z]{2,}$";
        public const string UpiMessage = "UPI ID looks like name@bank.";
    }
}
