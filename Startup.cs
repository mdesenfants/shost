using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Authentication.Certificate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.SpaServices.ReactDevelopmentServer;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SecureHost.DataProviders;
using System;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace SecureHost
{
    public class Startup
    {
        public Startup(IConfiguration configuration, IWebHostEnvironment env)
        {
            Configuration = configuration;
            Environment = env;
        }

        public IConfiguration Configuration { get; }
        public IWebHostEnvironment Environment { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            if (Environment.IsDevelopment())
            {
                services.AddControllersWithViews(x => x.Filters.Add(new AllowAnonymousFilter()));
            }
            else
            {
                services.AddControllersWithViews(x =>
                {
                    var policy = new AuthorizationPolicyBuilder()
                        .RequireAuthenticatedUser()
                        .Build();
                    x.Filters.Add(new AuthorizeFilter(policy));
                });
            }

            services.AddResponseCaching();

            services.AddApplicationInsightsTelemetry();

            services.AddCertificateForwarding(options =>
            {
                options.CertificateHeader = "X-ARR-ClientCert";
                options.HeaderConverter = (headerValue) =>
                {
                    X509Certificate2 clientCertificate = null;

                    if (!string.IsNullOrWhiteSpace(headerValue))
                    {
                        byte[] bytes = Convert.FromBase64String(headerValue);
                        clientCertificate = new X509Certificate2(bytes);
                    }

                    return clientCertificate;
                };
            });

            services.AddAuthentication(CertificateAuthenticationDefaults.AuthenticationScheme)
                .AddCertificate(options =>
                {
                    options.AllowedCertificateTypes = CertificateTypes.SelfSigned;

                    options.Events = new CertificateAuthenticationEvents()
                    {
                        OnCertificateValidated = context =>
                        {
                            bool match = context.ClientCertificate
                                .Thumbprint.Trim()
                                .Equals(Configuration["ClientThumbprint"].Trim(), StringComparison.OrdinalIgnoreCase);

                            if (!match)
                            {
                                context.Fail("Thumprint mismatch");
                            }

                            var claims = new[]
                            {
                                new Claim(
                                    ClaimTypes.NameIdentifier,
                                    context.ClientCertificate.Subject,
                                    ClaimValueTypes.String,
                                    context.Options.ClaimsIssuer),
                                new Claim(
                                    ClaimTypes.Name,
                                    context.ClientCertificate.Subject,
                                    ClaimValueTypes.String,
                                    context.Options.ClaimsIssuer)
                            };

                            context.Principal = new ClaimsPrincipal(
                                new ClaimsIdentity(claims, context.Scheme.Name));
                            context.Success();

                            return Task.CompletedTask;
                        },
                    };
                });

            services.AddAuthorization();

            // Get a credential and create a client object for the blob container.
            string containerEndpoint = $"https://{Configuration["AzureStorageName"]}.blob.core.windows.net/files";
            var container = new BlobContainerClient(new Uri(containerEndpoint), new DefaultAzureCredential());
            container.CreateIfNotExists();

            services.AddSingleton(container);

            // Add a key vault client to each request
            services.AddScoped((provider) =>
                new KeyVaultClient(
                    new KeyVaultClient.AuthenticationCallback(
                        new AzureServiceTokenProvider().KeyVaultTokenCallback)));

            services.AddScoped<EncryptedBlobProvider>();

            // In production, the React files will be served from this directory
            services.AddSpaStaticFiles(configuration =>
            {
                configuration.RootPath = "ClientApp/build";
            });
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
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseResponseCaching();

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseSpaStaticFiles();

            app.UseRouting();

            app.UseCertificateForwarding();
            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller}/{action=Index}/{id?}");
            });

            app.UseSpa(spa =>
            {
                spa.Options.SourcePath = "ClientApp";

                if (env.IsDevelopment())
                {
                    spa.UseReactDevelopmentServer(npmScript: "start");
                }
            });
        }
    }
}
