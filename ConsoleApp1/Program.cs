using System.IdentityModel.Tokens.Jwt;
using System.Net.Mail;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("Quotes")
    ?? throw new InvalidOperationException("Connection string 'Quotes' is missing.");
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection["Key"]
    ?? throw new InvalidOperationException("JWT signing key is missing. Configure Jwt:Key.");
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:4200"];

if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
{
    throw new InvalidOperationException("The JWT signing key must be at least 32 bytes.");
}

var issuer = jwtSection["Issuer"] ?? "Citatboken.Api";
var audience = jwtSection["Audience"] ?? "Citatboken.Angular";

builder.Services.AddDbContext<QuotesDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddCors(options => options.AddPolicy("Angular", policy =>
    policy.WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()));
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = ClaimTypes.Name,
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var database = scope.ServiceProvider.GetRequiredService<QuotesDbContext>();
    await database.Database.EnsureCreatedAsync();
}

app.UseCors("Angular");
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

var auth = app.MapGroup("/api/auth");
auth.MapPost("/register", Register);
auth.MapPost("/login", Login);

var quotes = app.MapGroup("/api/quotes").RequireAuthorization();
quotes.MapGet("/", ListQuotes);
quotes.MapPost("/", CreateQuote);
quotes.MapPut("/{id:int}", UpdateQuote);
quotes.MapDelete("/{id:int}", DeleteQuote);

app.Run();

async Task<IResult> Register(
    RegisterRequest request,
    QuotesDbContext database,
    IPasswordHasher<User> passwordHasher,
    CancellationToken cancellationToken)
{
    var email = request.Email?.Trim().ToLowerInvariant();
    var userName = request.UserName?.Trim();
    var errors = new Dictionary<string, string[]>();

    if (string.IsNullOrWhiteSpace(email) || email.Length > 254 || !MailAddress.TryCreate(email, out _))
    {
        errors["email"] = ["Använd en giltig e-postadress."];
    }

    if (string.IsNullOrWhiteSpace(userName) || userName.Length is < 2 or > 40)
    {
        errors["userName"] = ["Namnet måste innehålla mellan 2 och 40 tecken."];
    }

    if (string.IsNullOrEmpty(request.Password) || request.Password.Length is < 8 or > 128)
    {
        errors["password"] = ["Lösenordet måste innehålla mellan 8 och 128 tecken."];
    }

    if (errors.Count != 0)
    {
        return Results.ValidationProblem(errors);
    }

    if (await database.Users.AnyAsync(user => user.Email == email, cancellationToken))
    {
        return Results.Conflict(new { detail = "Det finns redan ett konto med den e-postadressen." });
    }

    var user = new User { Email = email!, UserName = userName! };
    user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
    database.Users.Add(user);
    await database.SaveChangesAsync(cancellationToken);

    return Results.Ok(CreateTokenResponse(user, jwtKey, issuer, audience));
}

async Task<IResult> Login(
    LoginRequest request,
    QuotesDbContext database,
    IPasswordHasher<User> passwordHasher,
    CancellationToken cancellationToken)
{
    var email = request.Email?.Trim().ToLowerInvariant();
    var user = email is null
        ? null
        : await database.Users.SingleOrDefaultAsync(
            candidate => candidate.Email == email,
            cancellationToken);

    if (user is null || string.IsNullOrEmpty(request.Password))
    {
        return Results.Unauthorized();
    }

    var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
    if (verification == PasswordVerificationResult.Failed)
    {
        return Results.Unauthorized();
    }

    if (verification == PasswordVerificationResult.SuccessRehashNeeded)
    {
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        await database.SaveChangesAsync(cancellationToken);
    }

    return Results.Ok(CreateTokenResponse(user, jwtKey, issuer, audience));
}

static async Task<IResult> ListQuotes(
    ClaimsPrincipal principal,
    QuotesDbContext database,
    CancellationToken cancellationToken)
{
    if (!TryGetUserId(principal, out var userId))
    {
        return Results.Unauthorized();
    }

    var result = await database.Quotes
        .AsNoTracking()
        .Where(quote => quote.UserId == userId)
        .OrderByDescending(quote => quote.CreatedAtUtc)
        .Select(quote => new QuoteResponse(
            quote.Id,
            quote.Text,
            quote.Author,
            quote.CreatedAtUtc))
        .ToListAsync(cancellationToken);

    return Results.Ok(result);
}

static async Task<IResult> CreateQuote(
    QuoteRequest request,
    ClaimsPrincipal principal,
    QuotesDbContext database,
    CancellationToken cancellationToken)
{
    if (!TryGetUserId(principal, out var userId))
    {
        return Results.Unauthorized();
    }

    var validation = ValidateQuote(request);
    if (validation is not null)
    {
        return validation;
    }

    var quote = new Quote
    {
        Text = request.Text.Trim(),
        Author = NormalizeAuthor(request.Author),
        UserId = userId,
        CreatedAtUtc = DateTime.UtcNow,
    };

    database.Quotes.Add(quote);
    await database.SaveChangesAsync(cancellationToken);

    return Results.Created($"/api/quotes/{quote.Id}", ToResponse(quote));
}

static async Task<IResult> UpdateQuote(
    int id,
    QuoteRequest request,
    ClaimsPrincipal principal,
    QuotesDbContext database,
    CancellationToken cancellationToken)
{
    if (!TryGetUserId(principal, out var userId))
    {
        return Results.Unauthorized();
    }

    var validation = ValidateQuote(request);
    if (validation is not null)
    {
        return validation;
    }

    var quote = await database.Quotes
        .FirstOrDefaultAsync(candidate => candidate.Id == id && candidate.UserId == userId, cancellationToken);
    if (quote is null)
    {
        return Results.NotFound();
    }

    quote.Text = request.Text.Trim();
    quote.Author = NormalizeAuthor(request.Author);
    await database.SaveChangesAsync(cancellationToken);

    return Results.Ok(ToResponse(quote));
}

static async Task<IResult> DeleteQuote(
    int id,
    ClaimsPrincipal principal,
    QuotesDbContext database,
    CancellationToken cancellationToken)
{
    if (!TryGetUserId(principal, out var userId))
    {
        return Results.Unauthorized();
    }

    var quote = await database.Quotes
        .FirstOrDefaultAsync(candidate => candidate.Id == id && candidate.UserId == userId, cancellationToken);
    if (quote is null)
    {
        return Results.NotFound();
    }

    database.Quotes.Remove(quote);
    await database.SaveChangesAsync(cancellationToken);

    return Results.NoContent();
}

static IResult? ValidateQuote(QuoteRequest request)
{
    var errors = new Dictionary<string, string[]>();
    if (string.IsNullOrWhiteSpace(request.Text) || request.Text.Trim().Length > 1000)
    {
        errors["text"] = ["Citatet måste innehålla mellan 1 och 1000 tecken."];
    }

    if (request.Author is { } author && author.Trim().Length > 120)
    {
        errors["author"] = ["Upphovspersonen får innehålla högst 120 tecken."];
    }

    return errors.Count == 0 ? null : Results.ValidationProblem(errors);
}

static string? NormalizeAuthor(string? author) =>
    string.IsNullOrWhiteSpace(author) ? null : author.Trim();

static bool TryGetUserId(ClaimsPrincipal principal, out int userId) =>
    int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

static QuoteResponse ToResponse(Quote quote) =>
    new(quote.Id, quote.Text, quote.Author, quote.CreatedAtUtc);

static AuthResponse CreateTokenResponse(User user, string key, string issuer, string audience)
{
    var expiresAt = DateTime.UtcNow.AddHours(1);
    var token = new JwtSecurityToken(
        issuer: issuer,
        audience: audience,
        claims:
        [
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.UserName),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
        ],
        expires: expiresAt,
        signingCredentials: new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256));

    return new AuthResponse(
        new JwtSecurityTokenHandler().WriteToken(token),
        user.Email,
        user.UserName,
        expiresAt);
}

public sealed class QuotesDbContext(DbContextOptions<QuotesDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Quote> Quotes => Set<Quote>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>()
            .HasIndex(user => user.Email)
            .IsUnique();
        modelBuilder.Entity<User>()
            .HasMany(user => user.Quotes)
            .WithOne()
            .HasForeignKey(quote => quote.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class User
{
    public int Id { get; set; }
    public required string Email { get; set; }
    public required string UserName { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public List<Quote> Quotes { get; set; } = [];
}

public sealed class Quote
{
    public int Id { get; set; }
    public required string Text { get; set; }
    public string? Author { get; set; }
    public int UserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed record RegisterRequest(string Email, string UserName, string Password);
public sealed record LoginRequest(string Email, string Password);
public sealed record QuoteRequest(string Text, string? Author);
public sealed record QuoteResponse(int Id, string Text, string? Author, DateTime CreatedAtUtc);
public sealed record AuthResponse(string AccessToken, string Email, string UserName, DateTime ExpiresAt);
