using GoogleCloudSamples.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Microsoft.Data.Entity;

namespace GoogleCloudSamples
{
    public class Startup
    {
        private ILoggerFactory _loggerFactory;

        public Startup(IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();
            Configuration = builder.Build();
        }

        public IConfigurationRoot Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            var logger = _loggerFactory.CreateLogger("ConfigureServices");
            services.AddAntiforgery();
            // Choose a a backend to store the books.
            switch (Configuration["Data:BookStore"]?.ToLower())
            {
                case "sqlserver":
                    AddSqlServer(services);
                    logger.LogInformation("Storing book data in SQL Server.");
                    break;

                case "datastore":
                    AddDatastore(services);
                    logger.LogInformation("Storing book data in Datastore.");
                    break;

                default:
                    Halt("No bookstore backend selected.\n" +
                        "Set the configuration variable Data:BookStore to " +
                        "one of the following: sqlserver, postgres, datastore.");
                    break;
            }

            // Add framework services.
            services.AddMvc();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole(Configuration.GetSection("Logging"));
            loggerFactory.AddDebug();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseBrowserLink();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }

            app.UseStaticFiles();

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
                routes.MapRoute(
                    name: "health",
                    template: "_ah/health",
                    defaults: new { controller = "Home", action = "Health" });
            });
        }
        public void Halt(string message)
        {
            var logger = _loggerFactory.CreateLogger("Halt");
            logger.LogCritical(message);
            if (Debugger.IsAttached)
                Debugger.Break();
            Environment.Exit(-1);
        }

        private void AddDatastore(IServiceCollection services)
        {
            string projectId = Configuration["GOOGLE_PROJECT_ID"];
            if (string.IsNullOrWhiteSpace(projectId))
                Halt("Set the configuration variable GOOGLE_PROJECT_ID.");
            services.Add(new ServiceDescriptor(typeof(IBookStore),
                (x) => new DatastoreBookStore(projectId),
                ServiceLifetime.Singleton));
        }

        private void AddSqlServer(IServiceCollection services)
        {
            var entityFramework = services.AddEntityFramework();
            string sqlserverConnectionString =
                Configuration["Data:SqlServer:ConnectionString"];
            if (string.IsNullOrWhiteSpace(sqlserverConnectionString))
                Halt("Set the configuration variable Data:SqlServer:ConnectionString.");
            entityFramework.AddSqlServer()
                .AddDbContext<ApplicationDbContext>(options =>
                    options.UseSqlServer(sqlserverConnectionString));
            services.AddScoped(typeof(IBookStore), typeof(DbBookStore));
        }
    }
}
