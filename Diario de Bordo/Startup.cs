using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddControllersWithViews();

        // Adiciona cache em memória para a sessão
        services.AddDistributedMemoryCache();

        // Configura sessão
        services.AddSession(options =>
        {
            options.IdleTimeout = TimeSpan.FromMinutes(30); // tempo de expiração
            options.Cookie.HttpOnly = true;
            options.Cookie.IsEssential = true;
        });
    }

    public void Configure(IApplicationBuilder app)
    {
        app.UseStaticFiles();
        app.UseRouting();

        // Ativa sessão antes de MapControllerRoute
        app.UseSession();

        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllerRoute(
                name: "default",
                pattern: "{controller=Account}/{action=Login}/{id?}");
        });
        app.Use(async (context, next) =>
        {
            await next();

            if (context.Response.StatusCode == 403)
            {
                context.Response.Redirect("/Shared/AccessDenied");
            }
        });

    }
}
