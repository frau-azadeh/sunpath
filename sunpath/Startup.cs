using System;
using System.Collections.Generic;
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
        private const string CorsPolicyName = "CorsPolicy";

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
                options.AddPolicy(CorsPolicyName, builder =>
                {
                    var allowedOrigins = new List<string>
                    {
                        "http://localhost:3000",
                        "https://localhost:3000"
                    };

                    var corsFilePath = Path.Combine(
                        Directory.GetCurrentDirectory(),
                        "wwwroot",
                        "settings",
                        "CorsOrigins.txt");

                    if (File.Exists(corsFilePath))
                    {
                        var fileOrigins = File
                            .ReadAllLines(corsFilePath)
                            .Select(line => line.Trim())
                            .Where(line =>
                                !string.IsNullOrWhiteSpace(line) &&
                                line != "*")
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToArray();

                        foreach (var origin in fileOrigins)
                        {
                            if (!allowedOrigins.Contains(
                                origin,
                                StringComparer.OrdinalIgnoreCase))
                            {
                                allowedOrigins.Add(origin);
                            }
                        }
                    }

                    builder
                        .WithOrigins(allowedOrigins.ToArray())
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                });
            });

            // ---------------------------------------------------------
            // MVC - ASP.NET Core 2.1
            // ---------------------------------------------------------
            services
                .AddMvc()
                .SetCompatibilityVersion(
                    CompatibilityVersion.Version_2_1);

            // ---------------------------------------------------------
            // Database
            // ---------------------------------------------------------
            services.AddTransient<DbHelper>();

            // ---------------------------------------------------------
            // Vehicle Services
            // ---------------------------------------------------------
            services.AddScoped<
                IVehicleService,
                VehicleService>();

            // ---------------------------------------------------------
            // Dispatch Services
            // ---------------------------------------------------------
            services.AddScoped<
                IDispatchService,
                DispatchService>();

            // ---------------------------------------------------------
            // Driver Repository
            // ---------------------------------------------------------
            services.AddScoped<
                IDriverRepository,
                DriverRepository>();

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
            //
            // ASP.NET Core 2.1:
            // باید قبل از SignalR و MVC باشد.
            // ---------------------------------------------------------
            app.UseCors(CorsPolicyName);

            // ---------------------------------------------------------
            // SignalR
            // ---------------------------------------------------------
            app.UseSignalR(routes =>
            {
                routes.MapHub<VehicleHub>(
                    "/vehicleHub");

                routes.MapHub<DriverHub>(
                    "/driverHub");
            });

            // ---------------------------------------------------------
            // MVC
            // ---------------------------------------------------------
            app.UseMvc();
        }
    }
}