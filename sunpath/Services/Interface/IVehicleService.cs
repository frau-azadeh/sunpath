using sunpath.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace sunpath.Services.Interface
{
    public interface IVehicleService
    {
        Task<List<Vehicle>> GetAllVehiclesAsync();
        Task<Vehicle> GetByIdAsync(int id);
        Task<int> CreateAsync(Vehicle vehicle);
        Task<bool> UpdateAsync(int id, Vehicle vehicle);
        Task<bool> DeleteAsync(int id);

        Task<bool> UpdateVehicleStatusAsync(
            int id,
            double? latitude,
            double? longitude,
            double speed,
            double heading);

        Task<bool> ExistsByPlateNumberAsync(string plateNumber, int? excludeId = null);
    }
}
