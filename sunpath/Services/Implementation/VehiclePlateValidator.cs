using System;
using System.Text.RegularExpressions;
namespace sunpath.Services.Implementation
{
    public static class VehiclePlateValidator
    {
        public static string Normalize(string value, int vehicleType)
        {
            if (vehicleType < 0 || vehicleType > 3) return null;
            var text = value ?? "";
            for (var i = 0; i < 10; i++) text = text.Replace("۰۱۲۳۴۵۶۷۸۹"[i], (char)('0' + i)).Replace("٠١٢٣٤٥٦٧٨٩"[i], (char)('0' + i));
            text = Regex.Replace(text.Replace("ایران", "").Replace('ي', 'ی').Replace('ك', 'ک'), @"\s+", "");
            if (vehicleType == 3)
            {
                var motor = Regex.Match(text, @"^([0-9]{3})[-/]?([0-9]{5})$");
                return motor.Success ? motor.Groups[1].Value + "-" + motor.Groups[2].Value : null;
            }
            var car = Regex.Match(text, @"^([0-9]{2})([آ-ی])([0-9]{3})[-/]?([0-9]{2})$");
            return car.Success ? car.Groups[1].Value + car.Groups[2].Value + car.Groups[3].Value + "-" + car.Groups[4].Value : null;
        }
    }
}
