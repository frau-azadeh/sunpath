using sunpath.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace sunpath.Services.Interface
{
    public interface IVehicleService
    {
        Task<IEnumerable<Vehicle>>GetAllVehiclesAsync();//گرفتن لیست تمام خودرو ها برای لود اولیه در نقشه
        Task UpdateVehicleStatusAsync(int id, double lat, double lng, double speed, double heading);

    }
}
