using Microsoft.AspNetCore.Components.Web;
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

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri("http://localhost:42718") });
builder.Services.AddScoped<IDeviceService, DeviceService>();
builder.Services.AddScoped<IClientTaskService, ClientTaskService>();
builder.Services.AddScoped<IClientTaskService, ClientTaskService>();

builder.Services.AddScoped<WeChatService>();
builder.Services.AddScoped<CrmStore>();
builder.Services.AddRadzenComponents();

builder.Services.AddBlazoredLocalStorage();
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthStateProvider>();

// builder.Services.AddIndexedDB(dbStore => ... ); -> Commented out to fix TimeGhost error
// We will use In-Memory storage for now (Contacts/Messages are lost on refresh, but app works).
builder.Services.AddScoped<IClientDbContext, MemoryClientDbContext>();

await builder.Build().RunAsync();
