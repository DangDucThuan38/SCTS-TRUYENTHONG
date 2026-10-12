using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()   // Cho phép tất cả các nguồn (IP/Domain)
              .AllowAnyMethod()   // Cho phép tất cả các phương thức (GET, POST,...)
              .AllowAnyHeader();  // Cho phép tất cả các Header
    });
});
// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseStaticFiles(new StaticFileOptions
{
    //FileProvider = new PhysicalFileProvider(@"C:\Contract\SLIDE_SHOW_SCTS\SLIDE_SHOW_SCTS\DATA"),
    FileProvider = new PhysicalFileProvider(@"E:\TRUYENTHONG\SLIDE_SHOW_SCTS\SLIDE_SHOW_SCTS\SLIDE_SHOW_SCTS"),
    //FileProvider = new PhysicalFileProvider(@"C:\HOANLOCVIETTRUYENTHONG\SLIDE_SHOW_SCTS\SLIDE_SHOW_SCTS\DATA"),
    //FileProvider = new PhysicalFileProvider(@"C:\TRUYENTHONG\SLIDE_SHOW_SCTS\SLIDE_SHOW_SCTS"),

    //C:\TRUYENTHONG\SLIDE_SHOW_SCTS\SLIDE_SHOW_SCTS

    RequestPath = "/StaticData"
});
app.UseAuthorization();

app.MapControllers();

app.Run();