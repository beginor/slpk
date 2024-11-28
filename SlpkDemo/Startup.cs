using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Slpk {

    public class Startup {

        public Startup(IConfiguration configuration) {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services) {
            services.AddCors(options => {
                var corsPolicy = Configuration.GetSection("cors")
                    .Get<CorsPolicy>();
                options.AddDefaultPolicy(corsPolicy);
            });
            services.AddSlpk(options => {
                Configuration.GetSection("slpk").Bind(options);
            });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env) {
            app.UseCors();
            app.UseSlpk();
            app.UseStaticFiles();
        }
    }
}
