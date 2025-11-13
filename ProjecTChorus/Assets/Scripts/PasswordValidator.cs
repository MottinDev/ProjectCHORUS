using System.Linq;

public static class PasswordValidator
{
    public class ValidationResult
    {
        public bool HasMinLength = false;
        public bool HasUppercase = false;
        public bool HasLowercase = false;
        public bool HasNumber = false;
        public bool HasSpecialChar = false;

        public bool IsValid => HasMinLength && HasUppercase && HasLowercase && HasNumber && HasSpecialChar;
    }

    public static ValidationResult ValidatePassword(string password)
    {
        var result = new ValidationResult();
        if (string.IsNullOrEmpty(password)) return result;

        result.HasMinLength = password.Length >= 8;
        result.HasUppercase = password.Any(char.IsUpper);
        result.HasLowercase = password.Any(char.IsLower);
        result.HasNumber = password.Any(char.IsDigit);
        result.HasSpecialChar = password.Any(c => !char.IsLetterOrDigit(c));

        return result;
    }
}