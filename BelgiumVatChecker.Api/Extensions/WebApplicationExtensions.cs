namespace BelgiumVatChecker.Api.Extensions;

public static class WebApplicationExtensions
{
    public static WebApplication UseSwaggerDocumentation(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Belgium VAT Checker API v1");
                c.RoutePrefix = "swagger";
            });
        }

        return app;
    }

    public static WebApplication UseApiMiddleware(this WebApplication app)
    {
        app.UseHttpsRedirection();
        app.UseCors();
        app.UseAuthorization();
        app.MapControllers();

        return app;
    }
}