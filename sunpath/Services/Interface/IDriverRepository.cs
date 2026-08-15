using sunpath.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace sunpath.Services.Interface
{
    public interface IDriverRepository
    {
        Task<List<Driver>> GetAllAsync();
        Task<Driver> GetByIdAsync(int id);
        Task<int> CreateAsync(Driver driver);
        Task<bool> UpdateAsync(int id, Driver driver);
        Task<bool> DeleteAsync(int id);
        Task<bool> ExistsByNationalIdAsync(string nationalId, int? excludeId = null);
    }
}