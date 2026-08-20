using sunpath.Models;
using sunpath.Models.Dto;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace sunpath.Services.Interface
{
    public interface IDispatchService
    {
        Task<int> CreateAsync(CreateDispatchRequest request);

        Task<Dispatch> GetByIdAsync(int id);

        Task<List<Dispatch>> GetAllAsync();

        Task<bool> UpdateStatusAsync(
            int id,
            UpdateDispatchStatusRequest request);

        Task<bool> UpdateVehicleLocationAsync(
            UpdateVehicleLocationRequest request);

        Task<bool> DeleteAsync(int id);
    }
}
