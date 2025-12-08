using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SCRM.UI.Components;
using SCRM.UI.Services;
using Radzen;
using Microsoft.AspNetCore.Components.Authorization;
using Blazored.LocalStorage;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<Routes>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri("http://localhost:63727") });
builder.Services.AddScoped<IDeviceService, DeviceService>();
builder.Services.AddScoped<IClientTaskService, ClientTaskService>();
builder.Services.AddScoped<IClientTaskService, ClientTaskService>();

builder.Services.AddScoped<WeChatService>();
builder.Services.AddScoped<CrmStore>();
builder.Services.AddRadzenComponents();

builder.Services.AddBlazoredLocalStorage();
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthStateProvider>();

await builder.Build().RunAsync();
