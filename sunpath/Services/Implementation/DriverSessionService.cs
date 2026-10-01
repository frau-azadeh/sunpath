using System;
using System.Globalization;
using Microsoft.AspNetCore.DataProtection;
namespace sunpath.Services.Implementation
{
    public class DriverSessionService
    {
        private readonly IDataProtector _protector;
        public DriverSessionService(IDataProtectionProvider provider)
        { _protector = provider.CreateProtector("SunPath.DriverSession.v1"); }
        public string Issue(int driverId) => _protector.Protect(driverId.ToString(CultureInfo.InvariantCulture) + ":" + DateTimeOffset.UtcNow.AddHours(12).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));
        public int? Validate(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return null;
            try
            {
                var parts = _protector.Unprotect(token).Split(':');
                int id; long expires;
                if (parts.Length != 2 || !int.TryParse(parts[0], out id) || id <= 0 || !long.TryParse(parts[1], out expires) || expires <= DateTimeOffset.UtcNow.ToUnixTimeSeconds()) return null;
                return id;
            }
            catch (System.Security.Cryptography.CryptographicException) { return null; }
            catch (FormatException) { return null; }
        }
        public int? FromAuthorization(string header) => header != null && header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? Validate(header.Substring(7).Trim()) : null;
    }
}
