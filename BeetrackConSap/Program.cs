using Dapper;
using Microsoft.AspNetCore.Authentication.Cookies;
using MorosidadWeb.Data;
using System.Data;

var builder = WebApplication.CreateBuilder(args);

//string connectionFemaco = builder.Configuration.GetConnectionString("FemacoConnection");
//builder.Services.AddDbContext<ApplicationFemacoContext>(options => options.UseSqlServer(connectionFemaco));
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(c => c.LoginPath = "/UserFemaco/Login");
builder.Services.AddSingleton<DapperContext>();
builder.Services.AddControllersWithViews();
builder.Services
    .AddMvc(options => {
        options.InputFormatters.Insert(0, new RawJsonBodyInputFormatter());
    });
var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment()) {
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
if (!app.Environment.IsDevelopment()) // Solo en producción
{
    app.UseHsts(); // HSTS para aplicaciones de producción (HTTP Strict Transport Security)
}
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

SqlMapper.AddTypeHandler(new DecimalTypeHandler());
app.Run();

internal class SqlDateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly> {
    public override void SetValue(IDbDataParameter parameter, DateOnly date) => parameter.Value = date.ToDateTime(new TimeOnly(0, 0));
    public override DateOnly Parse(object value) => DateOnly.FromDateTime((DateTime)value);
}

internal class SqlTimeOnlyTypeHandler : SqlMapper.TypeHandler<TimeOnly> {
    public override void SetValue(IDbDataParameter parameter, TimeOnly time) => parameter.Value = time.ToString();
    public override TimeOnly Parse(object value) => TimeOnly.FromTimeSpan((TimeSpan)value);
}

internal class DecimalTypeHandler : SqlMapper.TypeHandler<decimal> {
    public override void SetValue(IDbDataParameter parameter, decimal value) => parameter.Value = value;
    public override decimal Parse(object value) => Convert.ToDecimal(value);
}