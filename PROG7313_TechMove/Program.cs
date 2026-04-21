/*using Microsoft.EntityFrameworkCore;
using PROG7313_TechMove.Data;
using PROG7313_TechMove.Patterns.Factory;
using PROG7313_TechMove.Patterns.Observer;
using PROG7313_TechMove.Patterns.Repository;
using PROG7313_TechMove.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
*/
using Microsoft.EntityFrameworkCore;
using PROG7313_TechMove.Data;
using PROG7313_TechMove.Patterns.Factory;
using PROG7313_TechMove.Patterns.Observer;
using PROG7313_TechMove.Patterns.Repository;
using PROG7313_TechMove.Services;


var builder = WebApplication.CreateBuilder(args);

// Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Repositories (Repository Pattern)
builder.Services.AddScoped<IContractRepository, ContractRepository>();
builder.Services.AddScoped<IClientRepository, ClientRepository>();
builder.Services.AddScoped<IServiceRequestRepository, ServiceRequestRepository>();

// Factories (Factory Pattern) 
builder.Services.AddSingleton<IContractFactory, ContractFactory>();

// Observers (Observer Pattern) 
builder.Services.AddScoped<IContractObserver, AuditLogObserver>();

// Business Logic Services 
builder.Services.AddScoped<IContractService, ContractService>();
builder.Services.AddScoped<IServiceRequestService, ServiceRequestService>();
builder.Services.AddScoped<IFileService, FileService>();

// HTTP Client for Currency API 
builder.Services.AddHttpClient<ICurrencyService, CurrencyService>(client =>
{
client.Timeout = TimeSpan.FromSeconds(10);
});

//  MVC 
builder.Services.AddControllersWithViews();

var app = builder.Build();

//  Middleware pipeline 
if (!app.Environment.IsDevelopment())
{
app.UseExceptionHandler("/Home/Error");
app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Auto-migrate on startup (dev convenience) 
using var scope = app.Services.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
db.Database.Migrate();

app.Run();

// Required for integration test WebApplicationFactory
public partial class Program { }