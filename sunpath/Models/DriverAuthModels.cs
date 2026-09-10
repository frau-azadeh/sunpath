namespace sunpath.Models
{
    public class DriverLoginRequest
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }

    public class DriverLoginResponse
    {
        public bool Success { get; set; }
        public int DriverId { get; set; }
        public string FullName { get; set; }
        public string Phone { get; set; }
        public int? CurrentVehicleId { get; set; }
        public string Token { get; set; }
        public string Message { get; set; }
    }
}