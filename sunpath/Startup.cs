using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using sunpath.Data;
using sunpath.Hubs;
using sunpath.Services;
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
            services.AddCors(options =>
            {
                options.AddPolicy("CorsPolicy", builder =>
                {
                    var corsFilePath = Path.Combine(
                        Directory.GetCurrentDirectory(),
                        "wwwroot",
                        "settings",
                        "CorsOrigins.txt");

                    string[] allowedOrigins = { "http://localhost:3000" };

                    if (File.Exists(corsFilePath))
                    {
                        var fileContent = File.ReadAllLines(corsFilePath)
                            .Where(line => !string.IsNullOrWhiteSpace(line))
                            .Select(line => line.Trim())
                            .ToArray();

                        if (fileContent.Length > 0)
                        {
                            allowedOrigins = fileContent;
                        }
                    }

                    builder.WithOrigins(allowedOrigins)
                           .AllowAnyMethod()
                           .AllowAnyHeader()
                           .AllowCredentials();
                });
            });

            services.AddMvc()
                .SetCompatibilityVersion(CompatibilityVersion.Version_2_1);

            services.AddTransient<DbHelper>();

            services.AddScoped<IVehicleService, VehicleService>();

            services.AddScoped<IDriverRepository, DriverRepository>();

            services.AddSingleton<VehicleSimulationService>();
            services.AddSingleton<Microsoft.Extensions.Hosting.IHostedService>(
                provider => provider.GetRequiredService<VehicleSimulationService>());

            services.AddSignalR();
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseHsts();
            }

            app.UseCors("CorsPolicy");

            app.UseHttpsRedirection();

            app.UseSignalR(routes =>
            {
                routes.MapHub<VehicleHub>("/vehicleHub");
            });

            app.UseMvc();
        }
    }
}
