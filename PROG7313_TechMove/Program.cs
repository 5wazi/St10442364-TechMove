using PROG7313_TechMove.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Session ───────────────────────────────────────────────────────────────────
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout        = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly    = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite    = Microsoft.AspNetCore.Http.SameSiteMode.Strict;
    options.Cookie.Name        = ".TechMove.Session";
});

builder.Services.AddHttpContextAccessor();

builder.Services.AddHttpClient<ApiClient>(client =>
{
    var apiBase = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5100";
    client.BaseAddress = new Uri(apiBase.TrimEnd('/') + "/");
    client.Timeout     = TimeSpan.FromSeconds(30);
});

builder.Services.AddControllersWithViews();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();        
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();