using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using SCRM.UI.Components;
using SCRM.UI.Services;
using Radzen;
using Microsoft.AspNetCore.Components.Authorization;
using Blazored.LocalStorage;
using TG.Blazor.IndexedDB;
using SCRM.UI.Services.Data;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<Routes>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var networkSettings = builder.Configuration.GetSection(SCRM.SHARED.Models.NetworkSettings.SectionName).Get<SCRM.SHARED.Models.NetworkSettings>() ?? new SCRM.SHARED.Models.NetworkSettings();
builder.Services.AddScoped<HttpClient>(sp => new HttpClient { BaseAddress = new Uri(networkSettings.ApiBaseUrl) });
builder.Services.AddScoped<IDeviceService, DeviceService>();
builder.Services.AddScoped<IClientTaskService, ClientTaskService>();

// Suppress EF Core Info logs (SQL commands)
builder.Logging.SetMinimumLevel(LogLevel.Warning);

builder.Services.AddScoped<WeChatService>();
builder.Services.AddScoped<CrmStore>();
builder.Services.AddRadzenComponents();
builder.Services.AddAntDesign();

builder.Services.AddBlazoredLocalStorage();
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthStateProvider>();

// builder.Services.AddIndexedDB(dbStore => ... ); -> Commented out to fix TimeGhost error
// We will use In-Memory storage for now (Contacts/Messages are lost on refresh, but app works).
// ... (imports)

// ...
// builder.Services.AddScoped<IClientDbContext, MemoryClientDbContext>();

// Register EF Core SQLite (WASM)
builder.Services.AddDbContext<ClientDbContext>(options => 
    options.UseSqlite("Data Source=client.db"));

builder.Services.AddScoped<IClientDbContext>(sp => sp.GetRequiredService<ClientDbContext>());

await builder.Build().RunAsync();
