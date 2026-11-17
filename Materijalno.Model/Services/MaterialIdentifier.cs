using System;
using System.Linq;

namespace Materijalno.Model.Services
{
    public static class MaterialIdentifier
    {
        // Fox IDENT/SIFMAT are character fields of length 9. Zeros are data.
        public static string Normalize(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        public static bool IsValid(string value) => !string.IsNullOrEmpty(value) && value.Length <= 9 &&
            value.All(c => c >= '0' && c <= '9') && value.Any(c => c != '0');
    }
}
