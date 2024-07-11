using Delivery.Persistence.Data;
using Delivery.Repositories.Implementations;
using Delivery.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(option =>
    {
        option.LoginPath = new PathString("/Usuario/Login");
        option.ExpireTimeSpan = TimeSpan.FromDays(1);
        option.LogoutPath = new PathString("/Usuario/Login");
        option.AccessDeniedPath = new PathString("/Home/Index");
    });

builder.Services.AddDbContext<DeliveryDBContext>(options => 
    options.UseSqlServer(builder.Configuration.GetConnectionString("SQLConnection")));

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddScoped<IUsuarioRepository, UsuarioBase>();
builder.Services.AddScoped<IComidaRepository, ComidaBase>();
builder.Services.AddScoped<IPedidoRepository, PedidoBase>();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsProduction())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();

app.UseAuthorization();

DeliveryDBInitializer.Seed(app);

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
