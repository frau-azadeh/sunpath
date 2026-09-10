
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using sunpath.Data;
using sunpath.Hubs;
using sunpath.Services.Implementation;
using sunpath.Services.Interface;

namespace sunpath
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            // ---------------------------------------------------------
            // CORS
            // ---------------------------------------------------------
            services.AddCors(options =>
            {
                options.AddPolicy("CorsPolicy", builder =>
                {
                    var corsFilePath = Path.Combine(
                        Directory.GetCurrentDirectory(),
                        "wwwroot",
                        "settings",
                        "CorsOrigins.txt");

                    // Origin های پیش‌فرض برای اجرای Frontend در حالت Development
                    string[] allowedOrigins =
                    {
                        "http://localhost:3000",
                        "https://localhost:3000"
                    };

                    // اگر فایل CorsOrigins.txt وجود داشته باشد،
                    // Origin های داخل فایل استفاده می‌شوند.
                    if (File.Exists(corsFilePath))
                    {
                        var fileContent = File.ReadAllLines(corsFilePath)
                            .Where(line => !string.IsNullOrWhiteSpace(line))
                            .Select(line => line.Trim())
                            .Where(line => line != "*")
                            .Distinct()
                            .ToArray();

                        if (fileContent.Length > 0)
                        {
                            allowedOrigins = fileContent;
                        }
                    }

                    builder
                        .WithOrigins(allowedOrigins)
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                });
            });

            // ---------------------------------------------------------
            // MVC - ASP.NET Core 2.1
            // ---------------------------------------------------------
            services.AddMvc()
                .SetCompatibilityVersion(
                    CompatibilityVersion.Version_2_1);

            // ---------------------------------------------------------
            // Database
            // ---------------------------------------------------------
            services.AddTransient<DbHelper>();

            // ---------------------------------------------------------
            // Vehicle Services
            // ---------------------------------------------------------
            services.AddScoped<IVehicleService, VehicleService>();

            // ---------------------------------------------------------
            // Dispatch Services
            // ---------------------------------------------------------
            services.AddScoped<IDispatchService, DispatchService>();

            // ---------------------------------------------------------
            // Driver Repository
            // ---------------------------------------------------------
            services.AddScoped<IDriverRepository, DriverRepository>();

            // ---------------------------------------------------------
            // SignalR
            // ---------------------------------------------------------
            services.AddSignalR();
        }

        public void Configure(
            IApplicationBuilder app,
            IHostingEnvironment env)
        {
            // ---------------------------------------------------------
            // Environment
            // ---------------------------------------------------------
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseHsts();
            }

            // ---------------------------------------------------------
            // HTTPS
            // ---------------------------------------------------------
            app.UseHttpsRedirection();

            // ---------------------------------------------------------
            // CORS
            // باید قبل از MVC و قبل از Endpoint ها اجرا شود.
            // ---------------------------------------------------------
            app.UseCors("CorsPolicy");

            // ---------------------------------------------------------
            // SignalR
            // ---------------------------------------------------------
            app.UseSignalR(routes =>
            {
                routes.MapHub<VehicleHub>("/vehicleHub");
                routes.MapHub<DriverHub>("/driverHub");
            });

            // ---------------------------------------------------------
            // MVC
            // ---------------------------------------------------------
            app.UseMvc();
        }
    }
}

