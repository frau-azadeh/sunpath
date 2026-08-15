using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace sunpath.Models.Dto
{
    public class UpdateDriverDto
    {
        public string FirstName { get; set; }

        public string LastName { get; set; }

        public string NationalId { get; set; }

        public string Phone { get; set; }

        public int LicenseType { get; set; }
    }
}