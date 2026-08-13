using sunpath.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace sunpath.Services.Interface
{
    public interface IDriverRepository
    {
        List<Driver> GetAll();
        Driver GetById(int id);
        int Create(Driver driver);
        bool Update(int id, Driver driver);
        bool Delete(int id);
        bool ExistsByNationalId(string nationalId, int? excludeId = null);
    }
}