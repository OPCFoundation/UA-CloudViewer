using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Opc.Ua.Configuration;
using System;
using System.IO;

namespace UANodesetWebViewer
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;

            if (!Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), "NodeSets")))
            {
                Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "NodeSets"));
            }

            if (!Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), "JSON")))
            {
                Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "JSON"));
            }
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSession(options => {
                options.IdleTimeout = TimeSpan.FromMinutes(5);
            });

            services.AddControllersWithViews();
            services.AddSignalR();

            services.AddRazorPages();
            services.AddServerSideBlazor();

            services.AddSingleton<OpcSessionHelper>();
            services.AddSingleton<ApplicationInstance>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Browser/Error");

                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            app.UseStaticFiles();

            app.UseSession();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Browser}/{action=Index}/{id?}");
                endpoints.MapBlazorHub();
            });
        }
    }
}
