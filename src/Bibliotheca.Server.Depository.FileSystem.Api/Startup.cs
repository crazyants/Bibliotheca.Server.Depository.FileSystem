using Bibliotheca.Server.Depository.FileSystem.Core.Parameters;
using Bibliotheca.Server.Depository.FileSystem.Core.Services;
using Bibliotheca.Server.Depository.FileSystem.Core.Validators;
using Bibliotheca.Server.ServiceDiscovery.ServiceClient;
using Bibliotheca.Server.Mvc.Middleware.Authorization;
using Bibliotheca.Server.Mvc.Middleware.Diagnostics.Exceptions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Versioning;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Swashbuckle.Swagger.Model;
using System;
using System.Collections.Generic;

namespace Bibliotheca.Server.Depository.FileSystem.Api
{
    public class Startup
    {
        public IConfigurationRoot Configuration { get; }

        protected bool UseServiceDiscovery { get; set; } = true;

        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();
            Configuration = builder.Build();
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<ApplicationParameters>(Configuration);

            services.AddCors(options =>
            {
                options.AddPolicy("AllowAllOrigins", builder =>
                {
                    builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
                });
            });

            services.AddMvc(config =>
            {
                var policy = new AuthorizationPolicyBuilder()
                    .AddAuthenticationSchemes(SecureTokenDefaults.AuthenticationScheme)
                    .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                    .RequireAuthenticatedUser()
                    .Build();
            }).AddJsonOptions(options =>
            {
                options.SerializerSettings.DateTimeZoneHandling = Newtonsoft.Json.DateTimeZoneHandling.Utc;
            });

            services.AddApiVersioning(options =>
            {
                options.AssumeDefaultVersionWhenUnspecified = true;
                options.DefaultApiVersion = new ApiVersion(1, 0);
                options.ReportApiVersions = true;
                options.ApiVersionReader = new QueryStringOrHeaderApiVersionReader("api-version");
            });

            services.AddSwaggerGen();
            services.ConfigureSwaggerGen(options =>
            {
                options.SingleApiVersion(new Info
                {
                    Version = "v1",
                    Title = "File system depository API",
                    Description = "Microservice for file system depository feature for Bibliotheca.",
                    TermsOfService = "None"
                });
            });

            services.AddScoped<IFileSystemService, FileSystemService>();
            services.AddScoped<ICommonValidator, CommonValidator>();
            services.AddScoped<IProjectsService, ProjectsService>();
            services.AddScoped<IBranchesService, BranchesService>();
            services.AddScoped<IDocumentsService, DocumentsService>();
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            if (UseServiceDiscovery)
            {
                RegisterClient();
            }

            loggerFactory.AddConsole(Configuration.GetSection("Logging"));
            loggerFactory.AddDebug();

            app.UseExceptionHandler();

            app.UseCors("AllowAllOrigins");

            var secureTokenOptions = new SecureTokenOptions
            {
                SecureToken = Configuration["SecureToken"],
                AuthenticationScheme = SecureTokenDefaults.AuthenticationScheme,
                Realm = SecureTokenDefaults.Realm
            };
            app.UseSecureTokenAuthentication(secureTokenOptions);

            var jwtBearerOptions = new JwtBearerOptions
            {
                Authority = Configuration["OAuthAuthority"],
                Audience = Configuration["OAuthAudience"],
                AutomaticAuthenticate = true,
                AutomaticChallenge = true
            };
            app.UseBearerAuthentication(jwtBearerOptions);

            app.UseMvc();

            app.UseSwagger();
            app.UseSwaggerUi();
        }

        private void RegisterClient()
        {
            var serviceDiscoveryConfiguration = Configuration.GetSection("ServiceDiscovery");

            var tags = new List<string>();
            var tagsSection = serviceDiscoveryConfiguration.GetSection("ServiceTags");
            tagsSection.Bind(tags);

            var serviceDiscovery = new ServiceDiscoveryClient();
            serviceDiscovery.Register((options) =>
            {
                options.ServiceOptions.Id = serviceDiscoveryConfiguration["ServiceId"];
                options.ServiceOptions.Name = serviceDiscoveryConfiguration["ServiceName"];
                options.ServiceOptions.Address = serviceDiscoveryConfiguration["ServiceAddress"];
                options.ServiceOptions.Port = Convert.ToInt32(serviceDiscoveryConfiguration["ServicePort"]);
                options.ServiceOptions.HttpHealthCheck = serviceDiscoveryConfiguration["ServiceHttpHealthCheck"];
                options.ServiceOptions.Tags = tags;
                options.ServerOptions.Address = serviceDiscoveryConfiguration["ServerAddress"];
            });
        }
    }
}
