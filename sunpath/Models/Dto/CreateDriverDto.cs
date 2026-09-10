using System.ComponentModel.DataAnnotations;

namespace sunpath.Models.Dto
{
    public class CreateDriverDto
    {
        [Required] public string FirstName { get; set; }
        [Required] public string LastName { get; set; }
        [Required] public string NationalId { get; set; }
        [Required] public string Phone { get; set; }
        [Range(1, 3)] public int LicenseType { get; set; }

        [Required, StringLength(100)]
        public string Username { get; set; }

        [Required, StringLength(200, MinimumLength = 6)]
        public string Password { get; set; }
    }
}