using System;

namespace sunpath.Models
{
    public class Driver
    {
        public int Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string NationalId { get; set; }
        public string Phone { get; set; }
        public int LicenseType { get; set; }
        public DateTime CreatedAt { get; set; }

        // اطلاعات ورود؛ هرگز PasswordHash را به API لیست رانندگان برنگردان.
        public string Username { get; set; }
        public string PasswordHash { get; set; }
        public bool IsActive { get; set; } = true;
    }
}