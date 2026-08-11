using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using sunpath.Data;
using sunpath.Hubs;      // اضافه شده برای دسترسی به کلاس VehicleHub
using sunpath.Services;  // اضافه شده برای دسترسی به IVehicleService و VehicleService
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

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // ۱. تنظیم CORS برای برقراری ارتباط بدون خطای امنیت مرورگر با فرانت‌انند Next.js
            services.AddCors(options =>
            {
                options.AddPolicy("CorsPolicy", builder => builder
                    .WithOrigins("http://localhost:3000") // آدرس فرانت‌انند
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials()); // حیاتی برای کارکرد SignalR
            });

            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_1);

            // ۲. ثبت سرویس‌های برنامه در DI Container
            services.AddTransient<DbHelper>();
            services.AddScoped<IVehicleService, VehicleService>();

            // ۳. اضافه کردن سرویس SignalR به پروژه
            services.AddSignalR();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
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

            // ۴. فعال‌سازی سیاست CORS قبل از احراز هویت و روتینگ‌ها
            app.UseCors("CorsPolicy");

            app.UseHttpsRedirection();

            // ۵. تعریف مسیرهای هاب SignalR
            app.UseSignalR(routes =>
            {
                routes.MapHub<VehicleHub>("/vehicleHub");
            });

            app.UseMvc();
        }
    }
}
