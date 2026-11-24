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
    await database.Database.MigrateAsync();
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

var books = app.MapGroup("/api/books").RequireAuthorization();
books.MapGet("/", ListBooks);
books.MapGet("/{id:int}", GetBook);
books.MapPost("/", CreateBook);
books.MapPut("/{id:int}", UpdateBook);
books.MapDelete("/{id:int}", DeleteBook);

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
    if (string.IsNullOrWhiteSpace(request.Identifier) || request.Identifier.Trim().Length > 254)
    {
        return Results.Unauthorized();
    }

    var identifier = request.Identifier.Trim().ToLowerInvariant();
    var matchingUsers = await database.Users
        .Where(candidate => candidate.Email == identifier || candidate.UserName.ToLower() == identifier)
        .Take(2)
        .ToListAsync(cancellationToken);

    if (matchingUsers.Count != 1 || string.IsNullOrEmpty(request.Password))
    {
        return Results.Unauthorized();
    }

    var user = matchingUsers[0];
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

static async Task<IResult> ListBooks(
    ClaimsPrincipal principal,
    QuotesDbContext database,
    CancellationToken cancellationToken)
{
    if (!TryGetUserId(principal, out var userId))
    {
        return Results.Unauthorized();
    }

    var result = await database.Books
        .AsNoTracking()
        .Where(book => book.UserId == userId)
        .OrderBy(book => book.Title)
        .Select(book => new BookResponse(
            book.Id,
            book.Title,
            book.Author,
            book.PublicationDate))
        .ToListAsync(cancellationToken);

    return Results.Ok(result);
}

static async Task<IResult> GetBook(
    int id,
    ClaimsPrincipal principal,
    QuotesDbContext database,
    CancellationToken cancellationToken)
{
    if (!TryGetUserId(principal, out var userId))
    {
        return Results.Unauthorized();
    }

    var book = await database.Books
        .AsNoTracking()
        .Where(book => book.Id == id && book.UserId == userId)
        .Select(book => new BookResponse(
            book.Id,
            book.Title,
            book.Author,
            book.PublicationDate))
        .FirstOrDefaultAsync(cancellationToken);

    return book is null ? Results.NotFound() : Results.Ok(book);
}

static async Task<IResult> CreateBook(
    BookRequest request,
    ClaimsPrincipal principal,
    QuotesDbContext database,
    CancellationToken cancellationToken)
{
    if (!TryGetUserId(principal, out var userId))
    {
        return Results.Unauthorized();
    }

    if (string.IsNullOrWhiteSpace(request.Title) ||
        string.IsNullOrWhiteSpace(request.Author) ||
        string.IsNullOrWhiteSpace(request.PublicationDate))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["book"] = ["Titel, författare och publiceringsdatum måste anges."]
        });
    }

    var book = new Book
    {
        Title = request.Title.Trim(),
        Author = request.Author.Trim(),
        PublicationDate = request.PublicationDate.Trim(),
        UserId = userId
    };

    database.Books.Add(book);
    await database.SaveChangesAsync(cancellationToken);

    var response = new BookResponse(
        book.Id,
        book.Title,
        book.Author,
        book.PublicationDate);

    return Results.Created($"/api/books/{book.Id}", response);
}

static async Task<IResult> UpdateBook(
    int id,
    BookRequest request,
    ClaimsPrincipal principal,
    QuotesDbContext database,
    CancellationToken cancellationToken)
{
    if (!TryGetUserId(principal, out var userId))
    {
        return Results.Unauthorized();
    }

    var book = await database.Books
        .FirstOrDefaultAsync(
            book => book.Id == id && book.UserId == userId,
            cancellationToken);

    if (book is null)
    {
        return Results.NotFound();
    }

    book.Title = request.Title.Trim();
    book.Author = request.Author.Trim();
    book.PublicationDate = request.PublicationDate.Trim();

    await database.SaveChangesAsync(cancellationToken);

    return Results.Ok(new BookResponse(
        book.Id,
        book.Title,
        book.Author,
        book.PublicationDate));
}

static async Task<IResult> DeleteBook(
    int id,
    ClaimsPrincipal principal,
    QuotesDbContext database,
    CancellationToken cancellationToken)
{
    if (!TryGetUserId(principal, out var userId))
    {
        return Results.Unauthorized();
    }

    var book = await database.Books
        .FirstOrDefaultAsync(
            book => book.Id == id && book.UserId == userId,
            cancellationToken);

    if (book is null)
    {
        return Results.NotFound();
    }

    database.Books.Remove(book);
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
    public DbSet<Book> Books => Set<Book>();

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
        
        modelBuilder.Entity<User>()
            .HasMany(user => user.Books)
            .WithOne()
            .HasForeignKey(book => book.UserId)
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
    
    public List<Book> Books { get; set; } = [];
}

public sealed class Quote
{
    public int Id { get; set; }
    public required string Text { get; set; }
    public string? Author { get; set; }
    public int UserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class Book
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public required string Author { get; set; }
    public required string PublicationDate { get; set; }
    public int UserId { get; set; }
}

public sealed record BookResponse(
    int Id,
    string Title,
    string Author,
    string PublicationDate);

public sealed record BookRequest(
    string Title,
    string Author,
    string PublicationDate);

public sealed record RegisterRequest(string Email, string UserName, string Password);
public sealed record LoginRequest(string Identifier, string Password);
public sealed record QuoteRequest(string Text, string? Author);
public sealed record QuoteResponse(int Id, string Text, string? Author, DateTime CreatedAtUtc);
public sealed record AuthResponse(string AccessToken, string Email, string UserName, DateTime ExpiresAt);
