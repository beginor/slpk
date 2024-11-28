using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Slpk {

    public class SlpkMiddleware {

        private readonly RequestDelegate next;
        private readonly SlpkOptions options;
        private ILogger<SlpkMiddleware> logger;

        public SlpkMiddleware(
            RequestDelegate next,
            SlpkOptions options,
            ILogger<SlpkMiddleware> logger
        ) {
            this.next = next;
            this.options = options;
            this.logger = logger;
        }

        public Task Invoke(HttpContext httpContext) {
            if (httpContext.Request.Path.HasValue) {
                try {
                    return HandleRequest(httpContext);
                }
                catch (Exception ex) {
                    httpContext.Response.StatusCode = 500;
                    logger.LogError(
                        ex,
                        $"Handle {httpContext.Request.Path} error."
                    );
                    return httpContext.Response.WriteAsync(ex.Message);
                }
            }
            return next(httpContext);
        }

        private async Task HandleRequest(HttpContext context) {
            var req = context.Request;
            var reqPath = req.Path.Value;
            logger.LogInformation($"Request path {reqPath}");
            // var filePath = this.pathCache.GetOrAdd(reqPath, FindFilePath);
            var filePath = FindFilePath(reqPath);
            var res = context.Response;
            if (string.IsNullOrEmpty(filePath)) {
                logger.LogWarning($"No file found for request {reqPath}!");
                res.StatusCode = (int)HttpStatusCode.NotFound;
                await res.CompleteAsync();
                return;
            }
            logger.LogInformation($"File path is: {filePath}");
            var fileInfo = new FileInfo(filePath);
            var fileTime = fileInfo.LastWriteTimeUtc.ToFileTime().ToString("H");
            var etag = req.Headers["If-None-Match"].ToString();
            if (fileTime.Equals(etag, StringComparison.Ordinal)) {
                res.StatusCode = StatusCodes.Status304NotModified;
                await res.CompleteAsync();
                return;
            }
            res.StatusCode = StatusCodes.Status200OK;
            res.Headers.ContentLength = fileInfo.Length;
            res.Headers["Cache-Control"] = "no-cache";
            res.Headers["ETag"] = fileTime;
            if (filePath.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)) {
                if (filePath.EndsWith(".json.gz", StringComparison.OrdinalIgnoreCase)) {
                    res.ContentType = "application/json";
                }
                else {
                    res.ContentType = "application/octet-stream";
                }
                res.Headers["Content-Encoding"] = "gzip";
                var content = new byte[fileInfo.Length];
                using var stream = fileInfo.OpenRead();
                await stream.ReadAsync(content, 0, content.Length);
                await res.Body.WriteAsync(content);
            }
            else if (filePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) {
                res.ContentType = "application/json";
                using var stream = fileInfo.OpenText();
                var content = await stream.ReadToEndAsync();
                await res.WriteAsync(content);
            }
            else if (filePath.EndsWith(".bin")) {
                res.ContentType = "application/octet-stream";
                var content = new byte[fileInfo.Length];
                using var stream = fileInfo.OpenRead();
                await stream.ReadAsync(content, 0, content.Length);
                await res.Body.WriteAsync(content);
            }
            await res.CompleteAsync();
        }

        private string FindFilePath(string reqPath) {
            var relPath = reqPath.Substring(1);
            if (string.IsNullOrEmpty(relPath)) {
                return string.Empty;
            }
            if (Path.DirectorySeparatorChar != '/') {
                relPath = relPath.Replace('/', Path.DirectorySeparatorChar);
            }
            var localPath = Path.Combine(options.RootFolder, relPath);
            if (Directory.Exists(localPath)) {
                // try find index file;
                foreach (var indexFile in options.IndexFiles) {
                    var indexFilePath = Path.Combine(localPath, indexFile);
                    if (File.Exists(indexFilePath)) {
                        return indexFilePath;
                    }
                }
            }
            else if (!File.Exists(localPath)) {
                // try find file in extensions.
                foreach (var ext in options.Extensions) {
                    var extFilePath = localPath + ext;
                    if (File.Exists(extFilePath)) {
                        return extFilePath;
                    }
                }
            }
            else if (File.Exists(localPath)) {
                return localPath;
            }
            return string.Empty;
        }

    }

    public class SlpkOptions {
        public string PathBase { get; set; }
        public string RootFolder { get; set; }
        public string[] IndexFiles { get; set; }
        public string[] Extensions { get; set; }
    }

    public static class ServiceCollectionExtensions {

        public static void AddSlpk(
            this IServiceCollection services,
            Action<SlpkOptions> config
        ) {
            var options = new SlpkOptions();
            config(options);
            services.AddSingleton(options);
        }
    }

    public static class ApplicationBuilderExtensions {

        public static void UseSlpk(this IApplicationBuilder app) {
            var options = app.ApplicationServices.GetService<SlpkOptions>();
            var logger = app.ApplicationServices.GetService<ILogger<SlpkMiddleware>>();
            logger.LogInformation(
                $"Slpk: {options.PathBase} => {options.RootFolder}");
            app.Map(options.PathBase, slpkApp => {
                slpkApp.UseMiddleware<SlpkMiddleware>(options);
                slpkApp.UseStaticFiles();
            });
        }

    }

}
