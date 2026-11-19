using Calibration.Api;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IClinicSession, HttpClinicSession>();
builder.Services.AddDbContext<ClinicDb>(o => o.UseSqlite("Data Source=calibration.db"));
builder.Services.AddAuthentication().AddCookie();
builder.Services.AddAuthorization();
var app = builder.Build();
app.MapControllers();
app.Run();
