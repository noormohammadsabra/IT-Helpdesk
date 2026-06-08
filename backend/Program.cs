using System.Security.Claims;
using System.Text;
using HelpDesk.Api.Models;
using HelpDesk.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()
    ?? throw new InvalidOperationException("JWT settings are missing.");

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy
            .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
            ClockSkew = TimeSpan.FromMinutes(2),
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddOpenApi();
builder.Services.AddSingleton<PasswordService>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<DatabaseInitializer>();
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<TicketRepository>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var databaseInitializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    await databaseInitializer.InitializeAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("FrontendPolicy");
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new
{
    application = "IT Help Desk API",
    status = "Running",
    week = "Week 4 - Ticket Management",
}));

app.MapPost("/api/auth/login", async (
    LoginRequest request,
    UserRepository users,
    PasswordService passwords,
    TokenService tokens) =>
{
    var user = await users.GetByEmailAsync(request.Email);

    if (user is null || !passwords.VerifyPassword(request.Password, user.PasswordHash))
    {
        return Results.Unauthorized();
    }

    var token = tokens.CreateToken(user);

    return Results.Ok(new LoginResponse(
        token,
        user.FullName,
        user.Email,
        user.RoleName,
        user.Department));
});

app.MapPost("/api/auth/register", async (
    RegisterRequest request,
    UserRepository users,
    PasswordService passwords) =>
{
    var existingUser = await users.GetByEmailAsync(request.Email);

    if (existingUser is not null)
    {
        return Results.Conflict(new { message = "A user with this email already exists." });
    }

    var passwordHash = passwords.HashPassword(request.Password);
    var user = await users.CreateAsync(request, passwordHash);

    return Results.Created($"/api/users/{user.Id}", new
    {
        user.Id,
        user.FullName,
        user.Email,
        user.RoleName,
        user.Department,
    });
});

app.MapGet("/api/auth/me", [Authorize] (ClaimsPrincipal user) =>
{
    return Results.Ok(new
    {
        id = user.FindFirstValue(ClaimTypes.NameIdentifier),
        fullName = user.FindFirstValue(ClaimTypes.Name),
        email = user.FindFirstValue(ClaimTypes.Email),
        role = user.FindFirstValue(ClaimTypes.Role),
    });
});

app.MapGet("/api/dashboard", [Authorize] () =>
{
    return Results.Ok(new
    {
        openTickets = 18,
        inProgressTickets = 7,
        resolvedTickets = 25,
        recentTickets = new[]
        {
            new { reference = "HD-001", title = "Email access issue", priority = "High", status = "Open" },
            new { reference = "HD-002", title = "Printer not responding", priority = "Medium", status = "In Progress" },
        },
    });
});

app.MapGet("/api/ticket-categories", [Authorize] async (TicketRepository tickets) =>
{
    return Results.Ok(await tickets.GetCategoriesAsync());
});

app.MapGet("/api/ticket-priorities", [Authorize] async (TicketRepository tickets) =>
{
    return Results.Ok(await tickets.GetPrioritiesAsync());
});

app.MapGet("/api/ticket-statuses", [Authorize] async (TicketRepository tickets) =>
{
    return Results.Ok(await tickets.GetStatusesAsync());
});

app.MapGet("/api/tickets", [Authorize] async (ClaimsPrincipal user, TicketRepository tickets) =>
{
    var userId = GetCurrentUserId(user);
    var role = GetCurrentUserRole(user);
    return Results.Ok(await tickets.GetTicketsAsync(userId, role));
});

app.MapPost("/api/tickets", [Authorize] async (
    TicketCreateRequest request,
    ClaimsPrincipal user,
    TicketRepository tickets) =>
{
    if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Description))
    {
        return Results.BadRequest(new { message = "Title and description are required." });
    }

    var userId = GetCurrentUserId(user);
    var createdTicket = await tickets.CreateTicketAsync(request, userId);
    return Results.Created($"/api/tickets/{createdTicket.Id}", createdTicket);
});

app.MapPut("/api/tickets/{id:int}", [Authorize] async (
    int id,
    TicketUpdateRequest request,
    ClaimsPrincipal user,
    TicketRepository tickets) =>
{
    if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Description))
    {
        return Results.BadRequest(new { message = "Title and description are required." });
    }

    var userId = GetCurrentUserId(user);
    var role = GetCurrentUserRole(user);
    var updatedTicket = await tickets.UpdateTicketAsync(id, request, userId, role);

    return updatedTicket is null ? Results.NotFound() : Results.Ok(updatedTicket);
});

app.MapDelete("/api/tickets/{id:int}", [Authorize] async (
    int id,
    ClaimsPrincipal user,
    TicketRepository tickets) =>
{
    var userId = GetCurrentUserId(user);
    var role = GetCurrentUserRole(user);
    var deleted = await tickets.DeleteTicketAsync(id, userId, role);

    return deleted ? Results.NoContent() : Results.NotFound();
});

app.MapGet("/api/admin/users", [Authorize(Roles = "Admin")] async (UserRepository users) =>
{
    var allUsers = await users.GetAllAsync();
    return Results.Ok(allUsers.Select(user => new
    {
        user.Id,
        user.FullName,
        user.Email,
        user.RoleName,
        user.Department,
        user.IsActive,
    }));
});

app.Run();

static int GetCurrentUserId(ClaimsPrincipal user)
{
    var userIdValue = user.FindFirstValue(ClaimTypes.NameIdentifier);
    return int.TryParse(userIdValue, out var userId)
        ? userId
        : throw new InvalidOperationException("User ID claim is missing.");
}

static string GetCurrentUserRole(ClaimsPrincipal user)
{
    return user.FindFirstValue(ClaimTypes.Role)
        ?? throw new InvalidOperationException("Role claim is missing.");
}
