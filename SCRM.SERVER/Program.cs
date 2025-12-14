using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using SCRM.UI.Services;
using SCRM.UI.Services.Data;
using Radzen;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using SCRM.UI.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// Copy services from SCRM.WEB
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri("http://localhost:42718") });
builder.Services.AddScoped<IDeviceService, DeviceService>();
builder.Services.AddScoped<IClientTaskService, ClientTaskService>();

builder.Services.AddScoped<WeChatService>();
builder.Services.AddScoped<CrmStore>();
builder.Services.AddRadzenComponents();

builder.Services.AddBlazoredLocalStorage();
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthStateProvider>();

// Register MemoryClientDbContext instead of IndexedDB
builder.Services.AddScoped<IClientDbContext, MemoryClientDbContext>();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// app.UseHttpsRedirection(); // Disable for local dev if API is HTTP

app.UseStaticFiles();

app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
