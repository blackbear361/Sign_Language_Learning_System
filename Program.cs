using Sign_Language_Learning_System.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages(options =>
    options.Conventions.ConfigureFilter(
        new Microsoft.AspNetCore.Mvc.ServiceFilterAttribute(typeof(SessionAuthorizationFilter))));
builder.Services.AddScoped<SessionAuthorizationFilter>();
builder.Services.AddSingleton<MySqlConnectionFactory>();

// Enable session storage for login.
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

await AdminAccountSeeder.SeedAsync(app.Services.GetRequiredService<MySqlConnectionFactory>(), app.Configuration);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.MapRazorPages();

app.Run();
